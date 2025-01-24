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

	protected override StartingBotConfig GetStartingBotConfig() =>
		startingBotConfig ??= ConfigService.GetAllMapsStartingBotConfigs()!.Maps[ConfigService.GetMapLocation()].Scav;

	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesSCAV.Value);
}