using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.ObjectModel;

namespace Donuts.Bots;

public class ScavBotDataService : BotDataService
{
	public override DonutsSpawnType SpawnType => DonutsSpawnType.Scav;

	public override StartingBotConfig StartingBotConfig =>
		startingBotConfig ??= ConfigService.GetAllMapsStartingBotConfigs()!.Maps[ConfigService.GetMapLocation()].Scav;

	protected override ReadOnlyCollection<BotDifficulty> BotDifficulties { get; } =
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesSCAV.Value);

	protected override string GroupChance => DefaultPluginVars.scavGroupChance.Value;
	
	protected override WildSpawnType GetWildSpawnType() => WildSpawnType.assault;
	protected override EPlayerSide GetPlayerSide(WildSpawnType spawnType) => EPlayerSide.Savage;

	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesSCAV.Value);
}