using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Donuts.Spawning;
using Donuts.Spawning.Controllers;
using Donuts.Spawning.Services;
using Donuts.Tools;
using Donuts.Utils;
using EFT;
using EFT.UI;
using JetBrains.Annotations;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading;
using UnityEngine;
using UnityToolkit.Structures.DependencyInjection;
using UnityToolkit.Structures.EventBus;

#pragma warning disable CS0252, CS0253

namespace Donuts;

public class DonutsRaidManager : MonoBehaviourSingleton<DonutsRaidManager>
{
	private GameWorld _gameWorld;
	private Player _mainPlayer;
	private BotsController _botsController;
	private BotSpawner _eftBotSpawner;
	
	private DiContainer _dependencyContainer;
	
	private CancellationTokenSource _onDestroyTokenSource;
	private CancellationToken _onDestroyToken;
	private TimeoutController _timeoutController;
	
	private DonutsGizmos _donutsGizmos;
	private EventBusInitializer _eventBusInitializer;
	
	private IBotDataController _botDataController;
	private IBotSpawnController _botSpawnController;
	private IBotDespawnController _botDespawnController;
	
	internal const int INITIAL_SERVICES_COUNT = 3;
	private const int MS_DELAY_BEFORE_STARTING_BOTS_SPAWN = 2000;

	internal const string PMC_SERVICE_KEY = "Pmc";
	internal const string SCAV_SERVICE_KEY = "Scav";
	
	private float _updateTimer;
	
	private bool _canStartRaid;

