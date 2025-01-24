using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Bots.SpawnCheckProcessor;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using SPT.SinglePlayer.Utils.InRaid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using Systems.Effects;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public interface IBotSpawnService
{
	void FrameUpdate(float deltaTime);
	UniTask SpawnStartingBots();
	Queue<BotWave> GetBotWavesToSpawn();
	UniTask<bool> TrySpawnBotWave(BotWave wave);
	void DespawnExcessBots();
	void RestartPlayerHitTimer();
}

public abstract class BotSpawnService : IBotSpawnService
{
	private ReadOnlyCollection<Player> _allAlivePlayersReadOnly;
	private IBotCreator _botCreator;
	private BotsController _botsController;
	
	private BotSpawner _eftBotSpawner;
	private GameWorld _gameWorld;
	private string _mapLocation;
	private CancellationToken _onDestroyToken;
	
	private SpawnCheckProcessorBase _spawnCheckProcessor;
	
	// Spawn caps
	private int _currentRespawnCount;
	
	// Despawning
	private float _despawnCooldownTime;
	
	// Combat state
	private float _timeSinceLastHit;

	private readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(100);
	
	protected BotConfigService ConfigService { get; private set; }
	protected IBotDataService DataService { get; private set; }
	protected ManualLogSource Logger { get; private set; }
	protected MapBotWaves MapBotWaves { get; private set; }
	
	public void FrameUpdate(float deltaTime)
	{
		_timeSinceLastHit += deltaTime;
		UpdateBotWaveTimers(deltaTime);
	}
	
	public void RestartPlayerHitTimer()
	{
		_timeSinceLastHit = 0;
	}
	
	public async UniTask SpawnStartingBots()
	{
		List<PrepBotInfo> startingBotsCache = DataService.StartingBotsCache;
		ZoneSpawnPoints zoneSpawnPoints = DataService.ZoneSpawnPoints;
		for (int i = startingBotsCache.Count - 1; i >= 0; i--)
		{
			PrepBotInfo botSpawnInfo = startingBotsCache[i];
			if (_onDestroyToken.IsCancellationRequested) return;
			
			await StartingBotsSpawnPointsCheck(zoneSpawnPoints, botSpawnInfo);
			// Wait until next frame between spawns to reduce the chances of stalling the game
			if (i > 0)
			{
				await UniTask.Yield(cancellationToken: _onDestroyToken);
			}
		}
	}
	
	[NotNull]
	public Queue<BotWave> GetBotWavesToSpawn()
	{
		ReadOnlyCollection<BotWave> botWaves = GetBotWaves();
		Queue<BotWave> wavesToSpawn = new(botWaves.Count);
		
		foreach (BotWave wave in botWaves)
		{
			if (wave.ShouldSpawn())
			{
				wavesToSpawn.Enqueue(wave);
			}
		}
		
		return wavesToSpawn;
	}
	
	public async UniTask<bool> TrySpawnBotWave(BotWave wave)
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(TrySpawnBotWave);
#endif
		
		if (IsHumanPlayerInCombat())
		{
			ResetGroupTimers(wave.GroupNum);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(),
				"A Human player is in combat state");
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return false;
		}
		
		bool anySpawned = IsSpawnChanceSuccessful(wave.SpawnChance) && await TryProcessBotWave(wave);
		
		ResetGroupTimers(wave.GroupNum);
#if DEBUG
		sb.Clear();
		sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(), "Bot wave spawn triggered");
		Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
		return anySpawned;
	}
	
	public void DespawnExcessBots()
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(DespawnExcessBots);
		var spawnTypeName = DataService.SpawnType.ToString();
