using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Donuts.Models;
using Donuts.Tools;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using UnityEngine;
using UnityToolkit.Structures.EventBus;

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
	private EventBusInitializer _eventBusInitializer;
	
	private readonly TimeSpan _spawnInterval = TimeSpan.FromSeconds(1f);
	private readonly TimeSpan _delayBeforeStartingBotsSpawn = TimeSpan.FromSeconds(3f);
	
	private bool _hasSpawnedStartingBots;
	
	private bool _isReplenishBotDataOngoing;
	
	private bool _isSpawnProcessOngoing;
	private float _botSpawnPrevTime;
	
	private readonly Dictionary<IBotSpawnService, Queue<BotWave>> _botWavesToSpawn = [];
	
	public BotConfigService BotConfigService { get; private set; }
	public Dictionary<DonutsSpawnType, IBotDataService> BotDataServices { get; } = new();
	public Dictionary<DonutsSpawnType, IBotSpawnService> BotSpawnServices { get; } = new();
	
	internal static ManualLogSource Logger { get; }
	
	internal static bool IsBotSpawningEnabled
    {
        get
        {
            bool value = false;
            if (Singleton<IBotGame>.Instance != null && Singleton<IBotGame>.Instance.BotsController != null)
            {
                value = Singleton<IBotGame>.Instance.BotsController.IsEnable;
            }
            
            return value;
        }
    }
	
	public static bool CanStartRaid { get; private set; }
	//internal static List<List<Entry>> groupedFightLocations { get; set; } = [];
	
	static DonutsRaidManager()
	{
		Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsRaidManager));
	}
	
	public override void Awake()
	{
		if (!IsBotSpawningEnabled)
		{
#if DEBUG
            Logger.LogInfo("Bot spawning disabled, skipping DonutsRaidManager::Awake()"); 
#endif
            Destroy(this);
		}
		
		CanStartRaid = false;
		base.Awake();
		
		// TODO: In future release, make services for Bosses, special bots, and event bots. SWAG will become obsolete.
		
		_gameWorld = Singleton<GameWorld>.Instance;
		_mainPlayer = _gameWorld.MainPlayer;
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_eftBotSpawner = _botsController.BotSpawner;
		
		_onDestroyToken = this.GetCancellationTokenOnDestroy();
		_donutsGizmos = new DonutsGizmos(_onDestroyToken);
		_eventBusInitializer = new EventBusInitializer(DonutsPlugin.CurrentAssembly);
		_eventBusInitializer.Initialize();
		
		BotConfigService = BotConfigService.Create(Logger);
	}
	
	// ReSharper disable once Unity.IncorrectMethodSignature
	[UsedImplicitly]
	private async UniTaskVoid Start()
	{
		_eftBotSpawner.OnBotCreated += EftBotSpawner_OnBotCreated;
		_eftBotSpawner.OnBotRemoved += EftBotSpawner_OnBotRemoved;
		_gameWorld.OnPersonAdd += SubscribeHumanPlayerEventHandlers;
		
		await Initialize();
	}
	
	private void Update()
	{
		float deltaTime = Time.deltaTime;
		_donutsGizmos.DisplayMarkerInformation(_mainPlayer.Transform);

		if (!_hasSpawnedStartingBots) return;
		
		EventBus<BotDataService.UpdateWaveTimerEvent>.Raise(new BotDataService.UpdateWaveTimerEvent(deltaTime));
	}
	
	private void OnGUI()
	{
		_donutsGizmos.ToggleGizmoDisplay(DefaultPluginVars.DebugGizmos.Value);
	}
	
	public override void OnDestroy()
	{
		_donutsGizmos?.Dispose();
		
		if (_gameWorld != null)
		{
			_gameWorld.OnPersonAdd -= SubscribeHumanPlayerEventHandlers;
		}
		
		if (_eftBotSpawner != null)
		{
			_eftBotSpawner.OnBotRemoved -= EftBotSpawner_OnBotRemoved;
			_eftBotSpawner.OnBotCreated -= EftBotSpawner_OnBotCreated;
		}
		
		_eventBusInitializer.ClearAllBuses();
		
		base.OnDestroy();
		CanStartRaid = false;
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
	
	public async UniTask Initialize()
	{
#if DEBUG
		Logger.LogDebug("Started initializing Donuts Raid Manager");
#endif
		if (!await TryCreateDataServices())
		{
			Logger.NotifyLogError("Donuts: Failed to initialize Donuts Raid Manager, disabling Donuts for this raid.");
			Destroy(this);
			CanStartRaid = true;
			return;
		}
		
		CreateSpawnServices();
		CanStartRaid = true;
#if DEBUG
		Logger.LogDebug("Finished initializing Donuts Raid Manager");
#endif
	}
	
	public async UniTaskVoid StartBotSpawnController()
	{
		await SpawnStartingBots();
		
		_botSpawnPrevTime = Time.time;
		
		UniTaskAsyncEnumerable.EveryUpdate(PlayerLoopTiming.LastUpdate)
			// Updates are skipped while a task is being awaited within ForEachAwaitAsync()
			// TODO: Use 'await foreach' instead once we get C# 8.0 with Unity 2022 update
			.ForEachAwaitAsync(async _ => await UpdateAsync(), _onDestroyToken)
			.Forget();
	}
	
	private static async UniTask UpdateAsync()
	{
		DonutsRaidManager raidManager = Instance;
		if (raidManager == null) return;
		
		if (!raidManager._isReplenishBotDataOngoing)
		{
			await raidManager.ReplenishBotCache();
		}
		
		if (!raidManager._isSpawnProcessOngoing &&
			Time.time >= raidManager._botSpawnPrevTime + raidManager._spawnInterval.Seconds)
		{
			await raidManager.StartSpawnProcess();
		}
	}
	
	private async UniTask<bool> TryCreateDataServices()
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			await BotDataService.Create<PmcBotDataService>(BotConfigService, Logger, _onDestroyToken);
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			await BotDataService.Create<ScavBotDataService>(BotConfigService, Logger, _onDestroyToken);
		}
		
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
			await UniTask.Delay(_delayBeforeStartingBotsSpawn, cancellationToken: _onDestroyToken);
			foreach (IBotSpawnService service in BotSpawnServices.Values)
			{
				await service.SpawnStartingBots();
			}
			_hasSpawnedStartingBots = true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(nameof(DonutsRaidManager), nameof(SpawnStartingBots), ex);
		}
		catch (OperationCanceledException) {}
	}

	private async UniTask ReplenishBotCache()
	{
		try
		{
			_isReplenishBotDataOngoing = true;
			foreach (IBotDataService service in BotDataServices.Values)
			{
				await service.ReplenishBotCache();
			}
			_isReplenishBotDataOngoing = false;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(nameof(DonutsRaidManager), nameof(ReplenishBotCache), ex);
		}
		catch (OperationCanceledException) {}
	}
	
	private async UniTask StartSpawnProcess()
	{
		try
		{
			_isSpawnProcessOngoing = true;
			
			// Despawn excess bots
			foreach (IBotSpawnService service in BotSpawnServices.Values.ShuffleElements())
			{
				await service.DespawnExcessBots();
			}
			
			// Preparation for bot wave spawning
			foreach (IBotDataService dataService in BotDataServices.Values.ShuffleElements())
			{
				IBotSpawnService spawnService = BotSpawnServices[dataService.SpawnType];
				
				if (!_botWavesToSpawn.ContainsKey(spawnService))
				{
					_botWavesToSpawn.Add(spawnService, dataService.GetBotWavesToSpawn());
				}
			}
			
			// Spawn all queued bot waves
			await SpawnBotWaves();
			
			_botSpawnPrevTime = Time.time;
			_isSpawnProcessOngoing = false;
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
		var hasWavesToSpawn = true;
		while (!_onDestroyToken.IsCancellationRequested && hasWavesToSpawn)
		{
			hasWavesToSpawn = false;
			List<IBotSpawnService> shuffledServices = _botWavesToSpawn.Keys.ShuffleElements();
			foreach (IBotSpawnService service in shuffledServices)
			{
				Queue<BotWave> waveQueue = _botWavesToSpawn[service];
				if (waveQueue.Count == 0) continue;
				
				BotWave wave = waveQueue.Dequeue();
				
				if (await service.TrySpawnBotWave(wave))
				{
					anySpawned = true;
				}
				
				if (waveQueue.Count > 0)
				{
					hasWavesToSpawn = true;
				}
			}
		}
		return anySpawned;
	}
	
	private void DisposeBotWaveCache()
	{
		_botWavesToSpawn.Clear();
	}
	
	private static void EftBotSpawner_OnBotCreated(BotOwner bot)
	{
		bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
	}
	
	private static void EftBotSpawner_OnBotRemoved(BotOwner bot)
	{
		bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
	}
	
	private static void Memory_OnGoalEnemyChanged(BotOwner bot)
	{
		if (bot.Memory?.GoalEnemy == null) return;
		
		DonutsRaidManager raidManager = Instance;
		if (raidManager == null) return;
			
		BotMemoryClass memory = bot.Memory;
		EnemyInfo goalEnemy = memory.GoalEnemy;
		ReadOnlyCollection<Player> humanPlayers = raidManager.BotConfigService.GetHumanPlayerList();
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
				using (Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder())
				{
					sb.AppendFormat("Bot {0} changed target to player {1}. Resetting ReplenishBotData timer!",
						bot.Profile.Info.Nickname, player.Profile.Nickname);
					Logger.LogDebug(sb.ToString());
				}
#endif
				EventBus<BotDataService.ResetReplenishTimerEvent>.Raise(new BotDataService.ResetReplenishTimerEvent());
				break;
			}
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
				EventBus<BotDataService.ResetReplenishTimerEvent>.Raise(new BotDataService.ResetReplenishTimerEvent());
				EventBus<BotSpawnService.ResetPlayerCombatTimerEvent>.Raise(new BotSpawnService.ResetPlayerCombatTimerEvent());
				break;
		}
	}
	
	private static void SubscribeHumanPlayerEventHandlers(IPlayer player)
	{
		var humanPlayer = (Player)player;
		
		if (humanPlayer && !humanPlayer.IsAI && humanPlayer.HealthController?.IsAlive == true)
		{
			humanPlayer.BeingHitAction += TakingDamageCombatCooldown;
			humanPlayer.OnPlayerDeadOrUnspawn += DisposePlayerSubscriptions;
		}
	}
	
	private static void DisposePlayerSubscriptions(Player player)
	{
		player.BeingHitAction -= TakingDamageCombatCooldown;
		player.OnPlayerDeadOrUnspawn -= DisposePlayerSubscriptions;
	}
}