	public BotConfigService BotConfigService { get; private set; }
	
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
			Logger.LogDebugDetailed("Bot spawning disabled, skipping DonutsRaidManager::Awake()",
				nameof(DonutsRaidManager), nameof(Awake));
            Destroy(this);
		}
		
		base.Awake();
		
		_gameWorld = Singleton<GameWorld>.Instance;
		_mainPlayer = _gameWorld.MainPlayer;
		_botsController = Singleton<IBotGame>.Instance.BotsController;
		_eftBotSpawner = _botsController.BotSpawner;
		
		_onDestroyTokenSource = new CancellationTokenSource();
		_onDestroyToken = _onDestroyTokenSource.Token;
		_timeoutController = new TimeoutController(_onDestroyTokenSource);
		
		_dependencyContainer = new DiContainer();
		RegisterServices();
		_botDataController = new BotDataController(_dependencyContainer, _timeoutController);
		_botSpawnController = new BotSpawnController(_dependencyContainer);
		_botDespawnController = new BotDespawnController(_dependencyContainer);
		
		_donutsGizmos = new DonutsGizmos(_onDestroyToken);
		_eventBusInitializer = new EventBusInitializer(DonutsPlugin.CurrentAssembly);
		_eventBusInitializer.Initialize();
		
		_gameWorld.OnPersonAdd += SubscribeHumanPlayerEventHandlers;
		_eftBotSpawner.OnBotCreated += EftBotSpawner_OnBotCreated;
		_eftBotSpawner.OnBotRemoved += EftBotSpawner_OnBotRemoved;
		
		BotConfigService = _dependencyContainer.Resolve<BotConfigService>();
	}
	
	private void RegisterServices()
	{
		// TODO: In future release, make services for Bosses, special bots, and event bots. SWAG will become obsolete.
		_dependencyContainer.AddSingleton<BotConfigService, BotConfigService>();
		
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			_dependencyContainer.AddSingleton<IBotDataService, PmcDataService>(PMC_SERVICE_KEY);
			_dependencyContainer.AddSingleton<IBotSpawnService, PmcSpawnService>(PMC_SERVICE_KEY);
			_dependencyContainer.AddSingleton<IBotDespawnService, PmcDespawnService>(PMC_SERVICE_KEY);
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			_dependencyContainer.AddSingleton<IBotDataService, ScavDataService>(SCAV_SERVICE_KEY);
			_dependencyContainer.AddSingleton<IBotSpawnService, ScavSpawnService>(SCAV_SERVICE_KEY);
			_dependencyContainer.AddSingleton<IBotDespawnService, ScavDespawnService>(SCAV_SERVICE_KEY);
		}
	}
	
	// ReSharper disable once Unity.IncorrectMethodSignature
	[UsedImplicitly]
	private async UniTaskVoid Start()
	{
		await Initialize();
	}
	
	private void Update()
	{
		float deltaTime = Time.deltaTime;
		
		_donutsGizmos.DisplayMarkerInformation(_mainPlayer.Transform);
		
		if (_updateTimer >= 1f)
		{
			EventBus.Raise(EveryUpdateSecondEvent.Create(_updateTimer));
			_updateTimer = 0f;
		}
		
		_updateTimer += deltaTime;
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
		
		_botDataController?.Dispose();
		_eventBusInitializer?.ClearAllBuses();
		
		_onDestroyTokenSource?.Cancel();
		_onDestroyTokenSource?.Dispose();
		_timeoutController?.Dispose();
		
		base.OnDestroy();
		
		Logger.LogDebugDetailed("Raid manager cleaned up and disabled.", nameof(DonutsRaidManager), nameof(OnDestroy));
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
		
		Logger.LogDebugDetailed("Raid manager enabled", nameof(DonutsRaidManager), nameof(Enable));
	}
	
	private async UniTask Initialize()
	{
		Logger.LogDebugDetailed("Started initializing raid manager", nameof(DonutsRaidManager), nameof(Initialize));
		
		if (!await _botDataController.Initialize(_onDestroyToken))
		{
			DonutsHelper.NotifyLogError("Donuts: No data services initialized, disabling Donuts for this raid.");
			Destroy(this);
			return;
		}
		
		_botSpawnController.Initialize();
		_botDespawnController.Initialize();
		
		Logger.LogDebugDetailed("Finished initializing raid manager", nameof(DonutsRaidManager), nameof(Initialize));
		
		_canStartRaid = true;
	}
	
	internal IEnumerator DonutsRaidLoadingTask(IEnumerator startGameTask)
	{
		var botGenStatusEventBinding = new EventBinding<BotGenStatusChangeEvent>(SetMatchmakerStatus);
		EventBus.Register(botGenStatusEventBinding);
		
		Logger.LogWarning("Donuts is waiting for bot preparation to complete...");
		
		float startTime = Time.time;
		float lastLogTime = startTime;
		var waitInterval = new WaitForEndOfFrame();
		
		while (true)
		{
			// Check at end of every frame
			yield return waitInterval;
			
			if (_canStartRaid)
			{
				break;
			}
			
			float currentTime = Time.time;
			
			// Log every 2 seconds instead of every second to avoid spamming logs
			if (currentTime - lastLogTime >= 2f)
			{
				lastLogTime = currentTime;
				Logger.LogWarning("Donuts still waiting...");
			}
		}
		
		EventBus.Deregister(botGenStatusEventBinding);
		Logger.LogWarning("Donuts bot preparation is complete.");
		
		yield return startGameTask;
	}
	
	private static void SetMatchmakerStatus(BotGenStatusChangeEvent data)
	{
		if (DonutsPlugin.FikaEnabled)
		{
			Singleton<AbstractGame>.Instance.SetMatchmakerStatus(data.message, data.progress);
		}
		else
		{
			Singleton<MenuUI>.Instance.MatchmakerTimeHasCome.method_7(data.message, data.progress);
		}
	}
	
	public async UniTaskVoid StartBotSpawning()
	{
		await UniTask.Delay(MS_DELAY_BEFORE_STARTING_BOTS_SPAWN, cancellationToken: _onDestroyToken);
		if (_onDestroyToken.IsCancellationRequested) return;
		
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
		
		await raidManager._botSpawnController.SpawnStartingBots(raidManager._onDestroyToken);
		await raidManager._botDataController.ReplenishBotCache(raidManager._onDestroyToken);
		await raidManager._botSpawnController.SpawnBotWaves(raidManager._onDestroyToken);
		await raidManager._botDespawnController.DespawnExcessBots(raidManager._onDestroyToken);
	}
	
	private static void SubscribeHumanPlayerEventHandlers(IPlayer iPlayer)
	{
		var player = (Player)iPlayer;
		
		if (player)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Subscribed human player-related event handlers to player {0} ({1})'s events",
					player.Profile.Nickname, player.ProfileId);
				Logger.LogDebugDetailed(sb.ToString(), nameof(DonutsRaidManager), nameof(SubscribeHumanPlayerEventHandlers));
			}
			player.BeingHitAction += TakingDamageCombatCooldown;
			player.OnPlayerDeadOrUnspawn += DisposePlayerSubscriptions;
		}
	}
	
	private static void DisposePlayerSubscriptions(Player player)
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Unsubscribed human player-related event handlers from player {0} ({1})'s events",
				player.Profile.Nickname, player.ProfileId);
			Logger.LogDebugDetailed(sb.ToString(), nameof(DonutsRaidManager), nameof(DisposePlayerSubscriptions));
		}
		player.BeingHitAction -= TakingDamageCombatCooldown;
		player.OnPlayerDeadOrUnspawn -= DisposePlayerSubscriptions;
	}
	
	private static void EftBotSpawner_OnBotCreated(BotOwner bot)
	{
		bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Player {0} ({1}) now determined as bot, unsubscribing human player-related event handlers...",
				bot.Profile.Nickname, bot.ProfileId);
			Logger.LogDebugDetailed(sb.ToString(), nameof(DonutsRaidManager), nameof(EftBotSpawner_OnBotCreated));
		}
		DisposePlayerSubscriptions(bot.GetPlayer);
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
			if (player == null && !player.IsAlive())
			{
				continue;
			}
			
			if (memory.HaveEnemy &&
				goalEnemy.Person == player.InteractablePlayer &&
				goalEnemy.HaveSeenPersonal &&
				goalEnemy.IsVisible)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("{0} \"{1}\" changed target to human player {2}, resetting bot replenish cache timer!",
					bot.name, bot.Profile.Nickname, player.Profile.Nickname);
				Logger.LogDebugDetailed(sb.ToString(), nameof(DonutsRaidManager), nameof(Memory_OnGoalEnemyChanged));
				
				EventBus.Raise(PlayerTargetedByBotEvent.Create());
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
				EventBus.Raise(PlayerEnteredCombatEvent.Create());
				break;
		}
	}
}