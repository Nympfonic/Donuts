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
using System.Linq;
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
	private BotSpawner _eftBotSpawner;
	private CancellationToken _onDestroyToken;
	
	private SpawnCheckProcessorBase _spawnCheckProcessor;
	
	private GameWorld _gameWorld;
	private ReadOnlyCollection<Player> _allAlivePlayersReadOnly;
	private string _mapLocation;
	private BotsController _botsController;
	private IBotCreator _botCreator;
	
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
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.BotSpawnServices.Add(dataService.SpawnType, service);
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

	protected abstract bool IsHardStopEnabled();
	protected abstract int GetHardStopTime();

	// TODO: why is this method failing?
	// This is an old comment, method has been changed so it needs testing
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
#if DEBUG
			using (var sb = ZString.CreateUtf8StringBuilder())
			{
				sb.AppendFormat("RaidTimeLeftTime: {0}, HardStopTime: {1}",
					raidTimeLeftTime.ToString(CultureInfo.InvariantCulture), hardStopTime.ToString());
				Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(HasReachedHardStopTime));
			}
#endif
			return raidTimeLeftTime <= hardStopTime;
		}

		float raidTimeLeftPercent = RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f; // Percent left
		int hardStopPercent = GetHardStopTime();
#if DEBUG
		using (var sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("RaidTimeLeftPercent: {0}, HardStopPercent: {1}",
				raidTimeLeftPercent.ToString(CultureInfo.InvariantCulture), hardStopPercent.ToString());
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(HasReachedHardStopTime));
		}
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

	[CanBeNull]
	private AICorePoint GetClosestCorePoint(Vector3 position) =>
		_botsController.CoversData.GetClosest(position)?.CorePointInGame;

	private void ActivateBotAtPosition(
		[NotNull] BotCreationDataClass botData,
		Vector3 spawnPosition)
	{
		BotZone closestBotZone = _eftBotSpawner.GetClosestZone(spawnPosition, out _);
		AICorePoint closestCorePoint = GetClosestCorePoint(spawnPosition);
		botData.AddPosition(spawnPosition, closestCorePoint!.Id);
		
		var createBotCallbackWrapper = new CreateBotCallbackWrapper(botData);
		var getGroupWrapper = new GetBotsGroupWrapper(_eftBotSpawner);
		var groupAction = new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies);
		var callback = new Action<BotOwner>(createBotCallbackWrapper.CreateBotCallback);

		_botCreator.ActivateBot(botData, closestBotZone, false, groupAction, callback, _onDestroyToken);
		DataService.ClearBotCache(botData);
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.UpdateReplenishBotDataTime();
	}

	public async UniTask SpawnStartingBots()
	{
		for (int i = DataService.BotSpawnInfos.Count - 1; i >= 0; i--)
		{
			BotSpawnInfo botSpawnInfo = DataService.BotSpawnInfos[i];
			if (_onDestroyToken.IsCancellationRequested) return;

			await SpawnBot(botSpawnInfo.GroupSize, botSpawnInfo.Coordinates, generateNew: false);
		}
	}

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
		var anySpawned = false;
		if (IsHumanPlayerInCombat())
		{
			ResetGroupTimers(wave.GroupNum);
#if DEBUG
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(),
				"A Human player is in combat state");
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotWave));
#endif
			return false;
		}

		if (CanSpawn(wave.SpawnChance) && await TryProcessBotWave(wave))
		{
			anySpawned = true;
		}
			
 		ResetGroupTimers(wave.GroupNum);
#if DEBUG
 		using (var sb = ZString.CreateUtf8StringBuilder())
 		{
 			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", wave.GroupNum.ToString(),
			    "Bot wave spawn triggered");
 			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBotWave));
 		}
