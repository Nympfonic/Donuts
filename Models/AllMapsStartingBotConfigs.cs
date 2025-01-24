using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class AllMapsStartingBotConfigs
{
	[JsonProperty("Maps")]
	public Dictionary<string, MapStartingBotConfigs> Maps { get; set; }
}

[JsonObject]
public class MapStartingBotConfigs
{
	[JsonProperty("PMC")]
	public StartingBotConfig Pmc { get; set; }
	
	[JsonProperty("SCAV")]
	public StartingBotConfig Scav { get; set; }
}

[JsonObject]
public class StartingBotConfig
{
	[JsonProperty("MinCount")]
	public int MinCount { get; set; }
	
	[JsonProperty("MaxCount")]
	public int MaxCount { get; set; }
	
	[JsonProperty("MinGroupSize")]
	public int MinGroupSize { get; set; }
	
	[JsonProperty("MaxGroupSize")]
	public int MaxGroupSize { get; set; }
	
	[JsonProperty("Zones")]
	public List<string> Zones { get; set; }
}