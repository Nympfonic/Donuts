using System.Collections.Generic;

namespace Donuts.Models
{
	public class BotConfig
	{
		public int MinCount { get; set; }
		public int MaxCount { get; set; }
		public int MinGroupSize { get; set; }
		public int MaxGroupSize { get; set; }
		public List<string> Zones { get; set; }
	}
}
