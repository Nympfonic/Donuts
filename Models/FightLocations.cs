using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class FightLocations
{
	public List<Entry> Locations { get; set; }
}