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
	public StartingBotConfig StartingBotConfig { get; }
	public Queue<PrepBotInfo> StartingBotsCache { get; }
	public ZoneSpawnPoints ZoneSpawnPoints { get; }
	public DonutsSpawnType SpawnType { get; }
	public int MaxBotLimit { get; }

	UniTask<(bool success, PrepBotInfo prepBotInfo)> TryCreateBotData(BotDifficulty difficulty, int groupSize);
	UniTask ReplenishBotCache();
	[CanBeNull] PrepBotInfo FindCachedBotData(BotDifficulty difficulty, int targetCount);
	void RemoveFromBotCache(PrepBotInfo.GroupDifficultyKey key);
	[NotNull] Queue<BotWave> GetBotWavesToSpawn();
	void ResetGroupTimers(int groupNum);
	BotDifficulty GetBotDifficulty();
}

public abstract class BotDataService : IBotDataService
{
	public readonly struct ResetReplenishTimerEvent : IEvent;
	
	public readonly struct UpdateWaveTimerEvent(float deltaTime) : IEvent
	{
		public readonly float deltaTime = deltaTime;
	}
	
	private const int INITIAL_BOT_CACHE_SIZE = 30;
	private const int NUMBER_OF_GROUPS_TO_REPLENISH = 3;
	private const int FRAME_DELAY_BETWEEN_REPLENISH = 5;
	
	private readonly BotCreationDataCache _botCache = new(INITIAL_BOT_CACHE_SIZE);
	private IBotCreator _botCreator;
	private BotSpawner _eftBotSpawner;
	private CancellationToken _onDestroyToken;
	
	private EventBinding<ResetReplenishTimerEvent> _resetReplenishTimerBinding;
	private float _replenishBotCachePrevTime;
	
	private EventBinding<UpdateWaveTimerEvent> _updateWaveTimerBinding;
	
	private int _totalBots;
	
	private List<BotWave> _botWaves;
	private ILookup<int, BotWave> _botWavesByGroupNum;
	private (int min, int max) _waveGroupSize;
	
	protected StartingBotConfig startingBotConfig;
	
	protected BotConfigService ConfigService { get; private set; }
	protected ManualLogSource Logger { get; private set; }
	protected MapBotWaves MapBotWaves { get; private set; }
	
	protected abstract string GroupChance { get; }
	protected abstract ReadOnlyCollection<BotDifficulty> BotDifficulties { get; }
	
	public Queue<PrepBotInfo> StartingBotsCache { get; } = new(INITIAL_BOT_CACHE_SIZE);
	public ZoneSpawnPoints ZoneSpawnPoints { get; private set; } = [];
	public int MaxBotLimit { get; private set; }
	
	public abstract DonutsSpawnType SpawnType { get; }
	public abstract StartingBotConfig StartingBotConfig { get; }
	
	public static async UniTask<TBotDataService> Create<TBotDataService>(
		[NotNull] BotConfigService configService,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
		where TBotDataService : BotDataService, new()
	{
		var service = new TBotDataService();
		service.Initialize(configService, logger, cancellationToken);
		Dictionary<DonutsSpawnType, IBotDataService> botDataServices =
			MonoBehaviourSingleton<DonutsRaidManager>.Instance.BotDataServices;
		
		if (!botDataServices.ContainsKey(service.SpawnType))
		{
			botDataServices.Add(service.SpawnType, service);
		}
#if DEBUG
		else
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Critical error initializing {0}: SpawnType {1} is already in the BotDataServices dictionary.",
				service.GetType().Name, service.SpawnType.ToString());
			logger.LogDebugDetailed(sb.ToString(), nameof(BotDataService), nameof(Create));
		}
#endif
		
