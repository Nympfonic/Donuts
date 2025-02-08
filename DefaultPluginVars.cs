using Comfort.Common;
using Donuts.Bots;
using Donuts.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Donuts;

internal static class DefaultPluginVars
{
	// Main Settings
	internal static Setting<bool> PluginEnabled;
	internal static Setting<bool> DespawnEnabledPMC;
	internal static Setting<bool> DespawnEnabledSCAV;
	internal static Setting<bool> HardCapEnabled;
	internal static Setting<float> coolDownTimer;
	internal static Setting<string> pmcGroupChance;
	internal static Setting<string> scavGroupChance;
	internal static Setting<string> botDifficultiesPMC;
	internal static Setting<string> botDifficultiesSCAV;
	internal static Setting<string> botDifficultiesOther;
	internal static Setting<bool> ShowRandomFolderChoice;
	internal static Setting<string> pmcFaction;
	internal static Setting<string> forceAllBotType;

	internal static Setting<bool> useTimeBasedHardStop;
	internal static Setting<bool> hardStopOptionPMC;
	internal static Setting<int> hardStopTimePMC;
	internal static Setting<int> hardStopPercentPMC;
	internal static Setting<bool> hardStopOptionSCAV;
	internal static Setting<int> hardStopTimeSCAV;
	internal static Setting<int> hardStopPercentSCAV;

	internal static Setting<bool> hotspotBoostPMC;
	internal static Setting<bool> hotspotBoostSCAV;
	internal static Setting<bool> hotspotIgnoreHardCapPMC;
	internal static Setting<bool> hotspotIgnoreHardCapSCAV;
	internal static Setting<int> pmcFactionRatio;
	internal static Setting<float> battleStateCoolDown;
	internal static Setting<int> maxRespawnsPMC;
	internal static Setting<int> maxRespawnsSCAV;

	// Global Minimum Spawn Distance From Player
	internal static Setting<bool> globalMinSpawnDistanceFromPlayerBool;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerFactory;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerCustoms;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerReserve;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerStreets;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerWoods;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerLaboratory;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerShoreline;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerGroundZero;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerInterchange;
	internal static Setting<float> globalMinSpawnDistanceFromPlayerLighthouse;

	// Global Minimum Spawn Distance From Other Bots
	internal static Setting<bool> globalMinSpawnDistanceFromOtherBotsBool;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsFactory;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsCustoms;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsReserve;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsStreets;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsWoods;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsLaboratory;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsShoreline;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsGroundZero;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsInterchange;
	internal static Setting<float> globalMinSpawnDistanceFromOtherBotsLighthouse;

	// Advanced Settings
	internal static Setting<float> replenishInterval;
	internal static Setting<int> maxSpawnTriesPerBot;
	internal static Setting<float> despawnInterval;
	internal static Setting<string> groupWeightDistroLow;
	internal static Setting<string> groupWeightDistroDefault;
	internal static Setting<string> groupWeightDistroHigh;
	internal static Setting<float> maxRaidDelay;

	// Debugging
	internal static Setting<bool> debugLogging;
	internal static Setting<bool> DebugGizmos;
	internal static Setting<bool> gizmoRealSize;

	// Spawn Point Maker
	internal static Setting<string> spawnName;
	internal static Setting<int> groupNum;
	internal static Setting<string> wildSpawns;
	internal static Setting<float> minSpawnDist;
	internal static Setting<float> maxSpawnDist;
	internal static Setting<float> botTriggerDistance;
	internal static Setting<float> botTimerTrigger;
	internal static Setting<int> maxRandNumBots;
	internal static Setting<int> spawnChance;
	internal static Setting<int> maxSpawnsBeforeCooldown;
	internal static Setting<bool> ignoreTimerFirstSpawn;
	internal static Setting<float> minSpawnDistanceFromPlayer;
	internal static Setting<KeyCode> CreateSpawnMarkerKey;
	internal static Setting<KeyCode> DeleteSpawnMarkerKey;

	//private static readonly string[] _wildSpawnTypes = Enum.GetNames(typeof(WildSpawnType));

