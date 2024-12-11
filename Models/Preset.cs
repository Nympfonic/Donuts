using Newtonsoft.Json;

namespace Donuts.Models;

[JsonObject]
public class Preset
{
	[JsonProperty("name")]
	public string Name { get; set; }
	
	[JsonProperty("weight")]
	public int Weight { get; set; }
}