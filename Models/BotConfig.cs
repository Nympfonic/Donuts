using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class BotConfig
{
	[JsonProperty("minCount")]
	public int MinCount { get; set; }
	
	[JsonProperty("maxCount")]
	public int MaxCount { get; set; }
	
	[JsonProperty("minGroupSize")]
	public int MinGroupSize { get; set; }
	
	[JsonProperty("maxGroupSize")]
	public int MaxGroupSize { get; set; }
	
	[JsonProperty("zones")]
	public List<string> Zones { get; set; }
}