﻿using BepInEx.Logging;
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
	UniTask SpawnStartingBots();
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

	private readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(500);
	
	protected BotConfigService ConfigService { get; private set; }
	protected IBotDataService DataService { get; private set; }
	protected ManualLogSource Logger { get; private set; }
	
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
		
		_spawnCheckProcessor = new EntitySpawnCheckProcessor(_mapLocation, _allAlivePlayersReadOnly);
		_spawnCheckProcessor.SetNext(new WallSpawnCheckProcessor())
			.SetNext(new GroundSpawnCheckProcessor());
	}
	
	public void RestartPlayerHitTimer()
	{
		_timeSinceLastHit = 0;
	}
	
	public async UniTask SpawnStartingBots()
	{
		Queue<PrepBotInfo> startingBotsCache = DataService.StartingBotsCache;
		ZoneSpawnPoints zoneSpawnPoints = DataService.ZoneSpawnPoints;
		List<string> zoneNames = DataService.StartingBotConfig.Zones;

		int count = startingBotsCache.Count;
		while (count > 0 && !_onDestroyToken.IsCancellationRequested)
		{
			PrepBotInfo botSpawnInfo = startingBotsCache.Dequeue();
			count--;
			
			await StartingBotsSpawnPointsCheck(zoneSpawnPoints, botSpawnInfo, zoneNames);
			
			// Wait until next frame between spawns to reduce the chances of stalling the game
			if (count > 0)
			{
				await UniTask.Yield(cancellationToken: _onDestroyToken);
			}
		}
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
			DataService.ResetGroupTimers(wave.GroupNum);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(),
				"A Human player is in combat state");
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return false;
		}
		
		bool anySpawned = IsSpawnChanceSuccessful(wave.SpawnChance) && await TryProcessBotWave(wave);
		
		DataService.ResetGroupTimers(wave.GroupNum);
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

			if (!TryDespawnBot(furthestBot))
			{
				return;
			}
		}
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
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.UpdateReplenishBotDataTime();
	}
	
	private async UniTask StartingBotsSpawnPointsCheck(
		[NotNull] ZoneSpawnPoints zoneSpawnPoints,
		[NotNull] PrepBotInfo botSpawnInfo,
		[NotNull] IList<string> zoneNames)
	{
		// Iterate through unused zone spawn points
		var failsafeCounter = 0;
		while (failsafeCounter < 3)
		{
			if (_onDestroyToken.IsCancellationRequested) return;
			Vector3? spawnPoint = zoneSpawnPoints.GetUnusedStartingSpawnPoint(zoneNames, out string zoneName);
			if (!spawnPoint.HasValue) return;
			
			Vector3? positionOnNavMesh = await GetValidSpawnPosition(spawnPoint.Value, _spawnCheckProcessor);
			if (_onDestroyToken.IsCancellationRequested) return;
			if (!positionOnNavMesh.HasValue)
			{
				failsafeCounter++;
				await UniTask.Delay(_retryInterval, cancellationToken: _onDestroyToken);
				continue;
			}
			
			ActivateBotAtPosition(botSpawnInfo.botCreationData, positionOnNavMesh.Value);
			zoneSpawnPoints.SetStartingSpawnPointAsUsed(zoneName, spawnPoint.Value);
			return;
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
#endif
		ZoneSpawnPoints zoneSpawnPoints = DataService.ZoneSpawnPoints;
		if (zoneSpawnPoints.Count == 0)
		{
			DataService.ResetGroupTimers(wave.GroupNum);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Resetting timer for GroupNum {0}\nReason: {1}", wave.GroupNum.ToString(),
				"Fatal Error: No zone spawn points were loaded. Check your zoneSpawnPoints folder!");
			Logger.NotifyLogError(sb.ToString());
#endif
			return false;
		}
		
		List<string> waveZones = wave.Zones;
		
		if (waveZones.Count == 0)
		{
			DataService.ResetGroupTimers(wave.GroupNum);
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Resetting timer for GroupNum {0}\nReason: {1}", wave.GroupNum.ToString(),
				"Fatal Error: No zones specified in bot wave. Check your scenario wave patterns are set up correctly!");
			Logger.NotifyLogError(sb.ToString());
#endif
			return false;
		}
		
		// Iterate through shuffled wave zones, adjust for keyword zones and attempt spawning
		foreach (string zoneName in waveZones.ShuffleElements(createNewList: true))
		{
			// Instead of loosely matching, we do an exact match so we know if the wave is specifying all hotspots or just a few hotspots
			if (!ZoneSpawnPoints.IsKeywordZone(zoneName, out ZoneSpawnPoints.KeywordZoneType keyword, exactMatch: true))
			{
				bool isHotspot = ZoneSpawnPoints.IsHotspotZone(zoneName, out _);
				if (isHotspot)
				{
					AdjustHotspotSpawnChance(wave, zoneName);
				}
				
				if (await TrySpawnBotIfValidZone(zoneName, wave, zoneSpawnPoints, isHotspot)) return true;
				
				continue;
			}
			
			if (keyword == ZoneSpawnPoints.KeywordZoneType.Hotspot)
			{
				AdjustHotspotSpawnChance(wave, zoneName);
			}

			// If we matched a keyword zone and it still failed to spawn, return false
			// A wave should not contain multiple zones if a keyword is used
			return await TrySpawnBotIfValidZone(keyword, wave, zoneSpawnPoints,
				isHotspot: keyword == ZoneSpawnPoints.KeywordZoneType.Hotspot);
		}
		
		return false;
	}

	private async UniTask<bool> TrySpawnBotIfValidZone(
		[NotNull] string zoneName,
		[NotNull] BotWave wave,
		[NotNull] ZoneSpawnPoints zoneSpawnPoints,
		bool isHotspot)
	{
		if (!zoneSpawnPoints.TryGetValue(zoneName, out List<Vector3> spawnPoints))
		{
			return false;
		}
		
		foreach (Vector3 spawnPoint in spawnPoints.ShuffleElements(createNewList: true))
		{
			if (IsHumanPlayerWithinTriggerDistance(wave.TriggerDistance, spawnPoint) &&
				await TrySpawnBot(wave, spawnPoint, isHotspot))
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
	
	private async UniTask<bool> TrySpawnBotIfValidZone(
		ZoneSpawnPoints.KeywordZoneType keyword,
		[NotNull] BotWave wave,
		[NotNull] ZoneSpawnPoints zoneSpawnPoints,
		bool isHotspot)
	{
		List<KeyValuePair<string, List<Vector3>>> keywordZones = zoneSpawnPoints.GetSpawnPointsFromKeyword(keyword);
		if (keywordZones.Count == 0)
		{
			return false;
		}
		
		KeyValuePair<string, List<Vector3>> pair = keywordZones.PickRandomElement();
		
		foreach (Vector3 spawnPoint in pair.Value.ShuffleElements(createNewList: true))
		{
			if (IsHumanPlayerWithinTriggerDistance(wave.TriggerDistance, spawnPoint) &&
				await TrySpawnBot(wave, spawnPoint, isHotspot))
			{
#if DEBUG
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Spawning bot wave for GroupNum {0} at {1} (Keyword: {2}), {3}", wave.GroupNum, pair.Key,
					keyword.ToString(), spawnPoint.ToString());
				Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotIfValidZone));
#endif
				return true;
			}
		}

		return false;
	}
	
	protected abstract bool IsHotspotBoostEnabled();
	
	/// <summary>
	/// Sets the bot wave's spawn chance to 100% if hotspot boost setting is enabled.
	/// </summary>
	private void AdjustHotspotSpawnChance([NotNull] BotWave wave, [NotNull] string zoneName)
	{
		if (!IsHotspotBoostEnabled())
		{
			return;
		}
		
#if DEBUG
		using (Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("{0} is a hotspot; hotspot boost is enabled, setting spawn chance to 100", zoneName);
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustHotspotSpawnChance));
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
	
	protected abstract bool HasReachedHardCap(bool isHotspot);
	
	/// <summary>
	/// Checks certain spawn options, reset groups timers.
	/// </summary>
	private async UniTask<bool> TrySpawnBot([NotNull] BotWave wave, Vector3 spawnPoint, bool isHotspot)
	{
		if ((DefaultPluginVars.HardCapEnabled.Value && HasReachedHardCap(isHotspot)) || HasReachedHardStopTime())
		{
			return false;
		}
		
		int groupSize = DetermineBotGroupSize(wave.MinGroupSize, wave.MaxGroupSize);
		if (groupSize < 1)
		{
			return false;
		}
		
		if (await SpawnBot(groupSize, spawnPoint, spawnCheckProcessor: _spawnCheckProcessor))
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
		BotDifficulty difficulty = DataService.GetBotDifficulty();
		PrepBotInfo cachedPrepBotInfo = DataService.FindCachedBotData(difficulty, groupSize);
		if (generateNew && cachedPrepBotInfo?.botCreationData == null)
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("No cached bots found for this spawn, generating on the fly for {0} bots - this may take some time.",
				groupSize.ToString());
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			(bool success, cachedPrepBotInfo) = await DataService.TryCreateBotData(difficulty, groupSize);
			if (_onDestroyToken.IsCancellationRequested || !success) return false;
		}
		
		if (cachedPrepBotInfo?.botCreationData == null) return false;
		
#if DEBUG
		Logger.LogDebugDetailed("Found grouped cached bots, spawning them.", typeName, methodName);
#endif
		Vector3? spawnPosition = await GetValidSpawnPosition(spawnPoint, spawnCheckProcessor);
		if (_onDestroyToken.IsCancellationRequested) return false;
		
		if (spawnPosition.HasValue)
		{
			ActivateBotAtPosition(cachedPrepBotInfo.botCreationData, spawnPosition.Value);
			DataService.RemoveFromBotCache(new PrepBotInfo.GroupDifficultyKey(difficulty, groupSize));
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
			
			if (!NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas) ||
				(spawnCheckProcessor != null && !spawnCheckProcessor.Process(navHit.position)))
			{
				await UniTask.Delay(_retryInterval, cancellationToken: _onDestroyToken);
				continue;
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