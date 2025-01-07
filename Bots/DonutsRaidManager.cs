using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Donuts.Models;
using Donuts.Patches;
using Donuts.Tools;
using Donuts.Utils;
using EFT;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityToolkit.Extensions;

#pragma warning disable CS0252, CS0253

namespace Donuts.Bots;

public class DonutsRaidManager : MonoBehaviourSingleton<DonutsRaidManager>
{
	// private readonly Dictionary<WildSpawnType, EPlayerSide> _spawnTypeToSideMapping = new()
	// {
	// 	{ WildSpawnType.arenaFighterEvent, EPlayerSide.Savage },
	// 	{ WildSpawnType.assault, EPlayerSide.Savage },
	// 	{ WildSpawnType.assaultGroup, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossBoar, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossBoarSniper, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossBully, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossGluhar, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossKilla, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossKojaniy, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossSanitar, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossTagilla, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossZryachiy, EPlayerSide.Savage },
	// 	{ WildSpawnType.crazyAssaultEvent, EPlayerSide.Savage },
	// 	{ WildSpawnType.cursedAssault, EPlayerSide.Savage },
	// 	{ WildSpawnType.exUsec, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerBoar, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerBully, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerGluharAssault, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerGluharScout, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerGluharSecurity, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerGluharSnipe, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerKojaniy, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerSanitar, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerTagilla, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerZryachiy, EPlayerSide.Savage },
	// 	{ WildSpawnType.marksman, EPlayerSide.Savage },
	// 	{ WildSpawnType.pmcBot, EPlayerSide.Savage },
	// 	{ WildSpawnType.sectantPriest, EPlayerSide.Savage },
	// 	{ WildSpawnType.sectantWarrior, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerBigPipe, EPlayerSide.Savage },
	// 	{ WildSpawnType.followerBirdEye, EPlayerSide.Savage },
	// 	{ WildSpawnType.bossKnight, EPlayerSide.Savage },
	// };
	
	private GameWorld _gameWorld;
	private Player _mainPlayer;

	private BotsController _botsController;
	private BotSpawner _eftBotSpawner;
	
	private CancellationToken _onDestroyToken;
	private DonutsGizmos _donutsGizmos;

	private bool _hasSpawnedStartingBots;
	private float _replenishBotDataPrevTime;
	private float _botSpawnPrevTime;
	
	private readonly Dictionary<IBotSpawnService, List<BotWave>.Enumerator> _botWavesToSpawn = [];

	//private Dictionary<string, WildSpawnType> OriginalBotSpawnTypes;

	public BotConfigService BotConfigService { get; private set; }
	public Dictionary<DonutsSpawnType, IBotDataService> BotDataServices { get; } = new();
	public Dictionary<DonutsSpawnType, IBotSpawnService> BotSpawnServices { get; } = new();

	internal static ManualLogSource Logger { get; }
	
	internal static bool IsBotSpawningEnabled =>
		(bool)ReflectionHelper.BotsController_botEnabled_Field.GetValue(Singleton<IBotGame>.Instance.BotsController);

	public static bool IsBotPreparationComplete { get; private set; }
	//internal static List<List<Entry>> groupedFightLocations { get; set; } = [];

