using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Donuts.Bots;

[UsedImplicitly]
public class BotConfigService
{
	private readonly ManualLogSource _logger;
	
	private readonly Dictionary<DonutsSpawnType, int> _botCountLimits = [];
	
	private static readonly ReadOnlyCollection<Player> _emptyPlayerList = new(Array.Empty<Player>());
	private readonly List<Player> _humanPlayerList;
	private readonly ReadOnlyCollection<Player> _humanPlayerListReadOnly;
	
	private readonly GameWorld _gameWorld;
	private string _scenarioSelected;
	private string _mapLocation;
	private string _mapName;
	private AllMapsZoneConfigs _allMapsZoneConfigs;
	private AllMapsStartingBotConfigs _allMapsStartingBotConfigs;
	private AllMapsBotWavesConfigs _allMapsBotWavesConfigs;
	
	private bool _patternsLoaded;
	
	public BotConfigService()
	{
		_logger = DonutsRaidManager.Logger;
		
		_humanPlayerList = new List<Player>(5);
		_humanPlayerListReadOnly = _humanPlayerList.AsReadOnly();
		_gameWorld = Singleton<GameWorld>.Instance;
		
		GetMapLocation();
		GetMapName();
		GetSelectedScenario();
		GetAllMapsStartingBotConfigs();
		
		InitializeBotLimits(_scenarioSelected, _mapLocation);
	}
	
	[CanBeNull]
	public AllMapsBotWavesConfigs GetAllMapsBotWavesConfigs()
	{
		if (_allMapsBotWavesConfigs != null)
		{
			return _allMapsBotWavesConfigs;
		}
		
		string jsonFilePath = Path.Combine(DonutsPlugin.DirectoryPath, "patterns", _scenarioSelected, $"{_mapName}_waves.json");
		
		if (!File.Exists(jsonFilePath))
		{
			_logger.LogError($"{_mapName}_waves.json file not found at path: {jsonFilePath}");
			return null;
		}
		
		string jsonString = File.ReadAllText(jsonFilePath);
		var botWavesConfig = JsonConvert.DeserializeObject<AllMapsBotWavesConfigs>(jsonString);
		if (botWavesConfig == null)
		{
			_logger.LogError($"Failed to deserialize {_mapName}_waves.json for preset: {_scenarioSelected}");
			return null;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			_logger.LogInfo($"Successfully loaded {_mapName}_waves.json for preset: {_scenarioSelected}");
		}
		botWavesConfig.EnsureUniqueGroupNumForBotWaves();
		_allMapsBotWavesConfigs = botWavesConfig;
		return _allMapsBotWavesConfigs;
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
	public AllMapsZoneConfigs GetAllMapsZoneConfigs()
	{
		if (_allMapsZoneConfigs == null)
		{
			string zoneSpawnPointsPath = Path.Combine(DonutsPlugin.DirectoryPath, "zoneSpawnPoints");
			AllMapsZoneConfigs allMapsZoneConfigs = AllMapsZoneConfigs.LoadFromDirectory(zoneSpawnPointsPath);
			if (allMapsZoneConfigs == null)
			{
				_logger.NotifyLogError("Donuts: Failed to load AllMapZoneConfig. Donuts will not function properly.");
				return null;
			}
			
			_allMapsZoneConfigs = allMapsZoneConfigs;
		}
		
		return _allMapsZoneConfigs;
	}
	
	[CanBeNull]
	private string GetSelectedScenario()
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
	public AllMapsStartingBotConfigs GetAllMapsStartingBotConfigs()
	{
		if (_allMapsStartingBotConfigs != null)
		{
			return _allMapsStartingBotConfigs;
		}
		
		string jsonFilePath = Path.Combine(
			DonutsPlugin.DirectoryPath,
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
		_allMapsStartingBotConfigs = JsonConvert.DeserializeObject<AllMapsStartingBotConfigs>(jsonString);
		return _allMapsStartingBotConfigs;
	}
	
	public bool CheckForAnyScenarioPatterns()
	{
		if (_patternsLoaded) return true;
		
		string patternFolderPath = Path.Combine(DonutsPlugin.DirectoryPath, "patterns", _scenarioSelected);
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
			_logger.NotifyLogError(
				$"Donuts: No JSON Pattern files found in folder: {patternFolderPath}\nDonuts will not function properly.");
			return false;
		}
		
		_patternsLoaded = true;
		// Display selected preset
		if (DefaultPluginVars.ShowRandomFolderChoice.Value)
		{
			_logger.NotifyModSettingsStatus($"Donuts: Selected Spawn Preset: {_scenarioSelected}");
		}
		return true;
	}
	
	[NotNull]
	public ReadOnlyCollection<Player> GetHumanPlayerList()
	{
		if (_gameWorld.RegisteredPlayers.Count == 0)
		{
			return _emptyPlayerList;
		}

		List<Player> allPlayers = _gameWorld.AllPlayersEverExisted.ToList();
		foreach (Player player in allPlayers)
		{
			if (player != null && !player.IsAI && !_humanPlayerList.Contains(player))
			{
				_humanPlayerList.Add(player);
			}
		}
		
		return _humanPlayerListReadOnly;
	}
	
	public int GetMaxBotLimit(DonutsSpawnType spawnType)
	{
		if (_botCountLimits.TryGetValue(spawnType, out int botLimit))
		{
			return botLimit;
		}
		
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		sb.AppendFormat("Donuts: Failed to retrieve {0} max bot cap value. Report this to the author!", spawnType.ToString());
		DonutsRaidManager.Logger.NotifyLogError(sb.ToString());
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
		
		// TODO: Needs a refactor
		// Models would need rearranging, bot limits should be initialized in their respective services
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