#endif
		
		if (!IsDespawnBotEnabled())
		{
			return;
		}
		
		float timeSinceLastDespawn = Time.time - _despawnCooldownTime;
		bool hasReachedTimeToDespawn = timeSinceLastDespawn >= DefaultPluginVars.despawnInterval.Value;
		if (!IsOverBotLimit(out int excessBots) || !hasReachedTimeToDespawn)
		{
			return;
		}
		
		if (excessBots <= 0)
		{
#if DEBUG
			Logger.LogDebugDetailed($"{nameof(excessBots)} should be greater than zero! Verify if statements are correct!",
				nameof(BotSpawnService), methodName);
#endif
			return;
		}
		
		for (var i = 0; i < excessBots; i++)
		{
			Player furthestBot = FindFurthestBot();
			if (furthestBot == null)
			{
#if DEBUG
				sb.Clear();
				sb.AppendFormat("No {0} bot found to despawn. Aborting despawn logic!", spawnTypeName);
				Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
				return;
			}
			
			TryDespawnBot(furthestBot);
		}
	}
	
	public static TBotSpawnService Create<TBotSpawnService>(
		[NotNull] BotConfigService configService,
		[NotNull] IBotDataService dataService,
		[NotNull] BotSpawner eftBotSpawner,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken = default)
	where TBotSpawnService : BotSpawnService, new()
	{
		var service = new TBotSpawnService();
		service.Initialize(configService, dataService, eftBotSpawner, logger, cancellationToken);
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.BotSpawnServices.Add(dataService.SpawnType, service);
		return service;
	}
	
	private void Initialize(
		[NotNull] BotConfigService configService,
		[NotNull] IBotDataService dataService,
		[NotNull] BotSpawner eftBotSpawner,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
	{
		ConfigService = configService;
		DataService = dataService;
		_eftBotSpawner = eftBotSpawner;
		Logger = logger;
		_onDestroyToken = cancellationToken;
		
		_gameWorld = Singleton<GameWorld>.Instance;
		_allAlivePlayersReadOnly = _gameWorld.AllAlivePlayersList.AsReadOnly();
		_mapLocation = ConfigService.GetMapLocation();
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_botCreator = (IBotCreator)ReflectionHelper.BotSpawner_botCreator_Field.GetValue(_eftBotSpawner);
		
		if (!ConfigService.CheckForAnyScenarioPatterns())
		{
			return;
		}
		
		MapBotWaves = ConfigService.GetAllMapsBotWavesConfigs()?.Maps[_mapLocation];
		if (MapBotWaves == null)
		{
			Logger.NotifyLogError("Donuts: Failed to load bot waves. Donuts will not function properly.");
			return;
		}
		
		_spawnCheckProcessor = new PlayerVicinitySpawnCheckProcessor();
		_spawnCheckProcessor.SetNext(new BotVicinitySpawnCheckProcessor())
			.SetNext(new PlayerLineOfSightSpawnCheckProcessor())
			.SetNext(new WallSpawnCheckProcessor())
			.SetNext(new GroundSpawnCheckProcessor());
	}
	
	protected abstract bool IsHardStopEnabled();
	protected abstract int GetHardStopTime();
	
	// TODO: why is this method failing?
	// This is an old comment, method has been changed so it needs testing
	private bool HasReachedHardStopTime()
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(HasReachedHardStopTime);
#endif
		if (!IsHardStopEnabled())
		{
			return false;
		}
		
		if (DefaultPluginVars.useTimeBasedHardStop.Value)
		{
			float raidTimeLeftTime = RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
			int hardStopTime = GetHardStopTime();
#if DEBUG
			sb.Clear();
			sb.AppendFormat("RaidTimeLeftTime: {0}, HardStopTime: {1}",
				raidTimeLeftTime.ToString(CultureInfo.InvariantCulture), hardStopTime.ToString());
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return raidTimeLeftTime <= hardStopTime;
		}
		
		float raidTimeLeftPercent = RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f; // Percent left
		int hardStopPercent = GetHardStopTime();
#if DEBUG
		sb.Clear();
		sb.AppendFormat("RaidTimeLeftPercent: {0}, HardStopPercent: {1}",
			raidTimeLeftPercent.ToString(CultureInfo.InvariantCulture), hardStopPercent.ToString());
		Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
		return raidTimeLeftPercent <= hardStopPercent;
	}
	
	protected abstract ReadOnlyCollection<BotWave> GetBotWaves();
	
	private void UpdateBotWaveTimers(float deltaTime)
	{
		float cooldownDuration = DefaultPluginVars.coolDownTimer.Value;
		foreach (BotWave wave in GetBotWaves())
		{
			wave.UpdateTimer(deltaTime, cooldownDuration);
		}
	}
	
	/// <summary>
	/// Resets timers for every wave sharing the same group number.
	/// </summary>
	private void ResetGroupTimers(int groupNum)
	{
		foreach (BotWave wave in GetBotWaves())
		{
			if (wave.GroupNum == groupNum)
			{
				wave.ResetTimer();
			}
		}
	}
	
	[CanBeNull]
	private AICorePoint GetClosestCorePoint(Vector3 position) =>
		_botsController.CoversData.GetClosest(position)?.CorePointInGame;
	
	private void ActivateBotAtPosition([NotNull] BotCreationDataClass botData, Vector3 spawnPosition)
	{
		// Must add to _inSpawnProcess so the bot count managed by BotSpawner is correct
		// Normally BotSpawner::method_7() handles this but we skip it to directly call GClass888::ActivateBot()
		var currentInSpawnProcess = (int)ReflectionHelper.BotSpawner_inSpawnProcess_Field.GetValue(_eftBotSpawner);
		int newInSpawnProcess = currentInSpawnProcess + botData.Count;
		ReflectionHelper.BotSpawner_inSpawnProcess_Field.SetValue(_eftBotSpawner, newInSpawnProcess);
		
		// Add spawn point to the BotCreationDataClass
		BotZone closestBotZone = _eftBotSpawner.GetClosestZone(spawnPosition, out _);
		AICorePoint closestCorePoint = GetClosestCorePoint(spawnPosition);
		botData.AddPosition(spawnPosition, closestCorePoint!.Id);
		
		// Set SpawnParams so the bots are grouped correctly
		bool isGroup = botData.Count > 1;
		bool isBossGroup = botData._profileData.TryGetRole(out WildSpawnType role, out _) && role.IsBoss();
		int groupCount = botData.Count;
		var newSpawnParams = new BotSpawnParams
		{
			ShallBeGroup = new ShallBeGroupParams(isGroup, isBossGroup, groupCount),
			TriggerType = SpawnTriggerType.none
		};
		botData._profileData.SpawnParams = newSpawnParams;
		
		var activateBotCallbackWrapper = new ActivateBotCallbackWrapper(_eftBotSpawner, botData);
		var groupAction = new Func<BotOwner, BotZone, BotsGroup>(activateBotCallbackWrapper.GetGroupAndSetEnemies);
		var callback = new Action<BotOwner>(activateBotCallbackWrapper.CreateBotCallback);
		
		// shallBeGroup doesn't matter at this stage, it only matters in the callback action
		_botCreator.ActivateBot(botData, closestBotZone, false, groupAction, callback, _onDestroyToken);
		DataService.ScheduleForClearBotData(botData);
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.UpdateReplenishBotDataTime();
	}
	
	private async UniTask StartingBotsSpawnPointsCheck(
		[NotNull] ZoneSpawnPoints zoneSpawnPoints,
		[NotNull] PrepBotInfo botSpawnInfo)
	{
		// Iterate through unused zone spawn points
		var hasSpawned = false;
		while (!hasSpawned)
		{
			Vector3? spawnPoint = zoneSpawnPoints.GetUnusedStartingSpawnPoint(out int index);
			if (!spawnPoint.HasValue) return;
			
			Vector3? positionOnNavMesh = await GetValidSpawnPosition(spawnPoint.Value, _spawnCheckProcessor);
			if (_onDestroyToken.IsCancellationRequested) return;
			if (!positionOnNavMesh.HasValue)
			{
				await UniTask.Delay(_retryInterval, cancellationToken: _onDestroyToken);
				continue;
			}
			
			Vector3 actualSpawnPoint = positionOnNavMesh.Value;
			ActivateBotAtPosition(botSpawnInfo.Bots, actualSpawnPoint);
			zoneSpawnPoints.SetStartingSpawnPointAsUsed(index);
			hasSpawned = true;
		}
#if DEBUG
		Logger.LogDebugDetailed("Failed to spawn some starting bots!", GetType().Name, nameof(StartingBotsSpawnPointsCheck));
#endif
	}
	
	protected abstract bool IsCorrectSpawnType(WildSpawnType role);
	
	/// <summary>
	/// Finds the furthest bot away from all human players
	/// </summary>
	[CanBeNull]
	private Player FindFurthestBot()
	{
		var furthestSqrMagnitude = float.MinValue;
		Player furthestBot = null;
		ReadOnlyCollection<Player> allAlivePlayers = _allAlivePlayersReadOnly;
		ReadOnlyCollection<Player> humanPlayers = ConfigService.GetHumanPlayerList();
		// Iterate through alive players
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			// Ignore players that aren't bots or aren't the correct spawn type
			if (!player.IsAI ||
				player.AIData?.BotOwner == null ||
				!IsCorrectSpawnType(player.Profile.Info.Settings.Role))
			{
				continue;
			}
			
			// Iterate through all human players
			for (int j = humanPlayers.Count - 1; j >= 0; j--)
			{
				Player humanPlayer = humanPlayers[j];
				// Ignore dead human players
				if (humanPlayer == null || humanPlayer.HealthController == null || !humanPlayer.HealthController.IsAlive)
				{
					continue;
				}
				
				// Get distance of bot to human player using squared distance
				float sqrMagnitude = (humanPlayer.Transform.position - player.Transform.position).sqrMagnitude;
				
				// Check if this is the furthest distance
				if (sqrMagnitude > furthestSqrMagnitude)
				{
					furthestSqrMagnitude = sqrMagnitude;
					furthestBot = player;
				}
			}
		}
		
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		if (furthestBot == null)
		{
			sb.Append("Furthest bot is null. No bots found in the list.");
		}
		else
		{
			sb.AppendFormat("Furthest bot found: {0} at distance {1}", furthestBot.Profile.Info.Nickname,
				Mathf.Sqrt(furthestSqrMagnitude).ToString(CultureInfo.InvariantCulture));
		}
		Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(FindFurthestBot));
