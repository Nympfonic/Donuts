using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Donuts.Models
{
	public class AllMapsZoneConfig
	{
		public Dictionary<string, MapZoneConfig> Maps { get; set; } = [];

		public static AllMapsZoneConfig LoadFromDirectory(string directoryPath)
		{
			var allMapsConfig = new AllMapsZoneConfig();
			var files = Directory.GetFiles(directoryPath, "*.json");

			foreach (var file in files)
			{
				var jsonString = File.ReadAllText(file);
				var mapConfig = JsonConvert.DeserializeObject<MapZoneConfig>(jsonString);

				if (mapConfig == null) continue;

				if (!allMapsConfig.Maps.ContainsKey(mapConfig.MapName))
				{
					allMapsConfig.Maps[mapConfig.MapName] = new MapZoneConfig
					{
						MapName = mapConfig.MapName,
						Zones = []
					};
				}

				foreach (var zone in mapConfig.Zones)
				{
					if (!allMapsConfig.Maps[mapConfig.MapName].Zones.ContainsKey(zone.Key))
					{
						allMapsConfig.Maps[mapConfig.MapName].Zones[zone.Key] = [];
					}

					allMapsConfig.Maps[mapConfig.MapName].Zones[zone.Key].AddRange(zone.Value);
				}
			}

			return allMapsConfig;
		}
	}
}
