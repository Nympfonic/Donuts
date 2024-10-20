using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models
{
	public class Folder
	{
		public string Name { get; set; }
		public int Weight { get; set; }
		public bool RandomSelection { get; set; }
		public BotLimitPresets PMCBotLimitPresets { get; set; }
		public BotLimitPresets SCAVBotLimitPresets { get; set; }
		public string RandomScenarioConfig { get; set; }
		// FIXME
		//[JsonProperty("presets")]
		public List<Presets> Presets { get; set; }
	}
}