#endif
		
		return furthestBot;
	}
	
	private bool IsOverBotLimit(out int excess)
	{
		var isOverBotLimit = false;
		int aliveBots = GetAliveBotsCount();
		int botLimit = DataService.MaxBotLimit;
		if (aliveBots > botLimit)
		{
			excess = aliveBots - botLimit;
			isOverBotLimit = true;
		}
		else
		{
			excess = 0;
		}
		return isOverBotLimit;
	}
	
	protected abstract bool IsDespawnBotEnabled();
	
	private bool TryDespawnBot([NotNull] Player furthestBot)
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(TryDespawnBot);
#endif
		BotOwner botOwner = furthestBot.AIData?.BotOwner;
		if (botOwner == null)
		{
#if DEBUG
			Logger.LogDebugDetailed("Attempted to despawn a null bot.", typeName, methodName);
#endif
			return false;
		}
		
#if DEBUG
		sb.Clear();
		sb.AppendFormat("Despawning bot: {0} ({1})", furthestBot.Profile.Info.Nickname, furthestBot.name);
		Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
		
		Player botPlayer = botOwner.GetPlayer;
        _gameWorld.RegisteredPlayers.Remove(botOwner);
        _gameWorld.AllAlivePlayersList.Remove(botPlayer);
		Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botPlayer);
		// BSG calls this to despawn; this calls the BotOwner::Deactivate(), BotOwner::Dispose() and IBotGame::BotDespawn() methods
        botOwner.LeaveData.RemoveFromMap();
        
		// Update the cooldown
		_despawnCooldownTime = Time.time;
		return true;
	}
	
	private bool IsHumanPlayerInCombat()
	{
		return _timeSinceLastHit < DefaultPluginVars.battleStateCoolDown.Value;
	}
	
	private bool IsSpawnChanceSuccessful(int spawnChance)
    {
    	int randomValue = Random.Range(0, 100);
    	bool canSpawn = randomValue < spawnChance;
#if DEBUG
	    using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
	    sb.AppendFormat("SpawnChance: {0}, RandomValue: {1}, CanSpawn: {2}", spawnChance.ToString(),
			randomValue.ToString(), canSpawn.ToString());
	    Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(IsSpawnChanceSuccessful));
