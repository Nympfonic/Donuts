﻿using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Spawning.Models;
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
using Random = UnityEngine.Random;

namespace Donuts.Spawning.Services;

public interface IBotDataService : IServiceSpawnType, IDisposable
{
	public Queue<PrepBotInfo> StartingBotsCache { get; }
	public ZoneSpawnPoints ZoneSpawnPoints { get; }
	public int MaxBotLimit { get; }
	public ReadOnlyCollection<Player> AllAlivePlayers { get; }
	public string GroupChance { get; }
	public ReadOnlyCollection<BotDifficulty> BotDifficulties { get; }
	
	[NotNull] IUniTaskAsyncEnumerable<BotGenerationProgress> CreateStartingBotGenerationStream(CancellationToken cancellationToken);
	UniTask<(bool success, PrepBotInfo prepBotInfo)> TryGenerateBotProfiles(BotDifficulty difficulty, int groupSize,
		bool saveToCache = true, CancellationToken cancellationToken = default);
	UniTask ReplenishBotCache(CancellationToken cancellationToken);
	[CanBeNull] PrepBotInfo FindCachedBotData(BotDifficulty difficulty, int targetCount);
	void RemoveFromBotCache(PrepBotInfo.Key key);
	[CanBeNull] BotWave GetBotWaveToSpawn();
	[CanBeNull] Vector3? GetUnusedSpawnPoint(SpawnPointType spawnPointType = SpawnPointType.Standard);
	void ResetGroupTimers(int groupNum);
	int GetAliveBotsCount();
	BotDifficulty GetBotDifficulty();
}

public abstract class BotDataService : IBotDataService, ICancellable
{
	protected StartingBotConfig startingBotConfig;
	protected readonly MapBotWaves mapBotWaves;

	protected readonly BotConfigService configService;
	protected readonly ManualLogSource logger;
	
	private const int INITIAL_BOT_CACHE_SIZE = 30;
	private const int NUMBER_OF_GROUPS_TO_REPLENISH = 3;
	private const int FRAME_DELAY_BETWEEN_REPLENISH = 10;
	