#endif
		return anySpawned;
	}
	
	protected abstract bool IsCorrectSpawnType(WildSpawnType role);
	
	/// <summary>
	/// Finds the furthest bot away from all human players
	/// </summary>
	/// <returns></returns>
	[CanBeNull]
	private Player FindFurthestBot()
	{
		var furthestSqrMagnitude = float.MinValue;
		Player furthestBot = null;
		ReadOnlyCollection<Player> allAlivePlayers = _allAlivePlayersReadOnly;
		List<Player> humanPlayers = ConfigService.GetHumanPlayerList();
		// Iterate through alive players
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			// Ignore players that aren't bots and aren't the correct spawn type
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
				if (humanPlayer.HealthController == null || !humanPlayer.HealthController.IsAlive)
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
		using var sb = ZString.CreateUtf8StringBuilder();
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

	public void DespawnExcessBots()
	{
#if DEBUG
		using var sb = ZString.CreateUtf8StringBuilder();
		string typeName = GetType().Name;
		const string methodName = nameof(DespawnExcessBots);
		var spawnTypeName = DataService.SpawnType.ToString();
#endif
		
		if (!IsDespawnBotEnabled())
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Despawning for {0} bots is disabled.", spawnTypeName);
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
			return;
		}
		
		float timeSinceLastDespawn = Time.time - _despawnCooldownTime;
		bool hasReachedTimeToDespawn = timeSinceLastDespawn >= DefaultPluginVars.despawnInterval.Value;
		if (!IsOverBotLimit(out int excessBots) || !hasReachedTimeToDespawn)
		{
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Haven't met conditions to despawn {0] bots yet.", spawnTypeName);
			Logger.LogDebugDetailed(sb.ToString(), typeName, methodName);
#endif
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

	protected abstract bool IsDespawnBotEnabled();

	private bool TryDespawnBot([NotNull] Player furthestBot)
	{
		if (furthestBot == null)
		{
#if DEBUG
			Logger.LogDebugDetailed("Attempted to despawn a null bot.", GetType().Name, nameof(TryDespawnBot));
#endif
			return false;
		}

		BotOwner botOwner = furthestBot.AIData?.BotOwner;
		if (botOwner == null)
		{
#if DEBUG
			Logger.LogDebugDetailed("BotOwner is null for the furthest bot.", GetType().Name, nameof(TryDespawnBot));
#endif
			return false;
		}

#if DEBUG
		using (var sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("Despawning bot: {0} ({1})", furthestBot.Profile.Info.Nickname, furthestBot.name);
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryDespawnBot));
		}
#endif

		Player botPlayer = botOwner.GetPlayer;
		_gameWorld.RegisteredPlayers.Remove(botOwner);
		_gameWorld.AllAlivePlayersList.Remove(botPlayer);
		
		Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botPlayer);
        botOwner.LeaveData.RemoveFromMap(); // BSG calls this to despawn; this calls the BotOwner::Deactivate(), BotOwner::Dispose() and IBotGame::BotDespawn() methods
        
		// Update the cooldown
		_despawnCooldownTime = Time.time;
		return true;
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
	    using var sb = ZString.CreateUtf8StringBuilder();
	    sb.AppendFormat("SpawnChance: {0}, RandomValue: {1}, CanSpawn: {2}", spawnChance.ToString(),
			randomValue.ToString(), canSpawn.ToString());
	    Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(CanSpawn));