#endif
    	return canSpawn;
    }
	
	private async UniTask<bool> TryProcessBotWave(BotWave wave)
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(TryProcessBotWave);
#endif
		ZoneSpawnPoints zoneSpawnPoints = DataService.ZoneSpawnPoints;
		if (zoneSpawnPoints.Count == 0)
		{
			ResetGroupTimers(wave.GroupNum);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(),
				"No zone spawn points found");
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return false;
		}
		
		List<string> waveZones = wave.Zones;
		
		if (waveZones.Count == 0)
		{
			ResetGroupTimers(wave.GroupNum);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(),
				"No zones specified in bot wave");
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return false;
		}
		
		// Only check for keyword zones if there is only a single zone defined in the BotWave
		if (waveZones.Count == 1)
		{
			if (ZoneSpawnPoints.IsKeywordZone(waveZones[0], out string keywordZoneName))
			{
				AdjustSpawnChanceIfHotspot(wave, keywordZoneName);
				return await TrySpawnBotIfValidZone(keywordZoneName!, wave, zoneSpawnPoints);
			}
			
			return await TrySpawnBotIfValidZone(waveZones[0], wave, zoneSpawnPoints);
		}
		
		// Iterate through shuffled wave zones, adjust for hotspot zones and attempt spawning
		foreach (string zoneName in waveZones.ShuffleElements(true))
		{
			AdjustSpawnChanceIfHotspot(wave, zoneName);
			
			if (await TrySpawnBotIfValidZone(zoneName, wave, zoneSpawnPoints))
			{
				return true;
			}
		}
		
		return false;
	}

	private async UniTask<bool> TrySpawnBotIfValidZone(
		[NotNull] string zoneName,
		[NotNull] BotWave wave,
		[NotNull] ZoneSpawnPoints zoneSpawnPoints)
	{
		if (!zoneSpawnPoints.TryGetValue(zoneName, out List<Vector3> spawnPoints))
		{
			return false;
		}
		
		foreach (Vector3 spawnPoint in spawnPoints.ShuffleElements(createNewList: true))
		{
			if (IsHumanPlayerWithinTriggerDistance(wave.TriggerDistance, spawnPoint) &&
				await TrySpawnBot(wave, spawnPoint))
			{
#if DEBUG
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Spawning bot wave for GroupNum {0} at {1}, {2}", wave.GroupNum, zoneName,
					spawnPoint.ToString());
				Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotIfValidZone));
