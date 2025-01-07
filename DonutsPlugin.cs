using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using Donuts.PluginGUI;
using Donuts.Tools;
using Donuts.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Donuts;

[BepInPlugin("com.dvize.Donuts", "Donuts", "2.0.0")]
[BepInDependency("com.SPT.core", "3.10.0")]
[BepInDependency("xyz.drakia.waypoints")]
[BepInDependency("com.Arys.UnityToolkit", "1.1.0")]
public class DonutsPlugin : BaseUnityPlugin
{
	private const KeyCode ESCAPE_KEY = KeyCode.Escape;
	
	internal static PluginGUIComponent pluginGUIComponent;
	internal static ConfigEntry<KeyboardShortcut> toggleGUIKey;
	internal static string directoryPath;

	private static readonly List<Folder> _emptyScenarioList = [];
	
	private bool _isWritingToFile;
	
	public new static ManualLogSource Logger { get; private set; }

	private void Awake()
	{
		Logger = base.Logger;

		string assemblyPath = Assembly.GetExecutingAssembly().Location;
		directoryPath = Path.GetDirectoryName(assemblyPath);
		
		// Run dependency checker
		if (!DependencyChecker.ValidateDependencies(Logger, Info, GetType(), Config))
		{
			throw new Exception("Missing Dependencies");
		}

		DonutsConfiguration.ImportConfig(directoryPath);
		pluginGUIComponent = gameObject.AddComponent<PluginGUIComponent>();

		toggleGUIKey = Config.Bind("Config Settings", "Key To Enable/Disable Config Interface",
			new KeyboardShortcut(KeyCode.F9), "Key to Enable/Disable Donuts Configuration Menu");

		ModulePatchManager.EnablePatches();
	}

	// ReSharper disable once Unity.IncorrectMethodSignature
	[UsedImplicitly]
	private async UniTaskVoid Start()
	{
		await SetupScenariosUI();
	}

	private void Update()
	{
		// If setting a keybind, do not trigger functionality
		if (ImGUIToolkit.IsSettingKeybind()) return;

		ShowGuiInputCheck();

		if (IsKeyPressed(DefaultPluginVars.CreateSpawnMarkerKey.Value))
		{
			EditorFunctions.CreateSpawnMarker();
		}
		if (IsKeyPressed(DefaultPluginVars.WriteToFileKey.Value) && !_isWritingToFile)
		{
			_isWritingToFile = true;
			EditorFunctions.WriteToJsonFileAsync(directoryPath)
				.ContinueWith(() => _isWritingToFile = false)
				.Forget();
		}
		if (IsKeyPressed(DefaultPluginVars.DeleteSpawnMarkerKey.Value))
		{
			EditorFunctions.DeleteSpawnMarker();
		}
	}

	private static void ShowGuiInputCheck()
	{
		if (IsKeyPressed(toggleGUIKey.Value) || IsKeyPressed(ESCAPE_KEY))
		{
			if (!IsKeyPressed(ESCAPE_KEY))
			{
				DefaultPluginVars.ShowGUI = !DefaultPluginVars.ShowGUI;
			}
			// Check if the config window is open
			else if (DefaultPluginVars.ShowGUI)
			{
				DefaultPluginVars.ShowGUI = false;
			}
		}
	}

	private static async UniTask SetupScenariosUI()
	{
		await LoadDonutsScenarios();

		// Dynamically initialize the scenario settings
		DefaultPluginVars.pmcScenarioSelection = new Setting<string>("PMC Raid Spawn Preset Selection",
			"Select a preset to use when spawning as PMC",
			DefaultPluginVars.PmcScenarioSelectionValue ?? "live-like", "live-like", options: 
			DefaultPluginVars.pmcScenarioCombinedArray);

		DefaultPluginVars.scavScenarioSelection = new Setting<string>("SCAV Raid Spawn Preset Selection",
			"Select a preset to use when spawning as SCAV",
			DefaultPluginVars.ScavScenarioSelectionValue ?? "live-like", "live-like", options: DefaultPluginVars.scavScenarioCombinedArray);

		// Call InitializeDropdownIndices to ensure scenarios are loaded and indices are set
		//MainSettingsPage.InitializeDropdownIndices();
	}

