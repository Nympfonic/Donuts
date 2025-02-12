using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityToolkit.Structures.EventBus;
using BotProfileData = GClass652; // Implements IGetProfileData
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public interface IBotDataService
{
	public Queue<PrepBotInfo> StartingBotsCache { get; }
	public ZoneSpawnPoints ZoneSpawnPoints { get; }
	public int MaxBotLimit { get; }
	public ReadOnlyCollection<Player> AllAlivePlayers { get; }

	UniTask SetupStartingBotCache(CancellationToken cancellationToken);
	UniTask<(bool success, PrepBotInfo prepBotInfo)> TryCreateBotData(BotDifficulty difficulty, int groupSize,
		bool saveToCache = true, CancellationToken cancellationToken = default);
	UniTask ReplenishBotCache(CancellationToken cancellationToken);
	[CanBeNull] PrepBotInfo FindCachedBotData(BotDifficulty difficulty, int targetCount);
	void RemoveFromBotCache(PrepBotInfo.GroupDifficultyKey key);
	[NotNull] Queue<BotWave> GetBotWavesToSpawn();
	[CanBeNull] Vector3? GetUnusedSpawnPoint(SpawnPointType spawnPointType = SpawnPointType.Standard);
	void ResetGroupTimers(int groupNum);
	int GetAliveBotsCount();
	BotDifficulty GetBotDifficulty();
}

public abstract class BotDataService : IBotDataService
{
	public readonly struct ResetReplenishTimerEvent : IEvent;
	
	public readonly struct UpdateWaveTimerEvent(float deltaTime) : IEvent
	{
		public readonly float deltaTime = deltaTime;
	}
	
	protected StartingBotConfig startingBotConfig;
	protected readonly MapBotWaves mapBotWaves;

	protected readonly BotConfigService configService;
	protected readonly ManualLogSource logger;
	
	private const int INITIAL_BOT_CACHE_SIZE = 30;
	private const int NUMBER_OF_GROUPS_TO_REPLENISH = 3;
	private const int FRAME_DELAY_BETWEEN_REPLENISH = 5;
	
	private readonly BotCreationDataCache _botCache = new(INITIAL_BOT_CACHE_SIZE);
	private readonly IBotCreator _botCreator;
	private readonly BotSpawner _eftBotSpawner;
	
	private float _replenishBotCachePrevTime;
	
	private int _totalBots;
	
	private readonly Queue<BotWave> _cachedBotWavesToSpawn = new(10);
	
	protected DonutsSpawnType spawnType;
	
	protected BotWave[] botWaves;
	protected ILookup<int, BotWave> botWavesByGroupNum;
	protected (int min, int max) waveGroupSize;
	
	protected SpawnPointsCache startingSpawnPointsCache;
	// TODO: Figure out how to implement remembering used spawn points for individual bot waves
	//protected SpawnPointsCache waveSpawnPointsCache;
	
	public Queue<PrepBotInfo> StartingBotsCache { get; } = new(INITIAL_BOT_CACHE_SIZE);
	public ZoneSpawnPoints ZoneSpawnPoints { get; }
	public int MaxBotLimit { get; protected set; }
	public ReadOnlyCollection<Player> AllAlivePlayers { get; }
	
	protected abstract string GroupChance { get; }
	protected abstract ReadOnlyCollection<BotDifficulty> BotDifficulties { get; }
	
	protected BotDataService([NotNull] BotConfigService configService)
	{
		this.configService = configService;
		logger = DonutsRaidManager.Logger;
		
		_eftBotSpawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
		_botCreator = (IBotCreator)ReflectionHelper.BotSpawner_botCreator_Field.GetValue(_eftBotSpawner);
		
		var resetReplenishTimerBinding = new EventBinding<ResetReplenishTimerEvent>(ResetReplenishTimer);
		EventBus.Register(resetReplenishTimerBinding);

		var updateWaveTimerBinding = new EventBinding<UpdateWaveTimerEvent>(UpdateBotWaveTimers);
		EventBus.Register(updateWaveTimerBinding);
		
		string location = configService.GetMapLocation();
		ZoneSpawnPoints = configService.GetAllMapsZoneConfigs()!.Maps[location].Zones;
		if (ZoneSpawnPoints.Count == 0)
		{
			DonutsHelper.NotifyLogError("Donuts: Failed to load zone spawn points. Check your 'zoneSpawnPoints' folder!");
			return;
		}
		
		AllAlivePlayers = new ReadOnlyCollection<Player>(Singleton<GameWorld>.Instance.AllAlivePlayersList);
		
		if (!configService.CheckForAnyScenarioPatterns())
		{
			return;
		}
		
		mapBotWaves = configService.GetAllMapsBotWavesConfigs()?.Maps[location];
		if (mapBotWaves == null)
		{
			DonutsHelper.NotifyLogError("Donuts: Failed to load bot waves. Donuts will not function properly.");
			return;
		}
	}
	