#endif
				return true;
			}
		}
		
		return false;
	}
	
	protected abstract bool IsHotspotBoostEnabled();
	
	/// <summary>
	/// Sets the bot wave's spawn chance to 100% if the zone is a hotspot.
	/// </summary>
	private void AdjustSpawnChanceIfHotspot([NotNull] BotWave wave, string zoneName)
	{
		if (!IsHotspotBoostEnabled() || !ZoneSpawnPoints.IsHotspotZone(zoneName, out _))
		{
			return;
		}
		
#if DEBUG
		using (Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("{0} is a hotspot; hotspot boost is enabled, setting spawn chance to 100", zoneName);
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustSpawnChanceIfHotspot));
		}
#endif
		wave.SpawnChance = 100;
	}
	
	private bool IsHumanPlayerWithinTriggerDistance(int triggerDistance, Vector3 position)
	{
		int triggerSqrMagnitude = triggerDistance * triggerDistance;
		
		ReadOnlyCollection<Player> humanPlayerList = ConfigService.GetHumanPlayerList();
		for (int i = humanPlayerList.Count - 1; i >= 0; i--)
		{
			Player player = humanPlayerList[i];
			if (player == null || player.HealthController == null || player.HealthController.IsAlive == false)
			{
				continue;
			}
			
			float sqrMagnitude = (((IPlayer)player).Position - position).sqrMagnitude;
			if (sqrMagnitude <= triggerSqrMagnitude)
			{
				return true;
			}
		}
		return false;
	}
	
	protected abstract bool HasReachedHardCap();
	
	/// <summary>
	/// Checks certain spawn options, reset groups timers.
	/// </summary>
	private async UniTask<bool> TrySpawnBot([NotNull] BotWave wave, Vector3 spawnPoint)
	{
		if ((DefaultPluginVars.HardCapEnabled.Value && HasReachedHardCap()) || HasReachedHardStopTime())
		{
			return false;
		}
		
		int groupSize = DetermineBotGroupSize(wave.MinGroupSize, wave.MaxGroupSize);
		if (groupSize < 1)
		{
			return false;
		}
		
		if (await SpawnBot(groupSize, spawnPoint))
		{
			wave.SpawnTriggered();
		}
		
		return !_onDestroyToken.IsCancellationRequested;
	}
	
	private async UniTask<bool> SpawnBot(
		int groupSize,
		Vector3 spawnPoint,
		bool generateNew = true,
		SpawnCheckProcessorBase spawnCheckProcessor = null)
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(SpawnBot);
#endif
		bool isGroup = groupSize > 1;
		BotDifficulty botDifficulty = DataService.GetBotDifficulty();
		BotCreationDataClass cachedBotData = DataService.FindCachedBotData(botDifficulty, groupSize);
		if (generateNew && cachedBotData == null)
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("No cached bots found for this spawn, generating on the fly for {0} bots - this may take some time.",
				groupSize.ToString());
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			var botInfo = new PrepBotInfo(botDifficulty, isGroup, groupSize);
			(bool success, cachedBotData) = await DataService.TryCreateBotData(botInfo);
			if (_onDestroyToken.IsCancellationRequested || !success)
			{
				return false;
			}
		}
		
		if (cachedBotData == null)
		{
			return false;
		}
		