	private readonly TimeoutController _timeoutController;
	private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);
	private CancellationToken _sharedToken;
	
	private readonly BotCreationDataCache _botCache = new(INITIAL_BOT_CACHE_SIZE);
	private readonly IBotCreator _botCreator;
	
	private float _replenishBotCachePrevTime;
	
	private int _totalBotsInCache;
	
	protected BotWave[] botWaves;
	protected ILookup<int, BotWave> botWavesByGroupNum;
	protected (int min, int max) waveGroupSize;

	private readonly List<BotWave> _botWaveSpawnBuffer = new(10);
	
	protected SpawnPointsCache startingSpawnPointsCache;
	// TODO: Figure out how to implement remembering used spawn points for individual bot waves
	//protected SpawnPointsCache waveSpawnPointsCache;
	
	private int _startingBotsToGenerate = -1;
	
	public abstract DonutsSpawnType SpawnType { get; }
	public Queue<PrepBotInfo> StartingBotsCache { get; } = new(INITIAL_BOT_CACHE_SIZE);
	public ZoneSpawnPoints ZoneSpawnPoints { get; }
	public int MaxBotLimit { get; protected set; }
	public ReadOnlyCollection<Player> AllAlivePlayers { get; }
	public abstract string GroupChance { get; }
	public abstract ReadOnlyCollection<BotDifficulty> BotDifficulties { get; }
	
	protected BotDataService([NotNull] BotConfigService configService)
	{
		this.configService = configService;
		logger = DonutsRaidManager.Logger;
		_timeoutController = new TimeoutController(Singleton<DonutsRaidManager>.Instance.OnDestroyTokenSource);
		_botCreator = Singleton<IBotGame>.Instance.BotsController.BotSpawner._botCreator;
		
		RegisterEventBindings();
		
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
	
	private void RegisterEventBindings()
	{
		var playerEnteredCombatBinding = new EventBinding<PlayerEnteredCombatEvent>(ResetReplenishTimer);
		EventBus.Register(playerEnteredCombatBinding);
		
		var resetReplenishTimerBinding = new EventBinding<PlayerTargetedByBotEvent>(ResetReplenishTimer);
		EventBus.Register(resetReplenishTimerBinding);
		
		var botSpawnedBinding = new EventBinding<BotSpawnedEvent>(ResetReplenishTimer);
		EventBus.Register(botSpawnedBinding);
		
		var updateWaveTimerBinding = new EventBinding<EveryUpdateSecondEvent>(UpdateBotWaveTimers);
		EventBus.Register(updateWaveTimerBinding);
	}
	
	/// <remarks>
	/// Should only be used for BSG's bot profile generation!
	/// </remarks>
	CancellationToken ICancellable.GetCancelToken()
	{
		return _sharedToken;
	}
	
	void IDisposable.Dispose()
	{
		_timeoutController?.Dispose();
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
	/// Creates an asynchronous stream for generating the starting bots.
	/// </summary>
	public IUniTaskAsyncEnumerable<BotGenerationProgress> CreateStartingBotGenerationStream(CancellationToken cancellationToken)
	{
		if (_startingBotsToGenerate == -1)
		{
			_startingBotsToGenerate = Random.Range(startingBotConfig.MinCount, startingBotConfig.MaxCount + 1);
			if (DefaultPluginVars.debugLogging.Value)
			{
				logger.LogDebugDetailed($"Max starting bots set to {_startingBotsToGenerate.ToString()}", GetType().Name,
					nameof(CreateStartingBotGenerationStream));
			}
		}
		
		int botsToGenerate = _startingBotsToGenerate - StartingBotsCache.Count;
		
		int minGroupSize = Math.Max(startingBotConfig.MinGroupSize, 1);
		int maxGroupSize = startingBotConfig.MaxGroupSize;
		
		return new GenerateBotProfilesAsyncEnumerable(this, botsToGenerate, minGroupSize, maxGroupSize,
			cancellationToken);
	}
	
	protected static (int min, int max) GetWaveMinMaxGroupSize(IReadOnlyList<BotWave> waves)
	{
		if (waves.Count == 0)
		{
			return (min: 0, max: 0);
		}
		
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
		
		if (maxGroupSize < minGroupSize)
		{
			maxGroupSize = minGroupSize;
		}
		
		return (minGroupSize, maxGroupSize);
	}
	
	public abstract BotDifficulty GetBotDifficulty();
	
	public async UniTask<(bool success, PrepBotInfo prepBotInfo)> TryGenerateBotProfiles(
		BotDifficulty difficulty,
		int groupSize,
		bool saveToCache = true,
		CancellationToken cancellationToken = default)
	{
		await UniTask.SwitchToMainThread(PlayerLoopTiming.Update, cancellationToken);
		if (cancellationToken.IsCancellationRequested)
		{
			return (false, null);
		}
		
		WildSpawnType wildSpawnType = GetWildSpawnType();
		EPlayerSide side = GetPlayerSide(wildSpawnType);
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Generating bot profiles: Type={0}, Difficulty={1}, Side={2}, GroupSize={3}",
				wildSpawnType.ToString(), difficulty.ToString(), side.ToString(), groupSize.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryGenerateBotProfiles));
		}
		
		try
		{
			CancellationToken timeoutToken = _timeoutController.Timeout(s_timeout);
			_sharedToken = timeoutToken;
			
			var botProfileData = new BotProfileRequestData(side, wildSpawnType, difficulty, 0f);
			var botCreationData = await BotCreationDataClass.Create(botProfileData, _botCreator, groupSize, token: this);
			
			_sharedToken = cancellationToken;
			
			if (botCreationData?.Profiles == null || botCreationData.Profiles.Count == 0)
			{
				return (false, null);
			}
			
			var prepBotInfo = new PrepBotInfo(botCreationData, difficulty);
			if (saveToCache)
			{
				_botCache.Enqueue(prepBotInfo.key, prepBotInfo);
				_totalBotsInCache += groupSize;
			}
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Bot profiles generated and assigned successfully; {0} profiles loaded. IDs: {1}",
					botCreationData.Profiles.Count.ToString(),
					string.Join(", ", botCreationData.Profiles.Select(p => p.Id)));
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryGenerateBotProfiles));
			}
			
			return (true, prepBotInfo);
		}
		catch (OperationCanceledException)
		{
			if (_timeoutController.IsTimeout())
			{
				if (DefaultPluginVars.debugLogging.Value)
				{
					using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
					sb.AppendFormat(
						"Generating bot profiles timed out after {0}s; there was probably a bot generation error!",
						s_timeout.Seconds.ToString());
					logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryGenerateBotProfiles));
				}
				
				// Throw TimeoutException to bubble up the call stack so the parent task knows it timed out instead of generic OperationCanceledException
				throw new TimeoutException();
			}
		}
		catch (Exception ex)
		{
			logger.LogException(GetType().Name, nameof(TryGenerateBotProfiles), ex);
		}
		finally
		{
			_timeoutController.Reset();
		}
		
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
				_totalBotsInCache < MaxBotLimit &&
				!cancellationToken.IsCancellationRequested)
			{
				BotDifficulty difficulty = BotDifficulties.PickRandomElement();
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, waveGroupSize.min, waveGroupSize.max);
				
				(bool success, PrepBotInfo prepBotInfo) =
					await TryGenerateBotProfiles(difficulty, groupSize, cancellationToken: cancellationToken);
				if (cancellationToken.IsCancellationRequested) return;
				if (!success)
				{
					await UniTask.DelayFrame(FRAME_DELAY_BETWEEN_REPLENISH, cancellationToken: cancellationToken);
					continue;
				}
				
				generatedCount++;
				
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
		catch (OperationCanceledException) {}
		catch (Exception ex)
		{
			logger.LogException(GetType().Name, nameof(ReplenishBotCache), ex);
		}
	}
	
	public PrepBotInfo FindCachedBotData(BotDifficulty difficulty, int groupSize)
	{
		// Find PrepBotInfo that matches the difficulty and group size
		if (_botCache.TryPeek(new PrepBotInfo.Key(difficulty, groupSize), out PrepBotInfo prepBotInfo) &&
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
	
	public void RemoveFromBotCache(PrepBotInfo.Key key)
	{
		if (_botCache.TryDequeue(key, out PrepBotInfo prepBotInfo))
		{
			_totalBotsInCache -= prepBotInfo!.groupSize;
			return;
		}
		
		logger.LogDebugDetailed("Failure trying to dequeue PrepBotInfo from bot cache.", GetType().Name, nameof(RemoveFromBotCache));
	}
	
	/// <summary>
	/// Gets a random bot wave which meets the time requirement to spawn.
	/// </summary>
	public BotWave GetBotWaveToSpawn()
	{
		if (botWaves.Length == 0)
		{
			return null;
		}
		
		_botWaveSpawnBuffer.Clear();
		
		for (int i = botWaves.Length - 1; i >= 0; i--)
		{
			BotWave wave = botWaves[i];
			if (wave.ShouldSpawn())
			{
				_botWaveSpawnBuffer.Add(wave);
			}
		}
		
		BotWave chosenWave = _botWaveSpawnBuffer.PickRandomElement();
		if (chosenWave != null && DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Selected wave GroupNum={0}, TriggerTime={1} to attempt spawning",
				chosenWave.GroupNum.ToString(), chosenWave.TriggerTimer.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(GetBotWaveToSpawn));
		}
		
		return chosenWave;
	}
	
	/// <summary>
	/// Updates all bot wave timers, incrementing by the delta time.
	/// </summary>
	private void UpdateBotWaveTimers(EveryUpdateSecondEvent eventData)
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