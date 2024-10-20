using System.Collections.Generic;

namespace Donuts.Models
{
	public class MapZoneConfig
	{
		public string MapName { get; set; }
		public Dictionary<string, List<Position>> Zones { get; set; }
	}
}