#if DEBUG
		Logger.LogDebugDetailed("Found grouped cached bots, spawning them.", typeName, methodName);
#endif
		Vector3? spawnPosition = await GetValidSpawnPosition(spawnPoint, spawnCheckProcessor);
		if (_onDestroyToken.IsCancellationRequested) return false;
		
		if (spawnPosition.HasValue)
		{
			ActivateBotAtPosition(cachedBotData, spawnPosition.Value);
			return true;
		}
		
#if DEBUG
		Logger.LogDebugDetailed("No valid spawn position found after retries - skipping this spawn", typeName, methodName);
#endif
		return false;
	}
	
	private async UniTask<Vector3?> GetValidSpawnPosition(
		Vector3 position,
		[CanBeNull] SpawnCheckProcessorBase spawnCheckProcessor)
	{
		int maxSpawnAttempts = DefaultPluginVars.maxSpawnTriesPerBot.Value;
		for (var i = 0; i < maxSpawnAttempts; i++)
		{
			if (_onDestroyToken.IsCancellationRequested)
			{
				return null;
			}
			
			if (!NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
			{
				await UniTask.Delay(_retryInterval, cancellationToken: _onDestroyToken);
				continue;
			}
			
			if (spawnCheckProcessor != null)
			{
				var spawnCheckData = new SpawnCheckData(navHit.position, _mapLocation, _allAlivePlayersReadOnly);
				spawnCheckProcessor.Process(spawnCheckData);
				if (!spawnCheckData.Success)
				{
					await UniTask.Delay(_retryInterval, cancellationToken: _onDestroyToken);
					continue;
				}
			}
			
			Vector3 spawnPosition = navHit.position;
#if DEBUG
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Found spawn position at: {0}", spawnPosition.ToString());
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(GetValidSpawnPosition));
#endif
			return spawnPosition;
		}
		
		return null;
	}
	
	protected abstract int GetBotGroupSize(int minGroupSize, int maxGroupSize);
	
	private int DetermineBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		int groupSize = GetBotGroupSize(minGroupSize, maxGroupSize);
		if (groupSize < 1)
		{
			return -1;
		}
		
		// Check if hard cap is enabled and adjust maxCount based on active bot counts and limits
		if (DefaultPluginVars.HardCapEnabled.Value)
		{
			groupSize = AdjustGroupSizeForHardCap(groupSize);
			if (groupSize < 1)
			{
				return -1;
			}
		}
		
		// Check respawn limits and adjust accordingly
		groupSize = AdjustMaxCountForRespawnLimits(groupSize);
		if (groupSize < 1)
		{
			return -1;
		}
		
		return groupSize;
	}
	
	protected abstract int GetAliveBotsCount();
	
	private int AdjustGroupSizeForHardCap(int groupSize)
	{
		int activeBots = GetAliveBotsCount();
		int botLimit = DataService.MaxBotLimit;
		if (activeBots < botLimit && activeBots + groupSize > botLimit)
		{
			groupSize = botLimit - activeBots;
		}
		else
		{
			groupSize = -1;
		}
		return groupSize;
	}
	
	protected abstract int GetMaxBotRespawns();
	
	private int AdjustMaxCountForRespawnLimits(int groupSize)
	{
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(AdjustMaxCountForRespawnLimits);
		var spawnTypeName = DataService.SpawnType.ToString();
#endif
		int maxBotRespawns = GetMaxBotRespawns();
		// Don't cap if maxRespawns is set to zero or less
		if (maxBotRespawns <= 0)
		{
			_currentRespawnCount += groupSize;
			return groupSize;
		}
		
		if (_currentRespawnCount >= maxBotRespawns)
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Max {0} respawns reached, skipping this spawn", spawnTypeName);
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return -1;
		}
		
		if (_currentRespawnCount + groupSize >= maxBotRespawns)
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Max {0} respawn limit reached: {1}. Current {2} respawns this raid: {3}",
				spawnTypeName, DefaultPluginVars.maxRespawnsPMC.Value.ToString(),
				(_currentRespawnCount + groupSize).ToString());
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			groupSize = maxBotRespawns - _currentRespawnCount;
		}
		
		_currentRespawnCount += groupSize;
		return groupSize;
	}
}