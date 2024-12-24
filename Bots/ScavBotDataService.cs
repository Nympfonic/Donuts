using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.Generic;

namespace Donuts.Bots;

public class ScavBotDataService : BotDataService
{
	private BotConfig _botConfig;
	private List<BotDifficulty> _botDifficulties;
	
	public override DonutsSpawnType SpawnType => DonutsSpawnType.Scav;

	protected override BotConfig BotConfig =>
		_botConfig ??= ConfigService.GetStartingBotConfig()!.Maps[ConfigService.GetMapLocation()].Scav;
	
	protected override List<BotDifficulty> BotDifficulties =>
		_botDifficulties ??= BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesSCAV.Value.ToLower());

	protected override string GroupChance => DefaultPluginVars.scavGroupChance.Value;

	public override WildSpawnType GetWildSpawnType() => WildSpawnType.assault;
	public override EPlayerSide GetPlayerSide(WildSpawnType spawnType) => EPlayerSide.Savage;
	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesSCAV.Value);

	protected override List<string> GetZoneNames(string location)
	{
		return ConfigService.GetStartingBotConfig()!.Maps[location].Scav.Zones;
	}
}