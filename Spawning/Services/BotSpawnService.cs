﻿using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Spawning.Models;
using Donuts.Spawning.Processors;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using SPT.SinglePlayer.Utils.InRaid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Spawning.Services;

public interface IBotSpawnService : IServiceSpawnType
{
	UniTask<bool> SpawnStartingBots(CancellationToken cancellationToken);
	UniTask<bool> TrySpawnBotWave(CancellationToken cancellationToken);
}

public abstract class BotSpawnService : IBotSpawnService
{
	private readonly IBotCreator _botCreator;
	private readonly BotsController _botsController;
	private readonly BotSpawner _eftBotSpawner;
	
	private readonly WaveSpawnProcessorBase _waveSpawnProcessor;
	
	// Spawn caps
	private int _currentRespawnCount;
	
	private const int FRAME_DELAY_INTERVAL = 30;
	
	protected readonly BotConfigService configService;
	protected readonly IBotDataService dataService;
	protected readonly ManualLogSource logger;
	
	protected SpawnCheckProcessorBase spawnCheckProcessor;
	
	public abstract DonutsSpawnType SpawnType { get; }
	
	protected BotSpawnService(BotConfigService configService, IBotDataService dataService)
	{
		this.configService = configService;
		this.dataService = dataService;
		logger = DonutsRaidManager.Logger;
		
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_eftBotSpawner = _botsController.BotSpawner;
		_botCreator = _eftBotSpawner._botCreator;
		
		_waveSpawnProcessor = new PlayerCombatStateCheck();
		_waveSpawnProcessor.SetNext(new WaveSpawnChanceCheck());
	}
	
	public async UniTask<bool> SpawnStartingBots(CancellationToken cancellationToken)
	{
		Queue<PrepBotInfo> startingBotsCache = dataService.StartingBotsCache;
		
		int count = startingBotsCache.Count;
		if (count == 0)
		{
			return true;
		}
		
		while (count > 0 && !cancellationToken.IsCancellationRequested)
		{
			PrepBotInfo botSpawnInfo = startingBotsCache.Dequeue();
			count--;
			
			bool success = await TrySpawnStartingBot(botSpawnInfo, cancellationToken);
			if (cancellationToken.IsCancellationRequested || botSpawnInfo.botCreationData.SpawnStopped)
			{
				return false;
			}
			
			if (!success)
			{
				startingBotsCache.Enqueue(botSpawnInfo);
				continue;
			}
			
			// Delay between spawns to improve performance
			if (count > 0)
			{
				await UniTask.DelayFrame(FRAME_DELAY_INTERVAL, cancellationToken: cancellationToken);
			}
		}
		
		return startingBotsCache.Count == 0;
	}
	
	public async UniTask<bool> TrySpawnBotWave(CancellationToken cancellationToken)
	{
		BotWave wave = dataService.GetBotWaveToSpawn();
		if (wave == null)
		{
			return false;
		}
		
		int waveGroupNum = wave.GroupNum;
		
		if (!_waveSpawnProcessor.Process(wave))
		{
			dataService.ResetGroupTimers(waveGroupNum);
			return false;
		}
		
		bool anySpawned = await TryProcessBotWave(wave, cancellationToken);
		
		dataService.ResetGroupTimers(waveGroupNum);
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: Bot wave spawn triggered", waveGroupNum.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotWave));
		}
		