	private static readonly string[] _wildSpawnTypes =
	[
		"arenafighterevent",
		"assault",
		"assaultgroup",
		"bossboar",
		"bossboarsniper",
		"bossbully",
		"bossgluhar",
		"bosskilla",
		"bossknight",
		"bosskojaniy",
		"bosssanitar",
		"bosstagilla",
		"bosszryachiy",
		"crazyassaultevent",
		"cursedassault",
		"exusec-rogues",
		"raiders",
		"followerbigpipe",
		"followerbirdeye",
		"followerboar",
		"followerbully",
		"followergluharassault",
		"followergluharscout",
		"followergluharsecurity",
		"followergluharsnipe",
		"followerkojaniy",
		"followersanitar",
		"followertagilla",
		"followerzryachiy",
		"gifter",
		"marksman",
		"pmc",
		"pmcBEAR",
		"pmcUSEC",
		"sectantpriest",
		"sectantwarrior",
	];

	// Save Settings
	internal static Setting<bool> saveNewFileOnly;
	internal static Setting<KeyCode> WriteToFileKey;

	private static readonly Dictionary<string, int[]> _groupChanceWeights = new()
	{
		{ "Low", [400, 90, 9, 0, 0] },
		{ "Default", [210, 210, 45, 25, 10] },
		{ "High", [0, 75, 175, 175, 75] }
	};
	private static readonly string _defaultWeightsString = ConvertIntArrayToString(_groupChanceWeights["Default"]);
	private static readonly string _lowWeightsString = ConvertIntArrayToString(_groupChanceWeights["Low"]);
	private static readonly string _highWeightsString = ConvertIntArrayToString(_groupChanceWeights["High"]);

	private static readonly string[] _pmcGroupChanceList = ["None", "Default", "Low", "High", "Max", "Random"];
	private static readonly string[] _scavGroupChanceList = ["None", "Default", "Low", "High", "Max", "Random"];
	private static readonly string[] _pmcFactionList = ["Default", "USEC", "BEAR"];
	private static readonly string[] _forceAllBotTypeList = ["Disabled", "SCAV", "PMC"];

	public static Dictionary<string, int[]> GroupChanceWeights => _groupChanceWeights;
	
	public static BotDifficulty[] BotDifficulties { get; } =
		[BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible];

	private static string ConvertIntArrayToString(int[] array) => string.Join(",", array);

	// IMGUI Vars
	private static readonly string[] _botDifficultyOptions = ["AsOnline", "Easy", "Normal", "Hard", "Impossible"];

	internal static bool ShowGUI { get; set; }
	internal static Rect WindowRect { get; set; } = new(20, 20, 1664, 936);  // Default position and size

	// Scenario Selection
	internal static List<Folder> PmcScenarios { get; set; } = [];
	internal static List<Folder> PmcRandomScenarios { get; set; } = [];
	internal static List<Folder> ScavScenarios { get; set; } = [];
	internal static List<Folder> ScavRandomScenarios { get; set; } = [];
	
	internal static Setting<string> pmcScenarioSelection;
	internal static Setting<string> scavScenarioSelection;
	internal static string[] pmcScenarioCombinedArray;
	internal static string[] scavScenarioCombinedArray;

	// Temporarily store the scenario selections to initialize them later
	internal static string PmcScenarioSelectionValue { get; set; }
	internal static string ScavScenarioSelectionValue { get; set; }

	static DefaultPluginVars()
	{
		InitMainSettings();

		InitGlobalMinSpawnDistFromPlayer();
		InitGlobalMinSpawnDistFromOtherBots();

		InitAdvancedSettings();
		InitDebuggingSettings();
		InitSpawnPointMakerSettings();
		InitSaveSettings();
	}

