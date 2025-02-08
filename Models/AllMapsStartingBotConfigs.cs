using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class AllMapsStartingBotConfigs
{
	[JsonProperty("Maps")]
	public Dictionary<string, MapStartingBotConfigs> Maps { get; private set; }
}

[JsonObject]
public class MapStartingBotConfigs
{
	[JsonProperty("PMC")]
	public StartingBotConfig Pmc { get; private set; }
	
	[JsonProperty("SCAV")]
	public StartingBotConfig Scav { get; private set; }
}

[JsonObject]
public class StartingBotConfig
{
	[JsonProperty("MinCount")]
	public int MinCount { get; private set; }
	
	[JsonProperty("MaxCount")]
	public int MaxCount { get; private set; }
	
	[JsonProperty("MinGroupSize")]
	public int MinGroupSize { get; private set; }
	
	[JsonProperty("MaxGroupSize")]
	public int MaxGroupSize { get; private set; }
	
	[JsonProperty("Zones")]
	public string[] Zones { get; private set; }
}