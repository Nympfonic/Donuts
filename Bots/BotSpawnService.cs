using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Bots.SpawnCheckProcessor;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using SPT.SinglePlayer.Utils.InRaid;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Systems.Effects;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public interface IBotSpawnService
{
	void FrameUpdate(float deltaTime);
	UniTask SpawnStartingBots(CancellationToken cancellationToken);
	UniTask SpawnBotWaves(CancellationToken cancellationToken);
	void DespawnFurthestBot();
	void RestartPlayerHitTimer();
}

public abstract class BotSpawnService : IBotSpawnService
{
	private BotSpawner _eftBotSpawner;
	private GameWorld _gameWorld;
	private string _mapLocation;
	private BotsController _botsController;
	private IBotCreator _botCreator;

	private ISpawnCheckProcessor _spawnCheckProcessor;
	
	private bool _hasSpawnedStartingBots;
	private float _despawnCooldownTime;
	
	// Combat state
	private float _timeSinceLastHit;
	
	// Spawn caps
	private int _currentRespawnCount;
	
	protected BotConfigService ConfigService { get; private set; }
	protected IBotDataService DataService { get; private set; }
	protected ManualLogSource Logger { get; private set; }
	protected MapBotWaves MapBotWaves { get; private set; }

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
		return service;
	}

	public void FrameUpdate(float deltaTime)
	{
		_timeSinceLastHit += deltaTime;
		UpdateBotWaveTimers(deltaTime);
	}

	public void RestartPlayerHitTimer()
	{
		_timeSinceLastHit = 0;
	}

	/// <summary>
	/// Checks if HardStopTime setting is enabled and if the HardStopTime value has been reached for a spawn type.
	/// </summary>
	/// <remarks>Must call the <see cref="HasReachedHardStopTime(int, int)"/> overloaded signature.</remarks>
	protected abstract bool HasReachedHardStopTime();

	// TODO: why is this method failing?
	// This is an old comment, method has been changed so it needs testing
	protected bool HasReachedHardStopTime(int hardStopTime, int hardStopPercent)
	{
		float raidTimeLeftTime = RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
		float raidTimeLeftPercent = RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f; // Percent left
#if DEBUG
		Logger.LogDebug(string.Format(
			"RaidTimeLeftTime: {0}, RaidTimeLeftPercent: {1}, HardStopTime: {2}, HardStopPercent: {3}",
			raidTimeLeftTime.ToString(CultureInfo.InvariantCulture),
			raidTimeLeftPercent.ToString(CultureInfo.InvariantCulture), hardStopTime.ToString(),
			hardStopPercent.ToString()));
#endif
		return DefaultPluginVars.useTimeBasedHardStop.Value
			? raidTimeLeftTime <= hardStopTime
			: raidTimeLeftPercent <= hardStopPercent;
	}

	protected virtual void Initialize(
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
		
		_gameWorld = Singleton<GameWorld>.Instance;
		_mapLocation = ConfigService.GetMapLocation();
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_botCreator = (IBotCreator)ReflectionHelper.BotSpawner_botCreator_Field.GetValue(_eftBotSpawner);
		
		var isBotSpawningEnabled = (bool)ReflectionHelper.BotsController_botEnabled_Field
			.GetValue(Singleton<IBotGame>.Instance.BotsController);
		if (!isBotSpawningEnabled || !ConfigService.CheckForAnyScenarioPatterns())
		{
			return;
		}

		MapBotWaves = ConfigService.GetBotWavesConfig()?.Maps[_mapLocation];
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

	protected abstract List<BotWave> GetBotWavesList();

	private void UpdateBotWaveTimers(float deltaTime)
	{
		float cooldownDuration = DefaultPluginVars.coolDownTimer.Value;
		foreach (BotWave wave in GetBotWavesList())
		{
			wave.UpdateTimer(deltaTime, cooldownDuration);
		}
	}

	[CanBeNull]
	private AICorePoint GetClosestCorePoint(Vector3 position) =>
		_botsController.CoversData.GetClosest(position)?.CorePointInGame;

	private void ActivateBotAtPosition(
		[NotNull] BotCreationDataClass botData,
		Vector3 spawnPosition,
		CancellationToken cancellationToken)
	{
		BotZone closestBotZone = _eftBotSpawner.GetClosestZone(spawnPosition, out _);
		AICorePoint closestCorePoint = GetClosestCorePoint(spawnPosition);
		botData.AddPosition(spawnPosition, closestCorePoint!.Id);
		
		var createBotCallbackWrapper = new CreateBotCallbackWrapper(botData);
		var getGroupWrapper = new GetBotsGroupWrapper(_eftBotSpawner);
		var groupAction = new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies);
		var callback = new Action<BotOwner>(createBotCallbackWrapper.CreateBotCallback);

		_botCreator.ActivateBot(botData, closestBotZone, false, groupAction, callback, cancellationToken);
		DataService.ClearBotCache(botData);
		//_botDataService.ReplenishBotDataTimer.Restart();
	}

	public async UniTask SpawnStartingBots(CancellationToken cancellationToken)
	{
		if (_hasSpawnedStartingBots) return;

		_hasSpawnedStartingBots = true;
		
		foreach (BotSpawnInfo botSpawnInfo in DataService.BotSpawnInfos)
		{
			if (cancellationToken.IsCancellationRequested) return;
			
			await SpawnBot(botSpawnInfo.GroupSize, botSpawnInfo.Zone, botSpawnInfo.Coordinates, cancellationToken);
		}
	}

	public async UniTask SpawnBotWaves(CancellationToken cancellationToken)
	{
		var anySpawned = false;

		foreach (BotWave botWave in GetBotWavesList())
		{
			if (cancellationToken.IsCancellationRequested) return;
			
			if (IsHumanPlayerInCombat())
			{
#if DEBUG
				Logger.LogDebug("In combat state cooldown, breaking the loop.");
#endif
				break;
			}

			if (CanSpawn(botWave.SpawnChance) && await TryProcessBotWave(botWave, cancellationToken))
			{
				anySpawned = true;
			}
			// if CanSpawn is false then we need to reset the timers for this wave
			ResetGroupTimers(botWave.GroupNum);
		}

		if (!anySpawned)
		{
			await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
		}
	}
	
	protected abstract bool IsCorrectSpawnType(WildSpawnType role);
	
	[CanBeNull]
	private Player FindFurthestBot()
	{
		var maxSqrMagnitude = float.MinValue;
		Player furthestBot = null;

		foreach (Player bot in _gameWorld.AllAlivePlayersList)
		{
			if (bot.IsYourPlayer || bot.AIData.BotOwner == null || IsCorrectSpawnType(bot.Profile.Info.Settings.Role))
			{
				continue;
			}

			// Get distance of bot to player using squared distance
			float sqrMagnitude = (_gameWorld.MainPlayer.Transform.position - bot.Transform.position).sqrMagnitude;

			// Check if this is the furthest distance
			if (sqrMagnitude > maxSqrMagnitude)
			{
				maxSqrMagnitude = sqrMagnitude;
				furthestBot = bot;
			}
		}

#if DEBUG
		if (furthestBot == null)
		{
			Logger.LogDebug("Furthest bot is null. No bots found in the list.");
		}
		else
		{
			Logger.LogDebug(string.Format("Furthest bot found: {0} at distance {1}", furthestBot.Profile.Info.Nickname,
				Mathf.Sqrt(maxSqrMagnitude).ToString(CultureInfo.InvariantCulture)));
		}
#endif

		return furthestBot;
	}
	
	// Only consider despawning if the number of active bots of the type exceeds the limit
	private bool CanDespawnBot() => GetAliveBotsCount() > DataService.MaxBotLimit;

	public void DespawnFurthestBot()
	{
		if (!IsDespawnBotEnabled()) return;

		float currentTime = Time.time;
		float timeSinceLastDespawn = currentTime - _despawnCooldownTime;

		if (timeSinceLastDespawn < DefaultPluginVars.despawnInterval.Value || !CanDespawnBot())
		{
			return;
		}

		Player furthestBot = FindFurthestBot();

		if (furthestBot != null)
		{
			DespawnBot(furthestBot);
		}
#if DEBUG
		else
		{
			Logger.LogDebug($"No {DataService.SpawnType.ToString()} bot found to despawn.");
		}
#endif
	}

	protected abstract bool IsDespawnBotEnabled();

	private void DespawnBot([NotNull] Player furthestBot)
	{
		if (furthestBot == null)
		{
			Logger.LogError("Attempted to despawn a null bot.");
			return;
		}

		BotOwner botOwner = furthestBot.AIData.BotOwner;
		if (botOwner == null)
		{
			Logger.LogError("BotOwner is null for the furthest bot.");
			return;
		}

#if DEBUG
		Logger.LogDebug($"Despawning bot: {furthestBot.Profile.Info.Nickname} ({furthestBot.name})");
#endif

		_gameWorld.RegisteredPlayers.Remove(botOwner);
		_gameWorld.AllAlivePlayersList.Remove(botOwner.GetPlayer);

		IBotGame botGame = Singleton<IBotGame>.Instance;
		Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botOwner.GetPlayer);
		botOwner.Deactivate();
		botOwner.Dispose();
		botGame.BotsController.BotDied(botOwner);
		botGame.BotsController.DestroyInfo(botOwner.GetPlayer);
		//DestroyImmediate(botOwner.gameObject);
		Object.Destroy(botOwner.gameObject);
		//Destroy(botOwner);

		// Update the cooldown
		_despawnCooldownTime = Time.time;
	}

	private bool IsHumanPlayerInCombat()
	{
		return _timeSinceLastHit < DefaultPluginVars.battleStateCoolDown.Value;
	}

	private bool CanSpawn(int spawnChance)
    {
    	int randomValue = Random.Range(0, 100);
    	bool canSpawn = randomValue < spawnChance;
#if DEBUG
    	Logger.LogDebug(string.Format("SpawnChance: {0}, RandomValue: {1}, CanSpawn: {2}", spawnChance.ToString(),
		    randomValue.ToString(), canSpawn.ToString()));
#endif
    	return canSpawn;
    }

	private async UniTask<bool> TryProcessBotWave(BotWave botWave, CancellationToken cancellationToken)
	{
		Dictionary<string, List<Vector3>> zoneSpawnPoints = DataService.ZoneSpawnPoints;
		if (!zoneSpawnPoints.Any())
		{
#if DEBUG
			Logger.LogDebug("No zone spawn points found, cannot spawn any bot waves.");
#endif
			return false;
		}

		string randomZone = zoneSpawnPoints.Keys.PickRandomElement()!;
		List<Vector3> spawnPoints = zoneSpawnPoints[randomZone].ShuffleElements();

		AdjustSpawnChanceIfHotspot(botWave, randomZone);

		foreach (Vector3 spawnPoint in spawnPoints)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			
#if DEBUG
			Logger.LogDebug($"Triggering spawn for botWave: {botWave} at {randomZone}, {spawnPoint.ToString()}");
#endif
			if (await TrySpawnBot(botWave, randomZone, spawnPoint, spawnPoints, cancellationToken))
			{
				return true;
			}
		}
		return false;
	}

	private void ResetGroupTimers(int groupNum)
	{
		foreach (BotWave botWave in GetBotWavesList())
		{
			if (botWave.GroupNum == groupNum)
			{
				botWave.ResetTimer();
#if DEBUG
				Logger.LogDebug($"Resetting timer for GroupNum: {groupNum.ToString()}");
#endif
			}
		}
	}
	
	protected abstract bool IsHotspotBoostEnabled();

	/// <summary>
	/// Sets the bot wave's spawn chance to 100% if the zone is a hotspot.
	/// </summary>
	private void AdjustSpawnChanceIfHotspot(BotWave botWave, string zone)
	{
		if (!IsHotspotBoostEnabled()) return;
		
		bool isHotspotZone = zone.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0;
		if (!isHotspotZone) return;
		
#if DEBUG
		Logger.LogDebug($"{zone} is a hotspot; hotspot boost is enabled, setting spawn chance to 100");
#endif
		botWave.SpawnChance = 100;
	}


	private bool IsHumanPlayerWithinTriggerDistance(int triggerDistance, Vector3 position)
	{
		int triggerSqrMagnitude = triggerDistance * triggerDistance;

		List<Player> humanPlayerList = ConfigService.GetHumanPlayerList();
		for (int i = humanPlayerList.Count - 1; i >= 0; i--)
		{
			Player player = humanPlayerList[i];
			if (player == null || player.HealthController == null || player.HealthController.IsAlive == false) continue;

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
	private async UniTask<bool> TrySpawnBot(
		BotWave botWave,
		string zone,
		Vector3 primarySpawnPoint,
		List<Vector3> spawnPoints,
		CancellationToken cancellationToken)
	{
		if (!IsHumanPlayerWithinTriggerDistance(botWave.TriggerDistance, primarySpawnPoint) ||
			(DefaultPluginVars.HardCapEnabled.Value && HasReachedHardCap()) ||
			HasReachedHardStopTime())
		{
			ResetGroupTimers(botWave.GroupNum); // Reset timer if the wave is hard capped
			return false;
		}
		
		int groupSize = DetermineBotGroupSize(botWave.MinGroupSize, botWave.MaxGroupSize);
		if (groupSize < 1)
		{
			return false;
		}

		if (await SpawnBot(groupSize, zone, spawnPoints, cancellationToken))
		{
			botWave.TimesSpawned++;
		}
		
		if (cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		
		ResetGroupTimers(botWave.GroupNum);

		if (botWave.TimesSpawned >= botWave.MaxTriggersBeforeCooldown)
		{
			botWave.TriggerCooldown();
		}
		return true;
	}

	private async UniTask<bool> SpawnBot(
		int groupSize,
		string zone,
		List<Vector3> spawnPoints,
		CancellationToken cancellationToken)
	{
		bool isGroup = groupSize > 1;
#if DEBUG
		Logger.LogDebug($"Attempting to spawn {(isGroup ? "group" : "solo")} bot {groupSize.ToString()} in spawn zone {zone}");
#endif
		
		BotDifficulty botDifficulty = DataService.GetBotDifficulty();

		BotCreationDataClass cachedBotData = DataService.FindCachedBotData(botDifficulty, groupSize);
		if (cachedBotData == null)
		{
#if DEBUG
			Logger.LogWarning(string.Format(
				"No cached bots found for this spawn, generating on the fly for {0} bots - this may take some time.",
				groupSize.ToString()));
#endif
			var botInfo = new PrepBotInfo(botDifficulty, isGroup, groupSize);
			(bool success, cachedBotData) = await DataService.TryCreateBotData(botInfo);
			if (cancellationToken.IsCancellationRequested || !success)
			{
				return false;
			}
		}
#if DEBUG
		else Logger.LogWarning("Found grouped cached bots, spawning them.");
#endif

		var spawned = false;
		foreach (Vector3 position in spawnPoints)
		{
			Vector3 spawnPosition = await GetValidSpawnPosition(position, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			
			if (spawnPosition != Vector3.positiveInfinity)
			{
				ActivateBotAtPosition(cachedBotData, spawnPosition, cancellationToken);
				spawned = true;
			}
		}
		
#if DEBUG
		if (!spawned)
		{
			Logger.LogDebug("No valid spawn position found after retries - skipping this spawn");
		}
#endif
		return spawned;
	}

	private async UniTask<Vector3> GetValidSpawnPosition(Vector3 position, CancellationToken cancellationToken)
	{
		try
		{
			int maxSpawnAttempts = DefaultPluginVars.maxSpawnTriesPerBot.Value;
			for (var i = 0; i < maxSpawnAttempts; i++)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return Vector3.positiveInfinity;
				}

				if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
				{
					var spawnCheckData = new SpawnCheckData(navHit.position, _mapLocation,
						_gameWorld.AllAlivePlayersList);
					_spawnCheckProcessor.Process(spawnCheckData);
					if (!spawnCheckData.Success)
					{
						return Vector3.positiveInfinity;
					}
					
					Vector3 spawnPosition = navHit.position;
#if DEBUG
					Logger.LogDebug($"Found spawn position at: {spawnPosition.ToString()}");
#endif
					return spawnPosition;
				}
				
				await UniTask.Yield(cancellationToken);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError(string.Format("Exception thrown in {0}::{1}: {2}\n{3}", GetType(),
				nameof(GetValidSpawnPosition), ex.Message, ex.StackTrace));
		}
		catch (OperationCanceledException) {}

		return Vector3.positiveInfinity;
	}

	protected abstract int GetBotGroupSize(int minGroupSize, int maxGroupSize);

	private int DetermineBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		int groupSize = GetBotGroupSize(minGroupSize, maxGroupSize);

		// Check if hard cap is enabled and adjust maxCount based on active bot counts and limits
		if (DefaultPluginVars.HardCapEnabled.Value)
		{
			groupSize = AdjustGroupSizeForHardCap(groupSize);
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
		if (activeBots + groupSize > DataService.MaxBotLimit)
		{
			groupSize = DataService.MaxBotLimit - activeBots;
		}
		return groupSize;
	}

	protected abstract int GetMaxBotRespawns();

	private int AdjustMaxCountForRespawnLimits(int groupSize)
	{
		int maxBotRespawns = GetMaxBotRespawns();
		if (maxBotRespawns <= 0)
		{
			_currentRespawnCount += groupSize;
			return groupSize;
		}

		if (_currentRespawnCount >= maxBotRespawns)
		{
#if DEBUG
			Logger.LogDebug($"Max {DataService.SpawnType.ToString()} respawns reached, skipping this spawn");
#endif
			return -1;
		}

		if (_currentRespawnCount + groupSize >= maxBotRespawns)
		{
#if DEBUG
			Logger.LogDebug(
				string.Format("Max {0} respawn limit reached: {1}. Current {0} respawns this raid: {2}",
					DataService.SpawnType.ToString(), DefaultPluginVars.maxRespawnsPMC.Value.ToString(),
					(_currentRespawnCount + groupSize).ToString()));
#endif
			groupSize = maxBotRespawns - _currentRespawnCount;
			
		}
		_currentRespawnCount += groupSize;
		return groupSize;
	}
}