	/// <summary>
	/// Gets an unused starting spawn point.
	/// </summary>
	/// <returns>An unused starting spawn point or otherwise null.</returns>
	public Vector3? GetUnusedSpawnPoint(SpawnPointType spawnPointType = SpawnPointType.Standard)
	{
		return spawnPointType switch
		{
			SpawnPointType.Starting => startingSpawnPointsCache.GetUnusedSpawnPoint(),
			_ => throw new InvalidOperationException("Invalid spawn point type!"),
		};
	}
	
	private void ResetReplenishTimer()
	{
		_replenishBotCachePrevTime = Time.time;
	}
	
	/// <summary>
	/// Initializes the starting bot cache.
	/// </summary>
	public async UniTask SetupStartingBotCache(CancellationToken cancellationToken)
	{
		try
		{
			int maxBots = Random.Range(startingBotConfig.MinCount, startingBotConfig.MaxCount);
			int minGroupSize = Math.Max(startingBotConfig.MinGroupSize, 1);
			int maxGroupSize = startingBotConfig.MaxGroupSize;
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				logger.LogDebugDetailed($"Max starting bots set to {maxBots.ToString()}", GetType().Name, nameof(SetupStartingBotCache));
			}
			
			var currentBotCount = 0;
			while (currentBotCount < maxBots && !cancellationToken.IsCancellationRequested)
			{
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, minGroupSize, maxGroupSize, maxBots - currentBotCount);
				
				(bool success, PrepBotInfo prepBotInfo) = await TryCreateBotData(BotDifficulties.PickRandomElement(),
					groupSize, saveToCache: false, cancellationToken: cancellationToken);
				if (cancellationToken.IsCancellationRequested) return;
				
				if (success)
				{
					StartingBotsCache.Enqueue(prepBotInfo);
					currentBotCount += groupSize;
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogException(GetType().Name, nameof(SetupStartingBotCache), ex);
		}
		catch (OperationCanceledException) {}
	}
	
	protected static (int min, int max) GetWaveMinMaxGroupSize(IReadOnlyList<BotWave> waves)
	{
		var minGroupSize = 1;
		var maxGroupSize = int.MaxValue;
		
		foreach (BotWave wave in waves)
		{
			if (wave.MinGroupSize > minGroupSize)
			{
				minGroupSize = wave.MinGroupSize;
			}

			if (wave.MaxGroupSize < maxGroupSize)
			{
				maxGroupSize = wave.MaxGroupSize;
			}
		}
		
		return (minGroupSize, maxGroupSize);
	}
	
	public abstract BotDifficulty GetBotDifficulty();
	
	public async UniTask<(bool success, PrepBotInfo prepBotInfo)> TryCreateBotData(
		BotDifficulty difficulty,
		int groupSize,
		bool saveToCache = true,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return (false, null);
			}
			
