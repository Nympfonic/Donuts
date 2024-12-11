﻿using Donuts.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Donuts.Models;

[JsonObject]
public class BotWavesConfig
{
	[JsonProperty("maps")]
	public Dictionary<string, MapBotWaves> Maps { get; set; }

	public void EnsureUniqueGroupNumForBotWaves()
	{
		foreach (MapBotWaves map in Maps.Values)
		{
			map.SCAV = EnsureUniqueGroupNums(map.SCAV);
			map.PMC = EnsureUniqueGroupNums(map.PMC);
		}
	}
	
	[NotNull]
	private static List<BotWave> EnsureUniqueGroupNums([NotNull] List<BotWave> botWaves)
	{
		var uniqueWaves = new List<BotWave>();
		foreach (IGrouping<int, BotWave> group in botWaves.GroupBy(wave => wave.GroupNum))
		{
			List<BotWave> groupList = group.ToList();
			int count = groupList.Count;
			if (count > 1)
				uniqueWaves.Add(groupList.PickRandomElement());
			else if (count == 1)
				uniqueWaves.Add(groupList[0]);
		}
		return uniqueWaves;
	}
}