	private static void InitMainSettings()
	{
		PluginEnabled = new Setting<bool>("Donuts On", "Enable/Disable Spawning from Donuts Points",
			true, true);

		PluginEnabled.OnSettingChanged += _ =>
		{
			if (MonoBehaviourSingleton<DonutsRaidManager>.Instantiated)
			{
				MonoBehaviourSingleton<DonutsRaidManager>.Instance.enabled = PluginEnabled.Value;
			}
		};

		DespawnEnabledPMC = new Setting<bool>("Despawn PMCs",
			"When enabled, removes furthest PMC bots from player for each new dynamic spawn bot that is over your Donuts bot caps (ScenarioConfig.json).",
			true, true);

		DespawnEnabledSCAV = new Setting<bool>("Despawn SCAVs",
			"When enabled, removes furthest SCAV bots from player for each new dynamic spawn bot that is over your Donuts bot caps (ScenarioConfig.json).",
			true, true);

		HardCapEnabled = new Setting<bool>("Bot Hard Cap Option",
			"When enabled, all bot spawns will be hard capped by your preset caps. In other words, if your bot count is at the total Donuts cap then no more bots will spawn until one dies (vanilla SPT behavior).",
			false, false);

		coolDownTimer = new Setting<float>("Cooldown Timer",
			"Cooldown Timer for after a spawn has successfully spawned a bot the spawn marker's MaxSpawnsBeforeCooldown",
			300f, 300f, 0f, 1000f);

		pmcGroupChance = new Setting<string>("Donuts PMC Group Chance",
			"Setting to determine the odds of PMC groups and group size. All odds are configurable, check Advanced Settings above. See mod page for more details.",
			"Default", "Default", null, null, _pmcGroupChanceList);

		scavGroupChance = new Setting<string>("Donuts SCAV Group Chance",
			"Setting to determine the odds of SCAV groups and group size. All odds are configurable, check Advanced Settings above. See mod page for more details. See mod page for more details.",
			"Default", "Default", null, null, _scavGroupChanceList);

		botDifficultiesPMC = new Setting<string>("Donuts PMC Spawn Difficulty",
			"Difficulty Setting for All PMC Donuts Related Spawns",
			"Normal", "Normal", null, null, _botDifficultyOptions);

		botDifficultiesSCAV = new Setting<string>("Donuts SCAV Spawn Difficulty",
			"Difficulty Setting for All SCAV Donuts Related Spawns",
			"Normal", "Normal", null, null, _botDifficultyOptions);

		botDifficultiesOther = new Setting<string>("Other Bot Type Spawn Difficulty",
			"Difficulty Setting for all other bot types spawned with Donuts, such as bosses, Rogues, Raiders, etc.",
			"Normal", "Normal", null, null, _botDifficultyOptions);

		ShowRandomFolderChoice = new Setting<bool>("Show Spawn Preset Selection",
			"Shows the Spawn Preset Selected on Raid Start in bottom right", true, true);

		pmcFaction = new Setting<string>("Force PMC Faction",
			"Force a specific faction for all PMC spawns or use the default specified faction in the Donuts spawn files. Default is a random faction.",
			"Default", "Default", null, null, _pmcFactionList);

		forceAllBotType = new Setting<string>("Force Bot Type for All Spawns",
			"Force a specific bot type for all spawns - this option converts all defined starting spawns and waves to the specified bot type. Default is Disabled.",
			"Disabled", "Disabled", null, null, _forceAllBotTypeList);

		useTimeBasedHardStop = new Setting<bool>("Use Time-Based Hard Stop",
			"If enabled, the hard stop settings will be the time (in seconds) left in raid (configurable below). If disabled, the hard stop settings will be the percentage of time left in raid (configurable below).",
			true, true);

		hardStopOptionPMC = new Setting<bool>("PMC Spawn Hard Stop",
			"If enabled, all PMC spawns stop completely once there is n time or percentage time left in your raid. This is configurable in either seconds or percentage (see below).",
			false, false);

		hardStopPercentPMC = new Setting<int>("PMC Spawn Hard Stop: Percent Left of Raid",
			"The percentage of time left in your raid that will stop any further PMC spawns (if option is enabled). Default is 50 percent of the full raid time.",
			50, 50, 0, 100);

		hardStopTimePMC = new Setting<int>("PMC Spawn Hard Stop: Time Left in Raid",
			"The time (in seconds) left in your raid that will stop any further PMC spawns (if option is enabled). Default is 300 (5 minutes).",
			300, 300);

		hardStopOptionSCAV = new Setting<bool>("SCAV Spawn Hard Stop",
			"If enabled, all SCAV spawns stop completely once there is n time or percentage time left in your raid. This is configurable in either seconds or percentage (see below).",
			false, false);

		hardStopTimeSCAV = new Setting<int>("SCAV Spawn Hard Stop: Time Left in Raid",
			"The time (in seconds) left in your raid that will stop any further SCAV spawns (if option is enabled). Default is 300 (5 minutes).",
			300, 300);

		hardStopPercentSCAV = new Setting<int>("SCAV Spawn Hard Stop: Percent Left of Raid",
			"The percentage of time left in your raid that will stop any further SCAV spawns (if option is enabled). Default is 10 percent of the full raid time.",
			10, 10, 0, 100);

		hotspotBoostPMC = new Setting<bool>("PMC Hot Spot Spawn Boost",
			"If enabled, all hotspot points have a much higher chance of spawning more PMCs.",
			false, false);

		hotspotBoostSCAV = new Setting<bool>("SCAV Hot Spot Spawn Boost",
			"If enabled, all hotspot points have a much higher chance of spawning more SCAVs.",
			false, false);

		hotspotIgnoreHardCapPMC = new Setting<bool>("PMC Hot Spot Ignore Hard Cap",
			"If enabled, all hotspot spawn points will ignore the hard cap (if enabled). This applies to any spawn points labeled with 'Hotspot'. I recommended using this option with Despawn + Hardcap + Boost for the best experience with more action in hot spot areas.",
			false, false);

		hotspotIgnoreHardCapSCAV = new Setting<bool>("SCAV Hot Spot Ignore Hard Cap",
			"If enabled, all hotspot spawn points will ignore the hard cap (if enabled). This applies to any spawn points labeled with 'Hotspot'. I recommended using this option with Despawn + Hardcap + Boost for the best experience with more action in hot spot areas.",
			false, false);

		pmcFactionRatio = new Setting<int>("PMC Faction Ratio",
			"USEC/Bear Default Ratio. Default is 50%. Lower value = lower USEC chance, so: 20 would be 20% USEC, 80% Bear, etc.",
			50, 50);

		battleStateCoolDown = new Setting<float>("Battlestate Spawn Cooldown",
			"It will stop spawning bots until you haven't been hit for X amount of seconds\nas you are still considered being in battle",
			20f, 20f);

		maxRespawnsPMC = new Setting<int>("Maximum number of PMC respawns per raid",
			"Once Donuts has spawned this many PMCs in a raid, it will skip all subsequent triggered PMC spawns. Default is 0 (unlimited)",
			0, 0);

		maxRespawnsSCAV = new Setting<int>("Maximum number of SCAV respawns per raid",
			"Once Donuts has spawned this many SCAVs in a raid, it will skip all subsequent triggered SCAV spawns. Default is 0 (unlimited)",
			0, 0);
	}

