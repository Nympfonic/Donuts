using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Donuts.Models;

[JsonObject]
public class AllMapsZoneConfigs
{
	[JsonProperty("Maps")]
	public Dictionary<string, MapZoneConfig> Maps { get; set; } = [];
	
	public static AllMapsZoneConfigs LoadFromDirectory(string directoryPath)
	{
		var allMapsConfig = new AllMapsZoneConfigs();
		string[] files = Directory.GetFiles(directoryPath, "*.json");
		
		foreach (string file in files)
		{
			string json = File.ReadAllText(file);
			var mapConfig = JsonConvert.DeserializeObject<MapZoneConfig>(json);
			if (mapConfig == null) continue;
			
			InitializeMapConfig(allMapsConfig, mapConfig);
		}
		
		return allMapsConfig;
	}
	
	/// <summary>
	/// Adds a MapZoneConfig's list of zones and spawn points into the AllMapsZoneConfigs.Maps dictionary.
	/// </summary>
	/// <param name="allMapsConfigs">The target config to insert new data into.</param>
	/// <param name="mapConfig">The input config to insert its data into the target config.</param>
	/// <remarks>If a map/zone already exists, the new data is appended instead of overwriting the existing data.</remarks>
	private static void InitializeMapConfig(AllMapsZoneConfigs allMapsConfigs, MapZoneConfig mapConfig)
	{
		string mapName = mapConfig.MapName;
		if (!allMapsConfigs.Maps.TryGetValue(mapName, out MapZoneConfig map))
		{
			allMapsConfigs.Maps.Add(mapName, mapConfig);
			return;
		}
		
		foreach (KeyValuePair<string, List<Vector3>> zone in mapConfig.Zones)
		{
			ZoneSpawnPoints targetZonesDict = map.Zones;
			if (!targetZonesDict.ContainsKey(zone.Key))
			{
				targetZonesDict[zone.Key] = [];
			}
			targetZonesDict[zone.Key].AddRange(zone.Value);
		}
	}
}

[JsonObject]
public class MapZoneConfig
{
	[JsonProperty("MapName")]
	public string MapName { get; set; }
	
	[JsonProperty("Zones")]
	[JsonConverter(typeof(ZoneSpawnPointsConverter))]
	public ZoneSpawnPoints Zones { get; set; }
}