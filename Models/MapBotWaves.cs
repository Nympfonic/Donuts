using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class MapBotWaves
{
	[JsonProperty("PMC")]
	public List<BotWave> Pmc { get; set; }
	
	[JsonProperty("SCAV")]
	public List<BotWave> Scav { get; set; }
}