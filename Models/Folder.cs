using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class Folder
{
	[JsonProperty("Name")]
	public string Name { get; set; }
	
	[JsonProperty("Weight")]
	public int Weight { get; set; }
	
	[JsonProperty("RandomSelection")]
	public bool RandomSelection { get; set; }
	
	[JsonProperty("PmcBotLimitPresets")]
	public BotLimitPresets PmcBotLimitPresets { get; set; }
	
	[JsonProperty("ScavBotLimitPresets")]
	public BotLimitPresets ScavBotLimitPresets { get; set; }
	
	[JsonProperty("RandomScenarioConfig")]
	public string RandomScenarioConfig { get; set; }
	
	[JsonProperty("Presets")]
	public List<Preset> Presets { get; set; }
}