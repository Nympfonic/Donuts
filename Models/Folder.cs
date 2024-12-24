using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class Folder
{
	[JsonProperty("name")]
	public string Name { get; set; }
	
	[JsonProperty("weight")]
	public int Weight { get; set; }
	
	[JsonProperty("randomSelection")]
	public bool RandomSelection { get; set; }
	
	[JsonProperty("pmcBotLimitPresets")]
	public BotLimitPresets PmcBotLimitPresets { get; set; }
	
	[JsonProperty("scavBotLimitPresets")]
	public BotLimitPresets ScavBotLimitPresets { get; set; }
	
	[JsonProperty("randomScenarioConfig")]
	public string RandomScenarioConfig { get; set; }
	
	[JsonProperty("presets")]
	public List<Preset> Presets { get; set; }
}