	private static void InitGlobalMinSpawnDistFromPlayer()
	{
		globalMinSpawnDistanceFromPlayerBool = new Setting<bool>("Use Global Min Distance From Player",
			"If enabled, all spawns on all presets will use the global minimum spawn distance from player for each map defined below.",
			true, true);
		
		const string tooltipText = "Distance (in meters) that bots should spawn away from the player (you).";

		globalMinSpawnDistanceFromPlayerFactory = new Setting<float>("Factory", tooltipText, 35f, 35f);
		globalMinSpawnDistanceFromPlayerCustoms = new Setting<float>("Customs", tooltipText, 60f, 60f);
		globalMinSpawnDistanceFromPlayerReserve = new Setting<float>("Reserve", tooltipText, 80f, 80f);
		globalMinSpawnDistanceFromPlayerStreets = new Setting<float>("Streets", tooltipText, 80f, 80f);
		globalMinSpawnDistanceFromPlayerWoods = new Setting<float>("Woods", tooltipText, 110f, 110f);
		globalMinSpawnDistanceFromPlayerLaboratory = new Setting<float>("Laboratory", tooltipText, 40f, 40f);
		globalMinSpawnDistanceFromPlayerShoreline = new Setting<float>("Shoreline", tooltipText, 100f, 100f);
		globalMinSpawnDistanceFromPlayerGroundZero = new Setting<float>("Ground Zero", tooltipText, 50f, 50f);
		globalMinSpawnDistanceFromPlayerInterchange = new Setting<float>("Interchange", tooltipText, 85f, 85f);
		globalMinSpawnDistanceFromPlayerLighthouse = new Setting<float>("Lighthouse", tooltipText, 70f, 70f);
	}