			WildSpawnType wildSpawnType = GetWildSpawnType();
			EPlayerSide side = GetPlayerSide(wildSpawnType);
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Creating bot: Type={0}, Difficulty={1}, Side={2}, GroupSize={3}",
					wildSpawnType.ToString(), difficulty.ToString(), side.ToString(), groupSize.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryCreateBotData));
			}
			
			var botProfileData = new BotProfileData(side, wildSpawnType, difficulty, 0f);
			var botCreationData = await BotCreationDataClass
				.Create(botProfileData, _botCreator, groupSize, _eftBotSpawner);
			
			if (botCreationData?.Profiles == null || botCreationData.Profiles.Count == 0)
			{
				return (false, null);
			}

			var prepBotInfo = new PrepBotInfo(botCreationData, difficulty);
			if (saveToCache)
			{
				_botCache.Enqueue(prepBotInfo.groupDifficultyKey, prepBotInfo);
			}
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Bot created and assigned successfully; {0} profiles loaded. IDs: {1}",
					botCreationData.Profiles.Count.ToString(), string.Join(", ", botCreationData.Profiles.Select(p => p.Id)));
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryCreateBotData));
			}
			
			return (true, prepBotInfo);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogException(GetType().Name, nameof(TryCreateBotData), ex);
		}
		catch (OperationCanceledException) {}
		
		return (false, null);
	}
	
	protected abstract WildSpawnType GetWildSpawnType();
	protected abstract EPlayerSide GetPlayerSide(WildSpawnType wildSpawnType);
	
	public async UniTask ReplenishBotCache(CancellationToken cancellationToken)
	{
		try
		{
			if (Time.time < _replenishBotCachePrevTime + DefaultPluginVars.replenishInterval.Value)
			{
				return;
			}
			
			var generatedCount = 0;
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			while (generatedCount < NUMBER_OF_GROUPS_TO_REPLENISH &&
				_totalBots < MaxBotLimit &&
				!cancellationToken.IsCancellationRequested)
			{
				BotDifficulty difficulty = BotDifficulties.PickRandomElement();
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, waveGroupSize.min, waveGroupSize.max);
				
				(bool success, PrepBotInfo prepBotInfo) =
					await TryCreateBotData(difficulty, groupSize, cancellationToken: cancellationToken);
				if (cancellationToken.IsCancellationRequested) return;
				if (!success)
				{
					await UniTask.DelayFrame(FRAME_DELAY_BETWEEN_REPLENISH, cancellationToken: cancellationToken);
					continue;
				}
				
				generatedCount++;
				_totalBots += groupSize;
				
				if (DefaultPluginVars.debugLogging.Value)
				{
					prepBotInfo.botCreationData._profileData.TryGetRole(out WildSpawnType role, out _);
					sb.Clear();
					sb.AppendFormat("Replenishing group bot: {0} {1} {2} Count: {3}.", role.ToString(),
						prepBotInfo.difficulty.ToString(), prepBotInfo.botCreationData.Side.ToString(),
						prepBotInfo.groupSize.ToString());
					logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(ReplenishBotCache));
				}
				
				await UniTask.DelayFrame(FRAME_DELAY_BETWEEN_REPLENISH, cancellationToken: cancellationToken);
			}
			
			ResetReplenishTimer();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogException(GetType().Name, nameof(ReplenishBotCache), ex);
		}
		catch (OperationCanceledException) {}
	}
	
	public PrepBotInfo FindCachedBotData(BotDifficulty difficulty, int groupSize)
	{
		// Find PrepBotInfo that matches the difficulty and group size
		if (_botCache.TryPeek(new PrepBotInfo.GroupDifficultyKey(difficulty, groupSize), out PrepBotInfo prepBotInfo) &&
			prepBotInfo.botCreationData?.Profiles?.Count == groupSize)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Found cached bots for difficulty {0}, and target count {1}.", difficulty.ToString(),
					groupSize.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(FindCachedBotData));
			}
			
			return prepBotInfo;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("No cached bots found for difficulty {0}, and target count {1}.", difficulty.ToString(),
				groupSize.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(FindCachedBotData));
		}
		
		return null;
	}
	
	public void RemoveFromBotCache(PrepBotInfo.GroupDifficultyKey key)
	{
		if (_botCache.TryDequeue(key, out PrepBotInfo prepBotInfo))
		{
			_totalBots -= prepBotInfo!.groupSize;
			return;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			logger.LogDebugDetailed(
				"Failure trying to dequeue PrepBotInfo from bot cache.",
				GetType().Name, nameof(RemoveFromBotCache));
		}
	}
	
	/// <summary>
	/// Gets a queue of bot waves which meet the time requirement to spawn.
	/// </summary>
	/// <remarks>Caches bot waves to spawn until the queue count reaches zero.</remarks>
	public Queue<BotWave> GetBotWavesToSpawn()
	{
		if (botWaves.Length == 0 || _cachedBotWavesToSpawn.Count > 0)
		{
			return _cachedBotWavesToSpawn;
		}
		
		foreach (BotWave wave in botWaves.ShuffleElements())
		{
			if (wave.ShouldSpawn())
			{
				_cachedBotWavesToSpawn.Enqueue(wave);
			}
		}
		
		return _cachedBotWavesToSpawn;
	}
	
	/// <summary>
	/// Updates all bot wave timers, incrementing by the delta time.
	/// </summary>
	private void UpdateBotWaveTimers(UpdateWaveTimerEvent eventData)
	{
		float cooldownDuration = DefaultPluginVars.coolDownTimer.Value;
		foreach (BotWave wave in botWaves)
		{
			wave.UpdateTimer(eventData.deltaTime, cooldownDuration);
		}
	}
	
	/// <summary>
	/// Resets timers for every wave sharing the same group number.
	/// </summary>
	public void ResetGroupTimers(int groupNum)
	{
		foreach (BotWave wave in botWavesByGroupNum[groupNum])
		{
			wave.ResetTimer();
		}
	}
	
	public abstract int GetAliveBotsCount();
	
	protected static BotDifficulty GetBotDifficulty(string settingValue)
	{
		string difficultyLower = settingValue.ToLower();
		switch (difficultyLower)
		{
			case "asonline":
				return DefaultPluginVars.BotDifficulties[Random.Range(0, 3)];
			case "easy":
			case "normal":
			case "hard":
			case "impossible":
				if (!Enum.TryParse(difficultyLower, out BotDifficulty result))
				{
					goto default;
				}
				return result;
			default:
				return BotDifficulty.normal;
		}
	}
}