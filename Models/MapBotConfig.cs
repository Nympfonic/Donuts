using Newtonsoft.Json;

namespace Donuts.Models;

[JsonObject]
public class MapBotConfig
{
	[JsonProperty("PMC")]
	public BotConfig Pmc { get; set; }
	
	[JsonProperty("SCAV")]
	public BotConfig Scav { get; set; }
}