	private static void InitGlobalMinSpawnDistFromOtherBots()
	{
		globalMinSpawnDistanceFromOtherBotsBool = new Setting<bool>("Use Global Min Distance From Other Bots",
			"If enabled, all spawns on all presets will use the global minimum spawn distance from player for each map defined below.",
			true, true);

		const string tooltipText = "Distance (in meters) that bots should spawn away from other alive bots.";

		globalMinSpawnDistanceFromOtherBotsFactory = new Setting<float>("Factory", tooltipText, 15f, 15f);
		globalMinSpawnDistanceFromOtherBotsCustoms = new Setting<float>("Customs", tooltipText, 40f, 50f);
		globalMinSpawnDistanceFromOtherBotsReserve = new Setting<float>("Reserve", tooltipText, 50f, 50f);
		globalMinSpawnDistanceFromOtherBotsStreets = new Setting<float>("Streets", tooltipText, 50f, 50f);
		globalMinSpawnDistanceFromOtherBotsWoods = new Setting<float>("Woods", tooltipText, 80f, 80f);
		globalMinSpawnDistanceFromOtherBotsLaboratory = new Setting<float>("Laboratory", tooltipText, 20f, 20f);
		globalMinSpawnDistanceFromOtherBotsShoreline = new Setting<float>("Shoreline", tooltipText, 60f, 60f);
		globalMinSpawnDistanceFromOtherBotsGroundZero = new Setting<float>("Ground Zero", tooltipText, 30f, 30f);
		globalMinSpawnDistanceFromOtherBotsInterchange = new Setting<float>("Interchange", tooltipText, 65f, 65f);
		globalMinSpawnDistanceFromOtherBotsLighthouse = new Setting<float>("Lighthouse", tooltipText, 60f, 60f);
	}

	private static void InitAdvancedSettings()
	{
		maxRaidDelay = new Setting<float>("Raid Load Time Delay",
			"Max time (in seconds) that Donuts force delays raid load so it has time to generate bot data for all starting points. This is to avoid potential bot spawn delays on raid start. This may delay Default is 60 seconds.",
			60f, 60f, 0f, 120f);

		replenishInterval = new Setting<float>("Bot Cache Replenish Interval",
			"The time interval in seconds for Donuts to re-fill its bot data cache. Leave default unless you know what you're doing.",
			10f, 10f, 0f, 300f);

		maxSpawnTriesPerBot = new Setting<int>("Max Spawn Tries Per Bot",
			"It will stop trying to spawn one of the bots after this many attempts to find a good spawn point. Lower is better",
			1, 1, 0, 10);

		despawnInterval = new Setting<float>("Despawn Bot Interval",
			"This value is the number in seconds that Donuts should despawn bots. Default is 15 seconds. Note: decreasing this value may affect your performance.",
			15f, 15f, 5f, 600f);

		groupWeightDistroLow = new Setting<string>("Group Chance Weights: Low",
			"Weight Distribution for Group Chance 'Low'. Use relative weights for group sizes 1/2/3/4/5, respectively. Use this formula: group weight / total weight = % chance.",
			_lowWeightsString, _lowWeightsString);

		groupWeightDistroLow.OnSettingChanged +=
			_ => GroupChanceWeights["Low"] = ParseGroupWeightDistro(groupWeightDistroLow.Value);

		groupWeightDistroDefault = new Setting<string>("Group Chance Weights: Default",
			"Weight Distribution for Group Chance 'Default'. Use relative weights for group sizes 1/2/3/4/5, respectively. Use this formula: group weight / total weight = % chance.",
			_defaultWeightsString, _defaultWeightsString);
		
		groupWeightDistroDefault.OnSettingChanged +=
			_ => GroupChanceWeights["Default"] = ParseGroupWeightDistro(groupWeightDistroDefault.Value);

		groupWeightDistroHigh = new Setting<string>("Group Chance Weights: High",
			"Weight Distribution for Group Chance 'High'. Use relative weights for group sizes 1/2/3/4/5, respectively. Use this formula: group weight / total weight = % chance.",
			_highWeightsString, _highWeightsString);
		
		groupWeightDistroHigh.OnSettingChanged +=
			_ => GroupChanceWeights["High"] = ParseGroupWeightDistro(groupWeightDistroHigh.Value);
	}