	private static async UniTask LoadDonutsScenarios()
	{
		// TODO: Write a null check in case the files are missing and generate new defaults

		string scenarioConfigPath = Path.Combine(directoryPath, "ScenarioConfig.json");
		DefaultPluginVars.PmcScenarios = await LoadFoldersAsync(scenarioConfigPath);
		
		string randomScenarioConfigPath = Path.Combine(directoryPath, "RandomScenarioConfig.json");
		DefaultPluginVars.PmcRandomScenarios = await LoadFoldersAsync(randomScenarioConfigPath);

		DefaultPluginVars.ScavScenarios = DefaultPluginVars.PmcScenarios;
		DefaultPluginVars.ScavRandomScenarios = DefaultPluginVars.PmcRandomScenarios;

		PopulateScenarioValues();
		
#if DEBUG
		Logger.LogWarning($"Loaded PMC Scenarios: {string.Join(", ", DefaultPluginVars.pmcScenarioCombinedArray)}");
		Logger.LogWarning($"Loaded Scav Scenarios: {string.Join(", ", DefaultPluginVars.scavScenarioCombinedArray)}");
#endif
	}

	private static async UniTask<List<Folder>> LoadFoldersAsync([NotNull] string filePath)
	{
		if (!File.Exists(filePath))
		{
			Logger.LogError($"File not found: {filePath}");
			return _emptyScenarioList;
		}

		string fileContent = await DonutsHelper.ReadAllTextAsync(filePath);
		var folders = JsonConvert.DeserializeObject<List<Folder>>(fileContent);

		if (folders == null || folders.Count == 0)
		{
			Logger.LogError($"No Donuts Folders found in Scenario Config file at: {filePath}");
			return _emptyScenarioList;
		}

		Logger.LogWarning($"Loaded {folders.Count.ToString()} Donuts Scenario Folders");
		return folders;
	}

	private static void PopulateScenarioValues()
	{
		DefaultPluginVars.pmcScenarioCombinedArray = GenerateScenarioValues(DefaultPluginVars.PmcScenarios, DefaultPluginVars.PmcRandomScenarios);
		Logger.LogWarning($"Loaded {DefaultPluginVars.pmcScenarioCombinedArray.Length.ToString()} PMC Scenarios and Finished Generating");

		DefaultPluginVars.scavScenarioCombinedArray = GenerateScenarioValues(DefaultPluginVars.ScavScenarios, DefaultPluginVars.ScavRandomScenarios);
		Logger.LogWarning($"Loaded {DefaultPluginVars.scavScenarioCombinedArray.Length.ToString()} SCAV Scenarios and Finished Generating");
	}

	private static string[] GenerateScenarioValues([NotNull] List<Folder> scenarios, [NotNull] List<Folder> randomScenarios)
	{
		var scenarioValues = new string[scenarios.Count + randomScenarios.Count];
		var pointer = 0;

		foreach (Folder scenario in scenarios)
		{
			scenarioValues[pointer] = scenario.Name;
			pointer++;
		}

		foreach (Folder scenario in randomScenarios)
		{
			scenarioValues[pointer] = scenario.RandomScenarioConfig;
			pointer++;
		}

		return scenarioValues;
	}

	private static bool IsKeyPressed(KeyboardShortcut key)
	{
		bool isMainKeyDown = UnityInput.Current.GetKeyDown(key.MainKey);
		var allModifierKeysDown = true;
		
		foreach (KeyCode keyCode in key.Modifiers)
		{
			if (!UnityInput.Current.GetKey(keyCode))
			{
				allModifierKeysDown = false;
				break;
			}
		}
		
		return isMainKeyDown && allModifierKeysDown;
	}

	private static bool IsKeyPressed(KeyCode key) => UnityInput.Current.GetKeyDown(key);
}