	static DonutsRaidManager()
	{
		Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsRaidManager));
	}

	public override void Awake()
	{
		if (!IsBotSpawningEnabled)
		{
			Destroy(this);
		}
		
		IsBotPreparationComplete = false;
		base.Awake();
		
		ModulePatchManager.EnablePatch<StartSpawningRaidManagerPatch>();
		
		// TODO: In future release, make services for Bosses, special bots, and event bots. SWAG will become obsolete.

		_gameWorld = Singleton<GameWorld>.Instance;
		_mainPlayer = _gameWorld.MainPlayer;
		
		_onDestroyToken = this.GetCancellationTokenOnDestroy();
		_donutsGizmos = new DonutsGizmos(_onDestroyToken);
		
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_eftBotSpawner = _botsController.BotSpawner;
		
		BotConfigService = BotConfigService.Create(Logger);
	}

	private void Start()
	{
		_eftBotSpawner.OnBotCreated += EftBotSpawner_OnBotCreated;
		_eftBotSpawner.OnBotRemoved += EftBotSpawner_OnBotRemoved;
		_eftBotSpawner.OnBotRemoved += ClearBotOwnerData;
		foreach (Player player in BotConfigService.GetHumanPlayerList())
		{
			if (player.OrNull()?.HealthController?.IsAlive == true)
			{
				player.BeingHitAction += TakingDamageCombatCooldown;
				player.OnPlayerDeadOrUnspawn += DisposePlayerSubscriptions;
			}
		}
	}

	public static async Task Initialize()
	{
#if DEBUG
		Logger.LogDebug("Started initializing Donuts Raid Manager");
#endif
		if (Instance == null)
		{
			return;
		}
		
		if (!await Instance.TryCreateDataServices())
		{
			ModulePatchManager.DisablePatch<StartSpawningRaidManagerPatch>();
		}
		
		IsBotPreparationComplete = true;
#if DEBUG
		Logger.LogDebug("Finished initializing Donuts Raid Manager");
#endif
	}

	private void Update()
	{
		float deltaTime = Time.deltaTime;
		_donutsGizmos.DisplayMarkerInformation(_mainPlayer.Transform);

		if (!_hasSpawnedStartingBots) return;
		
		foreach (IBotSpawnService spawnService in BotSpawnServices.Values)
		{
			spawnService.FrameUpdate(deltaTime);
		}
	}

	private void OnGUI()
	{
		_donutsGizmos.ToggleGizmoDisplay(DefaultPluginVars.DebugGizmos.Value);
	}

	public override void OnDestroy()
	{
		_donutsGizmos.Dispose();
		
		if (_eftBotSpawner != null)
		{
			_eftBotSpawner.OnBotRemoved -= EftBotSpawner_OnBotRemoved;
			_eftBotSpawner.OnBotRemoved -= ClearBotOwnerData;
			_eftBotSpawner.OnBotCreated -= EftBotSpawner_OnBotCreated;
		}
		
		base.OnDestroy();
		IsBotPreparationComplete = false;
#if DEBUG
		Logger.LogDebug($"{nameof(DonutsRaidManager)} component cleaned up and disabled.");
#endif
	}

	public static void Enable()
	{
		if (!Singleton<GameWorld>.Instantiated)
		{
			Logger.LogError($"{nameof(Enable)}: GameWorld is null. Failed to enable component");
			return;
		}
		
		if (!Instantiated)
		{
			new GameObject(nameof(DonutsRaidManager)).AddComponent<DonutsRaidManager>();
		}
#if DEBUG
		Logger.LogDebug($"{nameof(DonutsRaidManager)} component enabled");
#endif
	}

	public async UniTask StartBotSpawnController()
	{
		CreateSpawnServices();
		await SpawnStartingBots();

		float currentTime = Time.time;
		_replenishBotDataPrevTime = currentTime;
		_botSpawnPrevTime = currentTime;

		UniTaskAsyncEnumerable.EveryUpdate(cancelImmediately: true)
			// TODO: Use 'await foreach' instead once we get C# 8.0 with Unity 2022 update
			.ForEachAwaitAsync(async _ => await UpdateAsync(), _onDestroyToken)
			.Forget();
	}

	public void UpdateReplenishBotDataTime()
	{
		_replenishBotDataPrevTime = Time.time;
	}

	private static async UniTask UpdateAsync()
	{
		DonutsRaidManager raidManager = Instance;
		if (raidManager == null) return;
		
		if (Time.time >= raidManager._replenishBotDataPrevTime + DefaultPluginVars.replenishInterval.Value)
		{
			await raidManager.ReplenishBotData();
			raidManager.UpdateReplenishBotDataTime();
		}

		if (Time.time >= raidManager._botSpawnPrevTime + 5f)
		{
			await raidManager.StartSpawnProcess();
			raidManager._botSpawnPrevTime = Time.time;
		}
	}

	private async UniTask<bool> TryCreateDataServices()
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		List<UniTask> tasks = [];
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			UniTask<PmcBotDataService> task =
				BotDataService.Create<PmcBotDataService>(BotConfigService, Logger, _onDestroyToken);
			tasks.Add(task);
		}

		if (forceAllBotType is "SCAV" or "Disabled")
		{
			UniTask<ScavBotDataService> task =
				BotDataService.Create<ScavBotDataService>(BotConfigService, Logger, _onDestroyToken);
			tasks.Add(task);
		}
		
		await UniTask.WhenAll(tasks);
		return !_onDestroyToken.IsCancellationRequested;
	}

	private void CreateSpawnServices()
	{
		if (BotDataServices.TryGetValue(DonutsSpawnType.Pmc, out IBotDataService pmcDataService))
		{
			BotSpawnService.Create<PmcBotSpawnService>(BotConfigService, pmcDataService, _eftBotSpawner, Logger,
				_onDestroyToken);
		}

		if (BotDataServices.TryGetValue(DonutsSpawnType.Scav, out IBotDataService scavDataService))
		{
			BotSpawnService.Create<ScavBotSpawnService>(BotConfigService, scavDataService, _eftBotSpawner, Logger,
				_onDestroyToken);
		}
	}

	private async UniTask SpawnStartingBots()
	{
		try
		{
#if DEBUG
			Logger.LogDebug($"{nameof(SpawnStartingBots)} Begun");
#endif
			foreach (IBotSpawnService service in BotSpawnServices.Values)
			{
				await service.SpawnStartingBots();
			}
			_hasSpawnedStartingBots = true;
#if DEBUG
			Logger.LogDebug($"{nameof(SpawnStartingBots)} Finished");
#endif
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(nameof(DonutsRaidManager), nameof(SpawnStartingBots), ex);
		}
		catch (OperationCanceledException) {}
	}

	private async UniTask ReplenishBotData()
	{
		try
		{
#if DEBUG
			Logger.LogDebug($"{nameof(ReplenishBotData)} Begun");
#endif
			foreach (IBotDataService service in BotDataServices.Values)
			{
				await service.ReplenishBotData();
			}
#if DEBUG
			Logger.LogDebug($"{nameof(ReplenishBotData)} Finished");
#endif
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(nameof(DonutsRaidManager), nameof(ReplenishBotData), ex);
		}
		catch (OperationCanceledException) {}
	}

	private async UniTask StartSpawnProcess()
	{
		try
		{
#if DEBUG
			Logger.LogDebug($"{nameof(StartSpawnProcess)} Begun");
#endif
			List<IBotSpawnService> spawnServices = BotSpawnServices.Values.ShuffleElements();
			var hasDespawnedBot = false;
			foreach (IBotSpawnService service in spawnServices)
			{
				// Only despawn if a bot hasn't been despawned during this current spawn process
				if (!hasDespawnedBot)
				{
					hasDespawnedBot = service.TryDespawnFurthestBot();
				}

				// Preparation for bot wave spawning
				_botWavesToSpawn.Add(service, service.GetBotWavesToSpawn().GetEnumerator());
			}

			bool anySpawned = await SpawnBotWaves();


			if (!anySpawned)
			{
				await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: _onDestroyToken);
			}
#if DEBUG
			Logger.LogDebug($"{nameof(StartSpawnProcess)} Finished");
#endif
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(nameof(DonutsRaidManager), nameof(StartSpawnProcess), ex);
		}
		catch (OperationCanceledException) {}
		finally
		{
			DisposeBotWaveCache();
		}
	}

	private async UniTask<bool> SpawnBotWaves()
	{
		var anySpawned = false;
		while (TryGetNextBotWave())
		{
			List<IBotSpawnService> spawnServices = _botWavesToSpawn.Keys.ShuffleElements();
			foreach (IBotSpawnService service in spawnServices)
			{
				BotWave wave = _botWavesToSpawn[service].Current;
				if (wave == null) continue;

				if (await service.TrySpawnBotWave(wave) && !anySpawned)
				{
					anySpawned = true;
				}
			}
		}
		return anySpawned;
	}

	private bool TryGetNextBotWave()
	{
		var hasWavesToSpawn = false;
		foreach (IBotSpawnService service in _botWavesToSpawn.Keys)
		{
			if (_botWavesToSpawn[service].MoveNext() & !hasWavesToSpawn)
			{
				hasWavesToSpawn = true;
			}
		}
		return hasWavesToSpawn;
	}

	private void DisposeBotWaveCache()
	{
		foreach (List<BotWave>.Enumerator waveEnumerator in _botWavesToSpawn.Values)
		{
			waveEnumerator.Dispose();
		}
		_botWavesToSpawn.Clear();
	}

	private static void EftBotSpawner_OnBotCreated(BotOwner bot)
	{
		bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
	}

	private static void EftBotSpawner_OnBotRemoved(BotOwner bot)
	{
		bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
		//OriginalBotSpawnTypes.Remove(bot.Profile.Id);
	}

	private static void Memory_OnGoalEnemyChanged(BotOwner bot)
	{
		if (bot.Memory?.GoalEnemy == null) return;
		
		DonutsRaidManager raidManager = Instance;
		if (raidManager == null) return;
			
		BotMemoryClass memory = bot.Memory;
		EnemyInfo goalEnemy = memory.GoalEnemy;
		List<Player> humanPlayers = raidManager.BotConfigService.GetHumanPlayerList();
		for (int i = humanPlayers.Count - 1; i >= 0; i--)
		{
			Player player = humanPlayers[i];
			if (player == null || player.HealthController == null || player.HealthController.IsAlive == false)
			{
				continue;
			}

			if (memory.HaveEnemy &&
				// Ignore this unintended reference comparison warning, it just doesn't understand Unity's greatness
				goalEnemy.Person == player.InteractablePlayer &&
				goalEnemy.HaveSeenPersonal &&
				goalEnemy.IsVisible)
			{
#if DEBUG
				using (var sb = ZString.CreateUtf8StringBuilder())
				{
					sb.AppendFormat("Bot {0} changed target to player {1}. Resetting ReplenishBotData timer!",
						bot.Profile.Info.Nickname, player.Profile.Nickname);
					Logger.LogDebug(sb.ToString());
				}
#endif
				raidManager.UpdateReplenishBotDataTime();
				break;
			}
		}
	}

	private static void ClearBotOwnerData(BotOwner botToRemove)
	{
		List<Player> humanPlayerList = Singleton<DonutsRaidManager>.Instance.BotConfigService.GetHumanPlayerList();
		foreach (Player humanPlayer in humanPlayerList)
		{
			if (humanPlayer.OrNull()?.HealthController?.IsAlive == true)
			{
				botToRemove.Memory.DeleteInfoAboutEnemy(humanPlayer);
			}
		}

		botToRemove.EnemiesController.EnemyInfos.Clear();

		List<Player> allAlivePlayers = Singleton<GameWorld>.Instance.AllAlivePlayersList;
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			if (player == null || !player.IsAI || player.AIData.BotOwner == botToRemove)
			{
				continue;
			}

			BotOwner botOwner = player.AIData.BotOwner;
			botOwner.Memory.DeleteInfoAboutEnemy(botToRemove);
			botOwner.BotsGroup.RemoveInfo(botToRemove);
			botOwner.BotsGroup.RemoveEnemy(botToRemove, EBotEnemyCause.death);
			botOwner.BotsGroup.RemoveAlly(botToRemove);
		}
	}

	private static void TakingDamageCombatCooldown(DamageInfoStruct info, EBodyPart part, float arg3)
	{
		switch (info.DamageType)
		{
			case EDamageType.Btr:
			case EDamageType.Melee:
			case EDamageType.Bullet:
			case EDamageType.Explosion:
			case EDamageType.GrenadeFragment:
			case EDamageType.Sniper:
				DonutsRaidManager raidManager = Singleton<DonutsRaidManager>.Instance;
				if (raidManager == null) return;
				
				raidManager.UpdateReplenishBotDataTime();
				foreach (IBotSpawnService service in raidManager.BotSpawnServices.Values)
				{
					service.RestartPlayerHitTimer();
				}
				break;
		}
	}

	private static void DisposePlayerSubscriptions(Player player)
	{
		player.BeingHitAction -= TakingDamageCombatCooldown;
		player.OnPlayerDeadOrUnspawn -= DisposePlayerSubscriptions;
	}
}