		await service.SetupInitialBotCache();
		return service;
	}
	
	private void Initialize(
		[NotNull] BotConfigService configService,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
	{
		ConfigService = configService;
		Logger = logger;
		_onDestroyToken = cancellationToken;
		_eftBotSpawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
		_botCreator = (IBotCreator)ReflectionHelper.BotSpawner_botCreator_Field.GetValue(_eftBotSpawner);
		
		_resetReplenishTimerBinding = new EventBinding<ResetReplenishTimerEvent>(ResetReplenishTimer);
		EventBus<ResetReplenishTimerEvent>.Register(_resetReplenishTimerBinding);

		_updateWaveTimerBinding = new EventBinding<UpdateWaveTimerEvent>(UpdateBotWaveTimers);
		EventBus<UpdateWaveTimerEvent>.Register(_updateWaveTimerBinding);
		
		string location = ConfigService.GetMapLocation();
		ZoneSpawnPoints = ConfigService.GetAllMapsZoneConfigs()!.Maps[location].Zones;
		MaxBotLimit = ConfigService.GetMaxBotLimit(SpawnType);
		
		if (!ConfigService.CheckForAnyScenarioPatterns())
		{
			return;
		}
		
		MapBotWaves = ConfigService.GetAllMapsBotWavesConfigs()?.Maps[location];
		if (MapBotWaves == null)
		{
			Logger.NotifyLogError("Donuts: Failed to load bot waves. Donuts will not function properly.");
			return;
		}
		
		_botWaves = GetBotWaves();
		if (_botWaves.Count == 0)
		{
			Logger.NotifyLogError("Donuts: No bot waves found in the config. Donuts will not function properly.");
			return;
		}
		
		_botWavesByGroupNum = _botWaves.ToLookup(wave => wave.GroupNum);
		_waveGroupSize = GetWaveMinMaxGroupSize();
	}

	private void ResetReplenishTimer()
	{
		_replenishBotCachePrevTime = Time.time;
	}
	
	protected abstract List<BotWave> GetBotWaves();
	
	private async UniTask SetupInitialBotCache()
	{
		try
		{
			StartingBotConfig startingBotCfg = StartingBotConfig;
			int maxBots = BotHelper.GetRandomBotCap(startingBotCfg.MinCount, startingBotCfg.MaxCount, MaxBotLimit);
#if DEBUG
			using (Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder())
			{
				sb.AppendFormat("Max starting bots set to {0}", maxBots.ToString());
				Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(SetupInitialBotCache));
			}
#endif
			while (_totalBots < maxBots && !_onDestroyToken.IsCancellationRequested)
			{
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, startingBotCfg.MinGroupSize, startingBotCfg.MaxGroupSize,
					maxBots - _totalBots);
				
				(bool success, PrepBotInfo prepBotInfo) = await TryCreateBotData(BotDifficulties.PickRandomElement(), groupSize);
				if (_onDestroyToken.IsCancellationRequested) return;
				
				if (success)
				{
					StartingBotsCache.Enqueue(prepBotInfo);
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(GetType().Name, nameof(SetupInitialBotCache), ex);
		}
		catch (OperationCanceledException) {}
	}
	
	private (int min, int max) GetWaveMinMaxGroupSize()
	{
		var minGroupSize = 0;
		var maxGroupSize = int.MaxValue;
		foreach (BotWave wave in _botWaves)
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
		int groupSize)
	{
		try
		{
#if DEBUG
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			string typeName = GetType().Name;
			const string methodName = nameof(TryCreateBotData);
#endif
			if (_onDestroyToken.IsCancellationRequested)
			{
				return (false, null);
			}
			
			WildSpawnType spawnType = GetWildSpawnType();
			EPlayerSide side = GetPlayerSide(spawnType);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Creating bot: Type={0}, Difficulty={1}, Side={2}, GroupSize={3}",
				spawnType.ToString(), difficulty.ToString(), side.ToString(), groupSize.ToString());
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			var botProfileData = new BotProfileData(side, spawnType, difficulty, 0f);
			var botCreationData = await BotCreationDataClass
				.Create(botProfileData, _botCreator, groupSize, _eftBotSpawner);
			
			if (botCreationData?.Profiles == null || botCreationData.Profiles.Count == 0)
			{
				return (false, null);
			}

			var prepBotInfo = new PrepBotInfo(botCreationData, difficulty);
			_botCache.Enqueue(prepBotInfo.groupDifficultyKey, prepBotInfo);
			_totalBots += groupSize;
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Bot created and assigned successfully; {0} profiles loaded. IDs: {1}",
				botCreationData.Profiles.Count.ToString(), string.Join(", ", botCreationData.Profiles.Select(p => p.Id)));
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return (true, prepBotInfo);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(GetType().Name, nameof(TryCreateBotData), ex);
		}
		catch (OperationCanceledException) {}
		
		return (false, null);
	}
	
	protected abstract WildSpawnType GetWildSpawnType();
	protected abstract EPlayerSide GetPlayerSide(WildSpawnType spawnType);
	
	public async UniTask ReplenishBotCache()
	{
		try
		{
#if DEBUG
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			string typeName = GetType().Name;
			const string methodname = nameof(ReplenishBotCache);
#endif
			if (Time.time < _replenishBotCachePrevTime + DefaultPluginVars.replenishInterval.Value)
			{
				return;
			}
			
			var generatedCount = 0;
			while (generatedCount < NUMBER_OF_GROUPS_TO_REPLENISH &&
				_totalBots <= MaxBotLimit &&
				!_onDestroyToken.IsCancellationRequested)
			{
				BotDifficulty difficulty = BotDifficulties.PickRandomElement();
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, _waveGroupSize.min, _waveGroupSize.max);
				
				(bool success, PrepBotInfo _) = await TryCreateBotData(difficulty, groupSize);
				if (_onDestroyToken.IsCancellationRequested) return;
				if (!success) continue;
				
				generatedCount++;
				await UniTask.Delay(FRAME_DELAY_BETWEEN_REPLENISH, cancellationToken: _onDestroyToken);
#if DEBUG
				prepBotInfo.botCreationData._profileData.TryGetRole(out WildSpawnType role, out _);
				sb.Clear();
				sb.AppendFormat("Replenishing group bot: {0} {1} {2} Count: {3}.", role.ToString(),
					prepBotInfo.difficulty.ToString(), prepBotInfo.botCreationData.Side.ToString(),
					prepBotInfo.groupSize.ToString());
				Logger.LogDebugDetailed(sb.ToString(), typeName, methodname);
#endif
			}
			
			ResetReplenishTimer();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(GetType().Name, nameof(ReplenishBotCache), ex);
		}
		catch (OperationCanceledException) {}
	}
	
	public PrepBotInfo FindCachedBotData(BotDifficulty difficulty, int groupSize)
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(FindCachedBotData);
#endif
		// Find PrepBotInfo that matches the difficulty and group size
		if (_botCache.TryPeek(new PrepBotInfo.GroupDifficultyKey(difficulty, groupSize), out PrepBotInfo prepBotInfo) &&
			prepBotInfo.botCreationData?.Profiles?.Count == groupSize)
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Found cached bots for difficulty {0}, and target count {1}.", difficulty.ToString(),
				groupSize.ToString());
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return prepBotInfo;
		}
		
