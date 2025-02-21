using Newtonsoft.Json;

namespace Donuts.Models;

[JsonObject]
public class Preset
{
	[JsonProperty("Name")]
	public string Name { get; set; }
	
	[JsonProperty("Weight")]
	public int Weight { get; set; }
}