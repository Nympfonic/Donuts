using Cysharp.Text;
using Donuts.Utils;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Donuts.Models;

public class AllMapsZoneConfigs
{
	public Dictionary<string, MapZoneConfig> Maps { get; } = [];
	
	public static AllMapsZoneConfigs LoadFromDirectory(string directoryPath)
	{
		var allMapsConfigs = new AllMapsZoneConfigs();
		string[] files = Directory.GetFiles(directoryPath, "*.json");
		
		foreach (string file in files)
		{
			string json = File.ReadAllText(file);
			var mapConfig = JsonConvert.DeserializeObject<MapZoneConfig>(json);
			if (mapConfig == null) continue;

			string mapName = mapConfig.MapName.ToLower();
			if (!allMapsConfigs.Maps.ContainsKey(mapName))
			{
				allMapsConfigs.Maps[mapName] = mapConfig;
				continue;
			}
			
			allMapsConfigs.Maps[mapName].AppendZones(mapConfig);
		}

		var sb = new StringBuilder();
		sb.AppendLine("allMapsConfigs.Maps[\"bigmap\"].Zones");
		foreach (string zoneName in allMapsConfigs.Maps["bigmap"].Zones.Keys)
		{
			sb.Append(zoneName);
			sb.Append(", ");
		}
		sb.AppendLine();
		DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(AllMapsZoneConfigs), nameof(LoadFromDirectory));
		
		return allMapsConfigs;
	}
}

[JsonObject]
public class MapZoneConfig
{
	[JsonProperty("MapName")]
	public string MapName { get; set; }
	
	[JsonProperty("Zones")]
	[JsonConverter(typeof(ZoneSpawnPoints.JsonConverter))]
	public ZoneSpawnPoints Zones { get; set; }
	
	public void AppendZones(MapZoneConfig other)
	{
		if (Zones == null || other?.Zones == null) return;
		
		foreach ((string zoneName, List<Vector3> spawnPoints) in other.Zones)
		{
			Zones.AppendSpawnPoints(zoneName, spawnPoints);
		}
	}
}