using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Donuts.Bots;

public class ScavBotDataService : BotDataService
{
	public override DonutsSpawnType SpawnType => DonutsSpawnType.Scav;

	protected override ReadOnlyCollection<BotDifficulty> BotDifficulties { get; } =
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesSCAV.Value);

	protected override string GroupChance => DefaultPluginVars.scavGroupChance.Value;
	
	protected override WildSpawnType GetWildSpawnType() => WildSpawnType.assault;
	protected override EPlayerSide GetPlayerSide(WildSpawnType spawnType) => EPlayerSide.Savage;

	protected override BotConfig GetStartingBotConfig() =>
		botConfig ??= ConfigService.GetAllMapsStartingBotConfig()!.Maps[ConfigService.GetMapLocation()].Scav;

	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesSCAV.Value);

	protected override List<string> GetZoneNames(string location) =>
		ConfigService.GetAllMapsStartingBotConfig()!.Maps[location].Scav.Zones;
}