#if DEBUG
		sb.Clear();
		sb.AppendFormat("No cached bots found for difficulty {0}, and target count {1}.", difficulty.ToString(),
			groupSize.ToString());
		Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
		return null;
	}
	
	public void RemoveFromBotCache(PrepBotInfo.GroupDifficultyKey key)
	{
		if (!_botCache.TryDequeue(key, out PrepBotInfo prepBotInfo))
		{
#if DEBUG
			Logger.LogDebugDetailed(
				"Failure trying to dequeue PrepBotInfo from bot cache.\nAre you sure you're calling this method in the right place?",
				GetType().Name, nameof(RemoveFromBotCache));
#endif
			return;
		}
		
		_totalBots -= prepBotInfo!.groupSize;
	}
	
	/// <summary>
	/// Gets a queue of bot waves which meet the time requirement to spawn.
	/// </summary>
	public Queue<BotWave> GetBotWavesToSpawn()
	{
		Queue<BotWave> wavesToSpawn = new(_botWaves.Count);
		
		foreach (BotWave wave in _botWaves.ShuffleElements(createNewList: true))
		{
			if (wave.ShouldSpawn())
			{
				wavesToSpawn.Enqueue(wave);
			}
		}
		
		return wavesToSpawn;
	}
	
	/// <summary>
	/// Updates all bot wave timers, incrementing by the delta time.
	/// </summary>
	private void UpdateBotWaveTimers(UpdateWaveTimerEvent eventData)
	{
		float cooldownDuration = DefaultPluginVars.coolDownTimer.Value;
		foreach (BotWave wave in _botWaves)
		{
			wave.UpdateTimer(eventData.deltaTime, cooldownDuration);
		}
	}
	
	/// <summary>
	/// Resets timers for every wave sharing the same group number.
	/// </summary>
	public void ResetGroupTimers(int groupNum)
	{
		foreach (BotWave wave in _botWavesByGroupNum[groupNum])
		{
			wave.ResetTimer();
		}
	}
	
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