﻿using BepInEx.Logging;
using Comfort.Common;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Donuts.Bots;

public class BotConfigService
{
	private readonly ManualLogSource _logger;
	
	private readonly Dictionary<DonutsSpawnType, int> _botCountLimits = [];
	
	private static readonly List<Player> _emptyPlayerList = [];
	private readonly List<Player> _playerList = new(5);
	
	private GameWorld _gameWorld;
	private string _scenarioSelected;
	private string _mapLocation;
	private string _mapName;
	private AllMapsZoneConfig _allMapsZoneConfig;
	private StartingBotConfig _startingBotConfig;
	private BotWavesConfig _botWavesConfig;
	
	private bool _patternsLoaded;

	private BotConfigService(ManualLogSource logger)
	{
		_logger = logger;
	}

	[NotNull]
	public static BotConfigService Create([NotNull] ManualLogSource logger)
	{
		var service = new BotConfigService(logger);
		service.Initialize();
		return service;
	}
	
	[CanBeNull]
	public BotWavesConfig GetBotWavesConfig()
	{
		if (_botWavesConfig != null)
		{
			return _botWavesConfig;
		}
		
		string jsonFilePath = Path.Combine(DonutsPlugin.directoryPath, "patterns", _scenarioSelected, $"{_mapName}_waves.json");

		if (!File.Exists(jsonFilePath))
		{
			_logger.LogError($"{_mapName}_waves.json file not found at path: {jsonFilePath}");
			return null;
		}

		string jsonString = File.ReadAllText(jsonFilePath);
		_botWavesConfig = JsonConvert.DeserializeObject<BotWavesConfig>(jsonString);
		if (_botWavesConfig == null)
		{
			_logger.LogError($"Failed to deserialize {_mapName}_waves.json for preset: {_scenarioSelected}");
			return null;
		}
#if DEBUG
		_logger.LogDebug($"Successfully loaded {_mapName}_waves.json for preset: {_scenarioSelected}");
#endif
		_botWavesConfig.EnsureUniqueGroupNumForBotWaves();
		return _botWavesConfig;
	}

	[NotNull]
	public string GetMapLocation()
	{
		if (_mapLocation == null)
		{
			string mapLocation = Singleton<GameWorld>.Instance.MainPlayer.Location.ToLower();
			// Handle Ground Zero (High) the same as Ground Zero
			if (mapLocation == "sandbox_high")
			{
				mapLocation = "sandbox";
			}
			_mapLocation = mapLocation;
		}
		return _mapLocation;
	}
	
	[NotNull]
	public string GetMapName()
	{
		if (_mapName == null)
		{
			string mapLocation = GetMapLocation();
			string mapName = mapLocation switch
			{
				"bigmap" => "customs",
				"factory4_day" => "factory",
				"factory4_night" => "factory_night",
				"tarkovstreets" => "streets",
				"rezervbase" => "reserve",
				"interchange" => "interchange",
				"woods" => "woods",
				"sandbox" or "sandbox_high" => "groundzero",
				"laboratory" => "laboratory",
				"lighthouse" => "lighthouse",
				"shoreline" => "shoreline",
				_ => mapLocation
			};
			_mapName = mapName;
		}
		return _mapName;
	}

	[CanBeNull]
	public AllMapsZoneConfig GetAllMapsZoneConfig()
	{
		if (_allMapsZoneConfig == null)
		{
			string zoneSpawnPointsPath = Path.Combine(DonutsPlugin.directoryPath, "zoneSpawnPoints");
			AllMapsZoneConfig allMapsZoneConfig = AllMapsZoneConfig.LoadFromDirectory(zoneSpawnPointsPath);
			if (allMapsZoneConfig == null)
			{
				_logger.NotifyLogError("Donuts: Failed to load AllMapZoneConfig. Donuts will not function properly.");
				return null;
			}
			_allMapsZoneConfig = allMapsZoneConfig;
		}
		return _allMapsZoneConfig;
	}
	
	[CanBeNull]
	public string GetSelectedScenario()
	{
		_scenarioSelected ??= PresetSelector.GetWeightedScenarioSelection();
		if (_scenarioSelected == null)
		{
			_logger.NotifyLogError("Donuts: No valid scenario nor fallback found. Donuts will not function properly.");
			return null;
		}
		return _scenarioSelected;
	}