	private static void InitDebuggingSettings()
	{
		debugLogging = new Setting<bool>("Enable Debug Logging",
			"When enabled, outputs debug logging to the BepInEx console and the LogOutput.log file", false, false);
		
		DebugGizmos = new Setting<bool>("Enable Debug Markers",
			"When enabled, draws debug spheres on set spawn from json", false, false);

		gizmoRealSize = new Setting<bool>("Debug Sphere Real Size",
			"When enabled, debug spheres will be the real size of the spawn radius", false, false);
	}

	private static void InitSpawnPointMakerSettings()
	{
		spawnName = new Setting<string>("Name", "Name used to identify the spawn marker",
			"Spawn Name Here", "Spawn Name Here");

		groupNum = new Setting<int>("Group Number", "Group Number used to identify the spawn marker",
			1, 1, 1, 100);

		wildSpawns = new Setting<string>("Wild Spawn Type", "Select an option.", "pmc", "pmc",
			null, null, _wildSpawnTypes);

		minSpawnDist = new Setting<float>("Min Spawn Distance", 
			"Min Distance Bots will Spawn From Marker You Set.", 1f, 1f, 0f, 500f);

		maxSpawnDist = new Setting<float>("Max Spawn Distance",
			"Max Distance Bots will Spawn From Marker You Set.", 20f, 20f, 1f, 1000f);

		botTriggerDistance = new Setting<float>("Bot Spawn Trigger Distance",
			"Distance in which the player is away from the fight location point that it triggers bot spawn",
			100f, 100f, 0.1f, 1000f);

		botTimerTrigger = new Setting<float>("Bot Spawn Timer Trigger",
			"In seconds before it spawns next wave while player in the fight zone area",
			180f, 180f, 0f, 10000f);

		maxRandNumBots = new Setting<int>("Max Random Bots",
			"Maximum number of bots of Wild Spawn Type that can spawn on this marker",
			2, 2, 1, 5);

		spawnChance = new Setting<int>("Spawn Chance for Marker",
			"Chance bot will be spawn here after timer is reached", 50, 50, 0, 100);

		maxSpawnsBeforeCooldown = new Setting<int>("Max Spawns Before Cooldown",
			"Number of successful spawns before this marker goes in cooldown", 5, 5, 1, 30);

		ignoreTimerFirstSpawn = new Setting<bool>("Ignore Timer for First Spawn",
			"When enabled for this point, it will still spawn even if timer is not ready for first spawn only",
			false, false);

		minSpawnDistanceFromPlayer = new Setting<float>("Min Spawn Distance From Player",
			"How far the random selected spawn near the spawn marker needs to be from player",
			40f, 40f, 0f, 500f);

		CreateSpawnMarkerKey = new Setting<KeyCode>("Create Spawn Marker Key",
			"Press this key to create a spawn marker at your current location",
			KeyCode.None, KeyCode.None);

		DeleteSpawnMarkerKey = new Setting<KeyCode>("Delete Spawn Marker Key",
			"Press this key to delete closest spawn marker within 5m of your player location",
			KeyCode.None, KeyCode.None);
	}

	private static void InitSaveSettings()
	{
		saveNewFileOnly = new Setting<bool>("Save New Locations Only",
			"If enabled saves the raid session changes to a new file. Disabled saves all locations you can see to a new file.",
			false, false);

		WriteToFileKey = new Setting<KeyCode>( "Create Temp Json File",
			"Press this key to write the json file with all entries so far",
			KeyCode.KeypadMinus, KeyCode.KeypadMinus);
	}
	
	private static int[] ParseGroupWeightDistro(string weightsString)
	{
		// Use the Split(char[]) method and manually remove empty entries
		return weightsString
			.Split(',')
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.Select(int.Parse)
			.ToArray();
	}
}