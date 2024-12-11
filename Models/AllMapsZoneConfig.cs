using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Donuts.Models;

[JsonObject]
public class AllMapsZoneConfig
{
	[JsonProperty("maps")]
	public Dictionary<string, MapZoneConfig> Maps { get; set; } = [];

	public static AllMapsZoneConfig LoadFromDirectory(string directoryPath)
	{
		var allMapsConfig = new AllMapsZoneConfig();
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

	private static void InitializeMapConfig(AllMapsZoneConfig allMapsConfig, MapZoneConfig mapConfig)
	{
		string mapName = mapConfig.MapName;
		if (!allMapsConfig.Maps.ContainsKey(mapName))
		{
			allMapsConfig.Maps[mapName] = new MapZoneConfig
			{
				MapName = mapName,
				Zones = []
			};
		}

		foreach (KeyValuePair<string, List<Position>> zone in mapConfig.Zones)
		{
			if (!allMapsConfig.Maps[mapName].Zones.ContainsKey(zone.Key))
			{
				allMapsConfig.Maps[mapName].Zones[zone.Key] = [];
			}
			allMapsConfig.Maps[mapName].Zones[zone.Key].AddRange(zone.Value);
		}
	}
}