		return anySpawned;
	}
	
	protected abstract bool IsHardStopEnabled();
	protected abstract int GetHardStopTime();
	
	private bool HasReachedHardStopTime()
	{
		if (!IsHardStopEnabled())
		{
			return false;
		}
		
		if (DefaultPluginVars.useTimeBasedHardStop.Value)
		{
			float raidTimeLeftTime = RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
			int hardStopTime = GetHardStopTime();
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("RaidTimeLeftTime: {0}, HardStopTime: {1}",
					raidTimeLeftTime.ToString(CultureInfo.InvariantCulture), hardStopTime.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(HasReachedHardStopTime));
			}
			
			return raidTimeLeftTime <= hardStopTime;
		}
		
		float raidTimeLeftPercent = RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f; // Percent left
		int hardStopPercent = GetHardStopTime();
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("RaidTimeLeftPercent: {0}, HardStopPercent: {1}",
				raidTimeLeftPercent.ToString(CultureInfo.InvariantCulture), hardStopPercent.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(HasReachedHardStopTime));
		}
		
		return raidTimeLeftPercent <= hardStopPercent;
	}
	
	/// <summary>
	/// Increment BotSpawner's _inSpawnProcess int field, so the bot count is correct.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Note 1: Normally <c>BotSpawner::method_7()</c> handles this, but we skip it to directly call <c>GClass888::ActivateBot()</c>.
	/// </para>
	/// <para>
	/// Note 2: For whatever reason, BSG increments by the number of spawn points (basically group member count)
	/// before beginning the bot spawn logic, but only decrements by 1 if it fails.
	/// The callback, however, correctly only decrements by 1 for each bot in the group.
	/// </para>
	/// </remarks>
	private void IncrementInProcessCounter(int value)
	{
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		
		int currentInSpawnProcess = _eftBotSpawner._inSpawnProcess;
		if (DefaultPluginVars.debugLogging.Value)
		{
			sb.Clear();
			sb.AppendFormat("BotSpawner._inSpawnProcess current value is {0}", currentInSpawnProcess.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(IncrementInProcessCounter));
		}
		
		int newInSpawnProcess = currentInSpawnProcess + value;
		if (newInSpawnProcess < 0)
		{
			newInSpawnProcess = 0;
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				sb.Clear();
				sb.AppendFormat("Warning! BotSpawner._inSpawnProcess new value is below zero: {0}, clamping to zero!",
					newInSpawnProcess.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(IncrementInProcessCounter));
			}
		}
		
		_eftBotSpawner._inSpawnProcess = newInSpawnProcess;
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			sb.Clear();
			sb.AppendFormat("BotSpawner._inSpawnProcess new value is {0}", newInSpawnProcess.ToString());
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(IncrementInProcessCounter));
		}
	}
	
	[CanBeNull]
	private AICorePoint GetClosestCorePoint(Vector3 position) =>
		_botsController.CoversData.GetClosest(position)?.CorePointInGame;
	
	private void ActivateBotAtPosition(
		[NotNull] BotCreationDataClass botData,
		Vector3 spawnPosition,
		CancellationToken cancellationToken = default)
	{
		int groupCount = botData.Count;
		
		// Add spawn point to the BotCreationDataClass
		BotZone closestBotZone = _eftBotSpawner.GetClosestZone(spawnPosition, out _);
		AICorePoint closestCorePoint = GetClosestCorePoint(spawnPosition);
		botData.AddPosition(spawnPosition, closestCorePoint!.Id);
		
		// Set SpawnParams so the bots are grouped correctly
		bool isGroup = groupCount > 1;
		bool isBossGroup = botData._profileData.TryGetRole(out WildSpawnType role, out _) && role.IsBoss();
		var newSpawnParams = new BotSpawnParams
		{
			ShallBeGroup = new ShallBeGroupParams(isGroup, isBossGroup, groupCount),
			TriggerType = SpawnTriggerType.none
		};
		botData._profileData.SpawnParams = newSpawnParams;
		
		var activateBotCallbackWrapper = new ActivateBotCallbackWrapper(_eftBotSpawner, botData);
		var groupAction = new Func<BotOwner, BotZone, BotsGroup>(activateBotCallbackWrapper.GetGroupAndSetEnemies);
		var callback = new Action<BotOwner>(activateBotCallbackWrapper.CreateBotCallback);
		
		EventBus.Raise(BotSpawnedEvent.Create());
		
		// shallBeGroup doesn't matter at this stage, it only matters in the callback action
		_botCreator.ActivateBot(botData, closestBotZone, false, groupAction, callback, cancellationToken);
	}
	
	private async UniTask<bool> TrySpawnStartingBot(
		[NotNull] PrepBotInfo botSpawnInfo,
		CancellationToken cancellationToken = default)
	{
		// Iterate through unused zone spawn points
		var failsafeCounter = 0;
		const int maxFailsafeAttempts = 3;
		while (failsafeCounter < maxFailsafeAttempts && !cancellationToken.IsCancellationRequested)
		{
			Vector3? simulatedSpawnPoint = dataService.GetUnusedSpawnPoint(SpawnPointType.Starting);
			if (!simulatedSpawnPoint.HasValue)
			{
				logger.LogDebugDetailed("No zone spawn points found, cancelling!", GetType().Name, nameof(TrySpawnStartingBot));
				break;
			}
			
			Vector3? positionOnNavMesh =
				await GetValidSpawnPosition(simulatedSpawnPoint.Value, spawnCheckProcessor, cancellationToken);
			if (cancellationToken.IsCancellationRequested) return false;
			if (!positionOnNavMesh.HasValue)
			{
				failsafeCounter++;
				await UniTask.DelayFrame(FRAME_DELAY_INTERVAL, cancellationToken: cancellationToken);
				continue;
			}
			
			await UniTask.Yield(cancellationToken);
			if (cancellationToken.IsCancellationRequested) return false;
			ActivateBotAtPosition(botSpawnInfo.botCreationData, positionOnNavMesh.Value, cancellationToken);
			return true;
		}
		
		if (!cancellationToken.IsCancellationRequested && DefaultPluginVars.debugLogging.Value)
		{
			logger.LogDebugDetailed("Failed to spawn some starting bots!", GetType().Name, nameof(TrySpawnStartingBot));
		}
		
		return false;
	}
	
	private async UniTask<bool> TryProcessBotWave(BotWave wave, CancellationToken cancellationToken = default)
	{
		ZoneSpawnPoints zoneSpawnPoints = dataService.ZoneSpawnPoints;
		string[] waveZones = wave.Zones;
		
		if (waveZones.Length == 0)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Donuts: No zones specified in {0} bot wave for GroupNum {1}. Check your scenario wave patterns are set up correctly!",
				SpawnType.Localized(), wave.GroupNum.ToString());
			DonutsHelper.NotifyLogError(sb.ToString());
			return false;
		}

		string randomZone = waveZones.PickRandomElement()!;
		
		// Instead of loosely matching, we do an exact match so we know if the wave is using the 'hotspot' keyword
		if (ZoneSpawnPoints.IsKeywordZone(randomZone, out ZoneSpawnPoints.KeywordZoneType keyword, exactMatch: true))
		{
			if (keyword == ZoneSpawnPoints.KeywordZoneType.Hotspot)
			{
				AdjustHotspotSpawnChance(wave, randomZone);
			}
			
			return await TrySpawnBotIfValidZone(keyword, wave, zoneSpawnPoints,
				isHotspot: keyword == ZoneSpawnPoints.KeywordZoneType.Hotspot, cancellationToken);
		}
		
		// This time, do a loose match for 'hotspot' so we can adjust the spawn chance of zones with 'hotspot' in the name
		bool isHotspot = ZoneSpawnPoints.IsHotspotZone(randomZone, out _);
		if (isHotspot)
		{
			AdjustHotspotSpawnChance(wave, randomZone);
		}
		
		return await TrySpawnBotIfValidZone(randomZone, wave, zoneSpawnPoints, isHotspot, cancellationToken);
	}

	private async UniTask<bool> TrySpawnBotIfValidZone(
		[NotNull] string zoneName,
		[NotNull] BotWave wave,
		[NotNull] ZoneSpawnPoints zoneSpawnPoints,
		bool isHotspot,
		CancellationToken cancellationToken = default)
	{
		if (!zoneSpawnPoints.TryGetValue(zoneName, out HashSet<Vector3> spawnPoints))
		{
			return false;
		}
		
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		foreach (Vector3 spawnPoint in spawnPoints.ShuffleElements())
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			
			if (!IsHumanPlayerWithinTriggerDistance(wave.TriggerDistance, spawnPoint) ||
				!await TrySpawnBot(wave, spawnPoint, isHotspot, cancellationToken))
			{
				continue;
			}
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				sb.Clear();
				sb.AppendFormat("Spawning bot wave for GroupNum {0} at {1}, {2}", wave.GroupNum, zoneName,
					spawnPoint.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotIfValidZone));
			}
			
			return true;
		}
		
		sb.Clear();
		sb.AppendFormat("Failed to find a valid spawn point for bot wave GroupNum {0}", wave.GroupNum);
		logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotIfValidZone));
		return false;
	}
	
	private async UniTask<bool> TrySpawnBotIfValidZone(
		ZoneSpawnPoints.KeywordZoneType keyword,
		[NotNull] BotWave wave,
		[NotNull] ZoneSpawnPoints zoneSpawnPoints,
		bool isHotspot,
		CancellationToken cancellationToken = default)
	{
		KeyValuePair<string, HashSet<Vector3>>[] keywordZones = zoneSpawnPoints.GetSpawnPointsFromKeyword(keyword);
		if (keywordZones == null || keywordZones.Length == 0)
		{
			return false;
		}
		
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		KeyValuePair<string, HashSet<Vector3>> spawnPoints = keywordZones.PickRandomElement();
		foreach (Vector3 spawnPoint in spawnPoints!.Value.ShuffleElements())
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			
			if (!IsHumanPlayerWithinTriggerDistance(wave.TriggerDistance, spawnPoint) ||
				!await TrySpawnBot(wave, spawnPoint, isHotspot, cancellationToken))
			{
				continue;
			}
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				sb.Clear();
				sb.AppendFormat("Spawning bot wave for GroupNum {0} at {1} (Keyword: {2}), {3}", wave.GroupNum,
					spawnPoints.Key, keyword.ToString(), spawnPoint.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotIfValidZone));
			}
			
			return true;
		}
		
		sb.Clear();
		sb.AppendFormat("Failed to find a valid spawn point for bot wave GroupNum {0}", wave.GroupNum);
		logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotIfValidZone));
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
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("{0} is a hotspot; hotspot boost is enabled, setting spawn chance to 100", zoneName);
			logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustHotspotSpawnChance));
		}
		
		wave.SetSpawnChance(100);
	}
	
	private bool IsHumanPlayerWithinTriggerDistance(int triggerDistance, Vector3 position)
	{
		int triggerSqrMagnitude = triggerDistance * triggerDistance;
		
		ReadOnlyCollection<Player> humanPlayerList = configService.GetHumanPlayerList();
		for (int i = humanPlayerList.Count - 1; i >= 0; i--)
		{
			Player player = humanPlayerList[i];
			if (player == null || !player.IsAlive())
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
	
	protected abstract bool ShouldIgnoreHardCap(bool isHotspot);
	protected abstract bool HasReachedHardCap(bool isHotspot);
	
	/// <summary>
	/// Checks certain spawn options, reset groups timers.
	/// </summary>
	private async UniTask<bool> TrySpawnBot(
		[NotNull] BotWave wave,
		Vector3 spawnPoint,
		bool isHotspot,
		CancellationToken cancellationToken = default)
	{
		if (DefaultPluginVars.HardCapEnabled.Value && HasReachedHardCap(isHotspot))
		{
			logger.LogDebugDetailed("Hard cap reached, aborting bot wave spawn!", GetType().Name, nameof(TrySpawnBot));
			return false;
		}
		
		if (HasReachedHardStopTime())
		{
			logger.LogDebugDetailed("Hard stop time reached, aborting bot wave spawn!", GetType().Name, nameof(TrySpawnBot));
			return false;
		}
		
		int groupSize = DetermineBotGroupSize(wave.MinGroupSize, wave.MaxGroupSize, isHotspot);
		if (groupSize < 1)
		{
			logger.LogDebugDetailed("Bot group size is invalid (less than 1), aborting bot wave spawn!", GetType().Name,
				nameof(TrySpawnBot));
			return false;
		}
		
		bool success = await SpawnBot(groupSize, spawnPoint, spawnChecker: spawnCheckProcessor,
			cancellationToken: cancellationToken);
		if (cancellationToken.IsCancellationRequested) return false;
		
		wave.SpawnTriggered();
		return success;
	}
	
	private async UniTask<bool> SpawnBot(
		int groupSize,
		Vector3 spawnPoint,
		bool generateNew = true,
		SpawnCheckProcessorBase spawnChecker = null,
		CancellationToken cancellationToken = default)
	{
		Vector3? spawnPosition = await GetValidSpawnPosition(spawnPoint, spawnChecker, cancellationToken);
		if (cancellationToken.IsCancellationRequested || !spawnPosition.HasValue)
		{
			if (!spawnPosition.HasValue && DefaultPluginVars.debugLogging.Value)
			{
				logger.LogDebugDetailed("No valid spawn position found after retries - skipping this spawn", GetType().Name,
					nameof(SpawnBot));
			}
			
			return false;
		}
		
		BotDifficulty difficulty = dataService.GetBotDifficulty();
		PrepBotInfo cachedPrepBotInfo = dataService.FindCachedBotData(difficulty, groupSize);
		var generateNewSuccess = false;
		
		if (generateNew && cachedPrepBotInfo?.botCreationData == null)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat(
					"No cached bots found for this spawn, generating new profiles for {0} bots - this may take some time...",
					groupSize.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(SpawnBot));
			}
			
			(bool success, cachedPrepBotInfo) = await dataService.TryGenerateBotProfiles(difficulty, groupSize,
				saveToCache: false, cancellationToken: cancellationToken);
			
			if (cancellationToken.IsCancellationRequested || !success)
			{
				if (DefaultPluginVars.debugLogging.Value)
				{
					logger.LogDebugDetailed("Failed to generate new bot profiles! Aborting spawning for this wave!",
						GetType().Name, nameof(SpawnBot));
				}
				
				return false;
			}
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Successfully generated {0} new bot profiles", groupSize);
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(SpawnBot));
			}
			
			generateNewSuccess = true;
		}
		
		if (cachedPrepBotInfo?.botCreationData == null)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				logger.LogDebugDetailed("Failed to find a cached bot to spawn! Aborting spawning for this wave!",
					GetType().Name, nameof(SpawnBot));
			}
			return false;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			logger.LogDebugDetailed("Found a cached bot to spawn! Preparing to spawn...", GetType().Name, nameof(SpawnBot));
		}
		
		ActivateBotAtPosition(cachedPrepBotInfo.botCreationData, spawnPosition.Value, cancellationToken);
		
		if (!generateNewSuccess)
		{
			dataService.RemoveFromBotCache(new PrepBotInfo.Key(difficulty, groupSize));
		}
		
		return true;
	}
	
	private async UniTask<Vector3?> GetValidSpawnPosition(
		Vector3 position,
		[CanBeNull] SpawnCheckProcessorBase spawnChecker,
		CancellationToken cancellationToken = default)
	{
		int maxSpawnAttempts = DefaultPluginVars.maxSpawnTriesPerBot.Value;
		for (var i = 0; i < maxSpawnAttempts; i++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return null;
			}
			
			if (!NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas) ||
				(spawnChecker != null && !spawnChecker.Process(navHit.position)))
			{
				await UniTask.DelayFrame(FRAME_DELAY_INTERVAL, cancellationToken: cancellationToken);
				continue;
			}
			
			Vector3 spawnPosition = navHit.position;
			
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Found spawn position at: {0}", spawnPosition.ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(GetValidSpawnPosition));
			}
			
			return spawnPosition;
		}
		
		return null;
	}
	
	protected abstract int GetBotGroupSize(int minGroupSize, int maxGroupSize);
	
	private int DetermineBotGroupSize(int minGroupSize, int maxGroupSize, bool isHotspot)
	{
		int groupSize = GetBotGroupSize(minGroupSize, maxGroupSize);
		if (groupSize < 1)
		{
			return -1;
		}
		
		// Check if hard cap is enabled and adjust maxCount based on active bot counts and limits
		if (DefaultPluginVars.HardCapEnabled.Value && !ShouldIgnoreHardCap(isHotspot))
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
	
	private int AdjustGroupSizeForHardCap(int groupSize)
	{
		int activeBots = dataService.GetAliveBotsCount();
		int botLimit = dataService.MaxBotLimit;
		
		if (activeBots >= botLimit)
		{
			return -1;
		}
		
		if (activeBots + groupSize > botLimit)
		{
			groupSize = botLimit - activeBots;
		}
		
		return groupSize;
	}
	
	protected abstract int GetMaxBotRespawns();
	
	private int AdjustMaxCountForRespawnLimits(int groupSize)
	{
		int maxBotRespawns = GetMaxBotRespawns();
		// Don't cap if maxRespawns is set to zero or less
		if (maxBotRespawns <= 0)
		{
			_currentRespawnCount += groupSize;
			return groupSize;
		}
		
		if (_currentRespawnCount >= maxBotRespawns)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Max {0} respawns reached, skipping this spawn", SpawnType.Localized());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustMaxCountForRespawnLimits));
			}
			
			return -1;
		}
		
		if (_currentRespawnCount + groupSize >= maxBotRespawns)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Max {0} respawn limit reached: {1}. Current {0} respawns this raid: {2}",
					SpawnType.Localized(), DefaultPluginVars.maxRespawnsPMC.Value.ToString(),
					(_currentRespawnCount + groupSize).ToString());
				logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustMaxCountForRespawnLimits));
			}
			
			groupSize = maxBotRespawns - _currentRespawnCount;
		}
		
		_currentRespawnCount += groupSize;
		return groupSize;
	}
}