#endif
    	return canSpawn;
    }

	private async UniTask<bool> TryProcessBotWave(BotWave botWave)
	{
		Dictionary<string, List<Vector3>> zoneSpawnPoints = DataService.ZoneSpawnPoints;
		if (!zoneSpawnPoints.Any())
		{
			ResetGroupTimers(botWave.GroupNum);
#if DEBUG
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}", botWave.GroupNum.ToString(),
				"No zone spawn points found");
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryProcessBotWave));
#endif
			return false;
		}

		string randomZone = zoneSpawnPoints.Keys.PickRandomElement()!;
		List<Vector3> spawnPoints = zoneSpawnPoints[randomZone].ShuffleElements();

		AdjustSpawnChanceIfHotspot(botWave, randomZone);

		foreach (Vector3 spawnPoint in spawnPoints)
		{
			if (_onDestroyToken.IsCancellationRequested)
			{
				return false;
			}
			
			if (await TrySpawnBot(botWave, randomZone, spawnPoint, spawnPoints))
			{
				return true;
			}
		}
		return false;
	}

	private void ResetGroupTimers(int groupNum)
	{
		foreach (BotWave botWave in GetBotWaves())
		{
			if (botWave.GroupNum == groupNum)
			{
				botWave.ResetTimer();
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
		using (var sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("{0} is a hotspot; hotspot boost is enabled, setting spawn chance to 100", zone);
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustSpawnChanceIfHotspot));
		}
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
		List<Vector3> spawnPoints)
	{
		if (!IsHumanPlayerWithinTriggerDistance(botWave.TriggerDistance, primarySpawnPoint) ||
			(DefaultPluginVars.HardCapEnabled.Value && HasReachedHardCap()) ||
			HasReachedHardStopTime())
		{
			ResetGroupTimers(botWave.GroupNum); // Reset timer if the wave is hard capped
#if DEBUG
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Resetting timer for GroupNum {0}, reason: {1}",
				botWave.GroupNum.ToString(), "Reached hard cap");
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBot));
#endif
			return false;
		}
		
		int groupSize = DetermineBotGroupSize(botWave.MinGroupSize, botWave.MaxGroupSize);
		if (groupSize < 1)
		{
			return false;
		}

		if (await SpawnBot(groupSize, spawnPoints))
		{
			botWave.TimesSpawned++;
#if DEBUG
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Spawning bot wave for GroupNum {0} at {1}, {2}",
				botWave.GroupNum, zone, primarySpawnPoint.ToString());
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TrySpawnBot));
#endif
		}
		
		if (_onDestroyToken.IsCancellationRequested)
		{
			return false;
		}

		if (botWave.TimesSpawned >= botWave.MaxTriggersBeforeCooldown)
		{
			botWave.TriggerCooldown();
		}
		return true;
	}

	private async UniTask<bool> SpawnBot(
		int groupSize,
		List<Vector3> spawnPoints,
		bool generateNew = true,
		bool ignoreChecks = false)
	{
		bool isGroup = groupSize > 1;
		BotDifficulty botDifficulty = DataService.GetBotDifficulty();

		BotCreationDataClass cachedBotData = DataService.FindCachedBotData(botDifficulty, groupSize);
		if (generateNew && cachedBotData == null)
		{
#if DEBUG
			using (var sb = ZString.CreateUtf8StringBuilder())
			{
				sb.AppendFormat("No cached bots found for this spawn, generating on the fly for {0} bots - this may take some time.",
					groupSize.ToString());
				Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(SpawnBot));
			}
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
		Logger.LogDebugDetailed("Found grouped cached bots, spawning them.", GetType().Name, nameof(SpawnBot));
#endif

		var spawned = false;
		foreach (Vector3 position in spawnPoints)
		{
			Vector3 spawnPosition = await GetValidSpawnPosition(ignoreChecks, position);
			if (_onDestroyToken.IsCancellationRequested)
			{
				return false;
			}
			
			if (spawnPosition != Vector3.zero)
			{
				ActivateBotAtPosition(cachedBotData, spawnPosition);
				spawned = true;
				break;
			}
		}
		
#if DEBUG
		if (!spawned)
		{
			Logger.LogDebugDetailed("No valid spawn position found after retries - skipping this spawn", GetType().Name,
				nameof(SpawnBot));
		}
#endif
		return spawned;
	}

	private async UniTask<Vector3> GetValidSpawnPosition(bool ignoreChecks, Vector3 position)
	{
		int maxSpawnAttempts = DefaultPluginVars.maxSpawnTriesPerBot.Value;
		for (var i = 0; i < maxSpawnAttempts; i++)
		{
			if (_onDestroyToken.IsCancellationRequested)
			{
				return Vector3.zero;
			}

			if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
			{
				if (!ignoreChecks)
				{
					var spawnCheckData = new SpawnCheckData(navHit.position, _mapLocation, _allAlivePlayersReadOnly);
					_spawnCheckProcessor.Process(spawnCheckData);
					if (!spawnCheckData.Success)
					{
						return Vector3.zero;
					}
				}
					
				Vector3 spawnPosition = navHit.position;
#if DEBUG
				using var sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Found spawn position at: {0}", spawnPosition.ToString());
				Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(GetValidSpawnPosition));
#endif
				return spawnPosition;
			}
				
			await UniTask.Yield(_onDestroyToken);
		}

		return Vector3.zero;
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
		// Don't cap if maxRespawns is set to zero or less
		if (maxBotRespawns <= 0)
		{
			_currentRespawnCount += groupSize;
			return groupSize;
		}

		if (_currentRespawnCount >= maxBotRespawns)
		{
#if DEBUG
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Max {0} respawns reached, skipping this spawn", DataService.SpawnType.ToString());
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustMaxCountForRespawnLimits));
#endif
			return -1;
		}

		if (_currentRespawnCount + groupSize >= maxBotRespawns)
		{
#if DEBUG
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Max {0} respawn limit reached: {1}. Current {2} respawns this raid: {3}",
				DataService.SpawnType.ToString(), DefaultPluginVars.maxRespawnsPMC.Value.ToString(),
				(_currentRespawnCount + groupSize).ToString());
			Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(AdjustMaxCountForRespawnLimits));
#endif
			groupSize = maxBotRespawns - _currentRespawnCount;
			
		}
		_currentRespawnCount += groupSize;
		return groupSize;
	}
}