	[CanBeNull]
	public StartingBotConfig GetStartingBotConfig()
	{
		if (_startingBotConfig != null)
		{
			return _startingBotConfig;
		}
		
		string jsonFilePath = Path.Combine(
			DonutsPlugin.directoryPath,
			"patterns",
			GetSelectedScenario()!,
			$"{GetMapName()}_start.json"
		);

		if (!File.Exists(jsonFilePath))
		{
			_logger.NotifyLogError($"Donuts: {GetMapName()}_start.json file not found. Donuts will not function properly.");
			return null;
		}

		using var reader = new StreamReader(jsonFilePath);
		string jsonString = reader.ReadToEnd();
		_startingBotConfig = JsonConvert.DeserializeObject<StartingBotConfig>(jsonString);
		return _startingBotConfig;
	}
	
	public bool CheckForAnyScenarioPatterns()
	{
		if (_patternsLoaded) return true;

		string patternFolderPath = Path.Combine(DonutsPlugin.directoryPath, "patterns", _scenarioSelected);
		if (!Directory.Exists(patternFolderPath))
		{
			Directory.CreateDirectory(patternFolderPath);
			//DonutsHelper.NotifyLogError($"Donuts Plugin: Folder from ScenarioConfig.json does not actually exist: {patternFolderPath}\nDisabling the donuts plugin for this raid.");
			//filesLoaded = false;
		}

		string[] jsonFiles = Directory.GetFiles(patternFolderPath, "*.json");
		if (jsonFiles.Length == 0)
		{
			// TODO: Implement generating default JSONs for the patterns if not found.
			_logger.NotifyLogError($"Donuts: No JSON Pattern files found in folder: {patternFolderPath}\nDonuts will not function properly.");
			return false;
		}

		_patternsLoaded = true;
		// Display selected preset
		if (DefaultPluginVars.ShowRandomFolderChoice.Value)
		{
			_logger.NotifyLog($"Donuts: Selected Spawn Preset: {_scenarioSelected}");
		}
		return true;
	}
	
	[NotNull]
	public List<Player> GetHumanPlayerList()
	{
		if (_gameWorld.RegisteredPlayers.Count == 0)
		{
			return _emptyPlayerList;
		}

		IEnumerable<Player> allPlayers = _gameWorld.AllPlayersEverExisted;
		foreach (Player player in allPlayers)
		{
			if (!player.IsAI && !_playerList.Contains(player))
			{
				_playerList.Add(player);
			}
		}
		return _playerList;
	}

	public int GetMaxBotLimit(DonutsSpawnType spawnType)
	{
		if (_botCountLimits.TryGetValue(spawnType, out int botLimit))
		{
			return botLimit;
		}
		return -1;
	}

	/// <summary>
	/// Counts the number of alive bots. A predicate can be specified to filter for specific bot types, but is optional.
	/// </summary>
	public int CalculateAliveBotsCount(Func<WildSpawnType, bool> predicate = null)
	{
		var count = 0;
		List<Player> allAlivePlayers = _gameWorld.AllAlivePlayersList;
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			if (player == null || !player.IsAI) continue;

			WildSpawnType role = player.Profile.Info.Settings.Role;
			if (predicate == null || predicate(role))
			{
				count++;
			}
		}
		return count;
	}

	private void Initialize()
	{
		_gameWorld = Singleton<GameWorld>.Instance;
		GetMapLocation();
		GetMapName();
		GetSelectedScenario();
		GetStartingBotConfig();

		InitializeBotLimits(_scenarioSelected, _mapLocation);
	}

	private void InitializeBotLimits([NotNull] string folderName, [NotNull] string location)
	{
		Folder selectedRaidFolder = null;
		foreach (Folder folder in DefaultPluginVars.PmcScenarios)
		{
			if (folder.Name == folderName)
			{
				selectedRaidFolder = folder;
				break;
			}
		}
		
		if (selectedRaidFolder == null) return;

		switch (location)
		{
			case "factory4_day" or "factory4_night":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.FactoryBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.FactoryBotLimit;
				break;
			case "bigmap":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.CustomsBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.CustomsBotLimit;
				break;
			case "interchange":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.InterchangeBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.InterchangeBotLimit;
				break;
			case "rezervbase":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.ReserveBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.ReserveBotLimit;
				break;
			case "laboratory":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.LaboratoryBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.LaboratoryBotLimit;
				break;
			case "lighthouse":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.LighthouseBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.LighthouseBotLimit;
				break;
			case "shoreline":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.ShorelineBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.ShorelineBotLimit;
				break;
			case "woods":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.WoodsBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.WoodsBotLimit;
				break;
			case "tarkovstreets":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.TarkovStreetsBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.TarkovStreetsBotLimit;
				break;
			case "sandbox" or "sandbox_high":
				_botCountLimits[DonutsSpawnType.Pmc] = selectedRaidFolder.PmcBotLimitPresets.GroundZeroBotLimit;
				_botCountLimits[DonutsSpawnType.Scav] = selectedRaidFolder.ScavBotLimitPresets.GroundZeroBotLimit;
				break;
		}
	}
}