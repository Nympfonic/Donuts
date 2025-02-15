using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Donuts.Spawning;
using Donuts.Spawning.Processors;
using Donuts.Spawning.Services;
using Donuts.Tools;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using UnityEngine;
using UnityToolkit.Structures.DependencyInjection;
using UnityToolkit.Structures.EventBus;

#pragma warning disable CS0252, CS0253

namespace Donuts;

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

	private TarkovApplication _tarkovApplication;
	private GameWorld _gameWorld;
	private Player _mainPlayer;
	private BotsController _botsController;
	private BotSpawner _eftBotSpawner;
	
	private DiContainer _dependencyContainer;
	private CancellationToken _onDestroyToken;
	private DonutsGizmos _donutsGizmos;
	private EventBusInitializer _eventBusInitializer;
	
	private const int INITIAL_SERVICES_COUNT = 5;
	private const float SPAWN_INTERVAL_SECONDS = 1f;
	private const int MS_DELAY_BETWEEN_SPAWNS = 500;
	private const int MS_DELAY_BEFORE_STARTING_BOTS_SPAWN = 2000;

	internal const string PMC_SERVICE_KEY = "Pmc";
	internal const string SCAV_SERVICE_KEY = "Scav";
	
	private bool _hasSpawnedStartingBots;
	private bool _isStartingBotSpawnOngoing;
	private float _startingSpawnPrevTime;
	
	private bool _isReplenishBotDataOngoing;
	
	private bool _isSpawnProcessOngoing;
	private float _waveSpawnPrevTime;
	
	private readonly List<IBotDataService> _botDataServices = new(INITIAL_SERVICES_COUNT);
	private readonly List<IBotSpawnService> _botSpawnServices = new(INITIAL_SERVICES_COUNT);
	private readonly List<IBotDespawnService> _botDespawnServices = new(INITIAL_SERVICES_COUNT);

	public BotConfigService BotConfigService { get; private set; }
	
	public static bool CanStartRaid { get; private set; }
	
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
	
	//internal static List<List<Entry>> groupedFightLocations { get; set; } = [];
	
	static DonutsRaidManager()
	{
		Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsRaidManager));
	}
	
	public override void Awake()
	{
		if (!IsBotSpawningEnabled)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				Logger.LogDebugDetailed("Bot spawning disabled, skipping DonutsRaidManager::Awake()",
					nameof(DonutsRaidManager), nameof(Awake));
			}
			
            Destroy(this);
		}
		
		CanStartRaid = false;
		base.Awake();
		
		_tarkovApplication = (TarkovApplication)Singleton<ClientApplication<ISession>>.Instance;
		_gameWorld = Singleton<GameWorld>.Instance;
		_mainPlayer = _gameWorld.MainPlayer;
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_eftBotSpawner = _botsController.BotSpawner;
		
		_dependencyContainer = new DiContainer();
		RegisterServices();
		
		_onDestroyToken = this.GetCancellationTokenOnDestroy();
		_donutsGizmos = new DonutsGizmos(_onDestroyToken);
		_eventBusInitializer = new EventBusInitializer(DonutsPlugin.CurrentAssembly);
		_eventBusInitializer.Initialize();
		
		BotConfigService = _dependencyContainer.Resolve<BotConfigService>();
	}
	
	private void RegisterServices()
	{
		// TODO: In future release, make services for Bosses, special bots, and event bots. SWAG will become obsolete.
		_dependencyContainer.AddSingleton<BotConfigService, BotConfigService>();
		
		_dependencyContainer.AddSingleton<IBotDataService, PmcDataService>(PMC_SERVICE_KEY);
		_dependencyContainer.AddSingleton<IBotSpawnService, PmcSpawnService>(PMC_SERVICE_KEY);
		_dependencyContainer.AddSingleton<IBotDespawnService, PmcDespawnService>(PMC_SERVICE_KEY);
		
		_dependencyContainer.AddSingleton<IBotDataService, ScavDataService>(SCAV_SERVICE_KEY);
		_dependencyContainer.AddSingleton<IBotSpawnService, ScavSpawnService>(SCAV_SERVICE_KEY);
		_dependencyContainer.AddSingleton<IBotDespawnService, ScavDespawnService>(SCAV_SERVICE_KEY);
	}
	
	// ReSharper disable once Unity.IncorrectMethodSignature
	[UsedImplicitly]
	private async UniTaskVoid Start()
	{
		_gameWorld.OnPersonAdd += SubscribeHumanPlayerEventHandlers;
		_eftBotSpawner.OnBotCreated += EftBotSpawner_OnBotCreated;
		_eftBotSpawner.OnBotRemoved += EftBotSpawner_OnBotRemoved;
		
		await Initialize();
	}
	
	private void Update()
	{
		float deltaTime = Time.deltaTime;
		_donutsGizmos.DisplayMarkerInformation(_mainPlayer.Transform);
		EventBus.Raise(new BotDataService.UpdateWaveTimerEvent(deltaTime));
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
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			Logger.LogDebugDetailed("Raid manager cleaned up and disabled.", nameof(DonutsRaidManager), nameof(OnDestroy));
		}
	}
	
	public static void Enable()
	{
		if (!Singleton<GameWorld>.Instantiated)
		{
			Logger.LogError("GameWorld is null. Failed to enable raid manager");
			return;
		}
		
		if (!Instantiated)
		{
			new GameObject(nameof(DonutsRaidManager)).AddComponent<DonutsRaidManager>();
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			Logger.LogDebugDetailed("Raid manager enabled", nameof(DonutsRaidManager), nameof(Enable));
		}
	}
	
	private async UniTask Initialize()
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			Logger.LogDebugDetailed("Started initializing raid manager", nameof(DonutsRaidManager), nameof(Initialize));
		}
		
		if (!await TryCreateDataServices())
		{
			DonutsHelper.NotifyLogError("Donuts: Failed to initialize Donuts Raid Manager, disabling Donuts for this raid.");
			Destroy(this);
			CanStartRaid = true;
			return;
		}
		
		CreateSpawnServices();
		CanStartRaid = true;
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			Logger.LogDebugDetailed("Finished initializing raid manager", nameof(DonutsRaidManager), nameof(Initialize));
		}
	}
	
	public async UniTaskVoid StartBotSpawnController()
	{
		await UniTask.Delay(MS_DELAY_BEFORE_STARTING_BOTS_SPAWN, cancellationToken: _onDestroyToken);
		
		_waveSpawnPrevTime = Time.time;
		
		UniTaskAsyncEnumerable.EveryUpdate()
			// Updates are skipped while a task is being awaited within ForEachAwaitAsync()
			// TODO: Use 'await foreach' instead once we get C# 8.0 in SPT 3.11
			.ForEachAwaitAsync(async _ => await UpdateAsync(), _onDestroyToken)
			.Forget();
	}
	
	private static async UniTask UpdateAsync()
	{
		DonutsRaidManager raidManager = Instance;
		if (raidManager == null) return;

		if (!raidManager._hasSpawnedStartingBots &&
			!raidManager._isStartingBotSpawnOngoing &&
			Time.time >= raidManager._startingSpawnPrevTime + SPAWN_INTERVAL_SECONDS)
		{
			await raidManager.SpawnStartingBots();
		}
		
		if (!raidManager._isReplenishBotDataOngoing)
		{
			await raidManager.ReplenishBotCache();
		}
		
		if (!raidManager._isSpawnProcessOngoing &&
			Time.time >= raidManager._waveSpawnPrevTime + SPAWN_INTERVAL_SECONDS)
		{
			await raidManager.StartSpawnProcess();
		}
	}
	
	private async UniTask<bool> TryCreateDataServices()
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			var pmcDataService = _dependencyContainer.Resolve<IBotDataService>(PMC_SERVICE_KEY);
			await pmcDataService.SetupStartingBotCache(_onDestroyToken);
			_botDataServices.Add(pmcDataService);
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			var scavDataService = _dependencyContainer.Resolve<IBotDataService>(SCAV_SERVICE_KEY);
			await scavDataService.SetupStartingBotCache(_onDestroyToken);
			_botDataServices.Add(scavDataService);
		}
		
		return !_onDestroyToken.IsCancellationRequested && _botDataServices.Count > 0;
	}
	
	private void CreateSpawnServices()
	{
		var pmcSpawnService = _dependencyContainer.Resolve<IBotSpawnService>(PMC_SERVICE_KEY);
		var pmcDespawnService = _dependencyContainer.Resolve<IBotDespawnService>(PMC_SERVICE_KEY);
		_botSpawnServices.Add(pmcSpawnService);
		_botDespawnServices.Add(pmcDespawnService);
		
		var scavSpawnService = _dependencyContainer.Resolve<IBotSpawnService>(SCAV_SERVICE_KEY);
		var scavDespawnService = _dependencyContainer.Resolve<IBotDespawnService>(SCAV_SERVICE_KEY);
		_botSpawnServices.Add(scavSpawnService);
		_botDespawnServices.Add(scavDespawnService);
	}
	
	private async UniTask SpawnStartingBots()
	{
		try
		{
			_isStartingBotSpawnOngoing = true;
			_startingSpawnPrevTime = Time.time;
			
			_hasSpawnedStartingBots = true;
			for (int i = _botSpawnServices.Count - 1; i >= 0; i--)
			{
				IBotSpawnService service = _botSpawnServices[i];
				if (!await service.SpawnStartingBots(_onDestroyToken))
				{
					_hasSpawnedStartingBots = false;
				}
			}
			
			_isStartingBotSpawnOngoing = false;
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
			
			for (int i = _botDataServices.Count - 1; i >= 0; i--)
			{
				IBotDataService service = _botDataServices[i];
				await service.ReplenishBotCache(_onDestroyToken);
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
			_waveSpawnPrevTime = Time.time;
			
			// Spawn all queued bot waves
			var hasWavesToSpawn = true;
			while (hasWavesToSpawn && !_onDestroyToken.IsCancellationRequested)
			{
				hasWavesToSpawn = false;
				
				// TODO: Separate starting bot spawning into its own list and service so it's not affected by this shuffle
				for (int i = _botSpawnServices.Count - 1; i >= 0; i--)
				{
					IBotSpawnService service = _botSpawnServices[i];
					if (!await service.TrySpawnBotWave(_onDestroyToken) || _onDestroyToken.IsCancellationRequested)
					{
						continue;
					}
					
					hasWavesToSpawn = true;
					
					if (i > 0)
					{
						await UniTask.Delay(MS_DELAY_BETWEEN_SPAWNS, cancellationToken: _onDestroyToken);
					}
				}
				
				if (hasWavesToSpawn)
				{
					await UniTask.Delay(MS_DELAY_BETWEEN_SPAWNS, cancellationToken: _onDestroyToken);
				}
			}
			
			// Despawn excess bots
			for (int i = _botDespawnServices.Count - 1; i >= 0; i--)
			{
				IBotDespawnService service = _botDespawnServices[i];
				await service.DespawnExcessBots(_onDestroyToken);
			}
			
			_isSpawnProcessOngoing = false;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(nameof(DonutsRaidManager), nameof(StartSpawnProcess), ex);
		}
		catch (OperationCanceledException) {}
	}
	
	private static void SubscribeHumanPlayerEventHandlers(IPlayer player)
	{
		var humanPlayer = (Player)player;
		
		// We don't check if it's an AI here because it doesn't have the BotOwner MonoBehaviour script at this point
		// Simply remove the subscription later in the BotSpawner::OnBotCreated event
		if (humanPlayer && humanPlayer.HealthController?.IsAlive == true)
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
	
	private static void EftBotSpawner_OnBotCreated(BotOwner bot)
	{
		bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
		// Remove these subscriptions since now it's confirmed this player is a bot and not a human player
		Player botPlayer = bot.GetPlayer;
		botPlayer.BeingHitAction -= TakingDamageCombatCooldown;
		botPlayer.OnPlayerDeadOrUnspawn -= DisposePlayerSubscriptions;
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
				goalEnemy.Person == player.InteractablePlayer &&
				goalEnemy.HaveSeenPersonal &&
				goalEnemy.IsVisible)
			{
				EventBus.Raise(new BotDataService.ResetReplenishTimerEvent());
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
				EventBus.Raise(new BotDataService.ResetReplenishTimerEvent());
				EventBus.Raise(new PlayerCombatStateCheck.ResetTimerEvent());
				break;
		}
	}
}