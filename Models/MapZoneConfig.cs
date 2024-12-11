using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class MapZoneConfig
{
	[JsonProperty("mapName")]
	public string MapName { get; set; }
	
	[JsonProperty("zones")]
	public Dictionary<string, List<Position>> Zones { get; set; }
}