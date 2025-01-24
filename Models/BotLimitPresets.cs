using Newtonsoft.Json;

namespace Donuts.Models;

[JsonObject]
public class BotLimitPresets
{
	[JsonProperty("FactoryBotLimit")]
	public int FactoryBotLimit { get; set; }
	
	[JsonProperty("InterchangeBotLimit")]
	public int InterchangeBotLimit { get; set; }
	
	[JsonProperty("LaboratoryBotLimit")]
	public int LaboratoryBotLimit { get; set; }
	
	[JsonProperty("LighthouseBotLimit")]
	public int LighthouseBotLimit { get; set; }
	
	[JsonProperty("ReserveBotLimit")]
	public int ReserveBotLimit { get; set; }
	
	[JsonProperty("ShorelineBotLimit")]
	public int ShorelineBotLimit { get; set; }
	
	[JsonProperty("WoodsBotLimit")]
	public int WoodsBotLimit { get; set; }
	
	[JsonProperty("CustomsBotLimit")]
	public int CustomsBotLimit { get; set; }
	
	[JsonProperty("TarkovStreetsBotLimit")]
	public int TarkovStreetsBotLimit { get; set; }
	
	[JsonProperty("GroundZeroBotLimit")]
	public int GroundZeroBotLimit { get; set; }
}