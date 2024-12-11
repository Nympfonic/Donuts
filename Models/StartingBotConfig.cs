using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class StartingBotConfig
{
	[JsonProperty("maps")]
	public Dictionary<string, MapBotConfig> Maps { get; set; }
}