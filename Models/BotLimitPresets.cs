using Newtonsoft.Json;

namespace Donuts.Models;

[JsonObject]
public class BotLimitPresets
{
	[JsonProperty("factoryBotLimit")]
	public int FactoryBotLimit { get; set; }
	
	[JsonProperty("interchangeBotLimit")]
	public int InterchangeBotLimit { get; set; }
	
	[JsonProperty("laboratoryBotLimit")]
	public int LaboratoryBotLimit { get; set; }
	
	[JsonProperty("lighthouseBotLimit")]
	public int LighthouseBotLimit { get; set; }
	
	[JsonProperty("reserveBotLimit")]
	public int ReserveBotLimit { get; set; }
	
	[JsonProperty("shorelineBotLimit")]
	public int ShorelineBotLimit { get; set; }
	
	[JsonProperty("woodsBotLimit")]
	public int WoodsBotLimit { get; set; }
	
	[JsonProperty("customsBotLimit")]
	public int CustomsBotLimit { get; set; }
	
	[JsonProperty("tarkovStreetsBotLimit")]
	public int TarkovStreetsBotLimit { get; set; }
	
	[JsonProperty("groundZeroBotLimit")]
	public int GroundZeroBotLimit { get; set; }
}