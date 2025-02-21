using Donuts.Utils;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Donuts.Spawning.Models;

[JsonObject]
public class AllMapsBotWavesConfigs
{
	[JsonProperty("Maps")]
	public Dictionary<string, MapBotWaves> Maps { get; private set; }
	
	internal void Validate()
	{
		foreach (MapBotWaves map in Maps.Values)
		{
			map.EnsureUniqueGroupNums();
		}
	}
}

[JsonObject]
public class MapBotWaves
{
	private static readonly PropertyInfo[] _properties = typeof(MapBotWaves).GetProperties();
	
	[JsonProperty("PMC")]
	public BotWave[] Pmc { get; private set; }
	
	[JsonProperty("SCAV")]
	public BotWave[] Scav { get; private set; }
	
	internal void EnsureUniqueGroupNums()
	{
		// Dynamically retrieve all the BotWave[] properties at runtime so we don't have to write out logic for each of them
		foreach (PropertyInfo property in _properties)
		{
			var currentWaves = (BotWave[])property.GetValue(this);
			
			// Perform grouping based on a wave's GroupNum value
			IGrouping<int, BotWave>[] groupings = currentWaves.GroupBy(wave => wave.GroupNum).ToArray();
			int groupingsCount = groupings.Length;
			
			if (groupingsCount == 0)
			{
				continue;
			}
			
			var index = 0;
			var uniqueWaves = new BotWave[groupingsCount];
			// We pick a random wave from each group so we avoid duplicates sharing the same groupNum
			// TODO: Wouldn't this go against the rule where multiple waves can share the same timer by using same groupNum?
			foreach (IGrouping<int, BotWave> group in groupings)
			{
				BotWave[] groupArray = group.ToArray();
				uniqueWaves[index++] = groupArray.PickRandomElement();
			}
			
			property.SetValue(this, uniqueWaves);
		}
	}
}