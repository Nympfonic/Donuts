using Donuts.Models;
using Donuts.Utils;
using EFT;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public class PmcBotDataService : BotDataService
{
	public override DonutsSpawnType SpawnType => DonutsSpawnType.Pmc;

	protected override List<BotDifficulty> BotDifficulties { get; } =
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesPMC.Value.ToLower());

	protected override string GroupChance => DefaultPluginVars.pmcGroupChance.Value;

	protected override WildSpawnType GetWildSpawnType() =>
		DefaultPluginVars.pmcFaction.Value switch
		{
			"USEC" => WildSpawnType.pmcUSEC,
			"BEAR" => WildSpawnType.pmcBEAR,
			_ => GetPmcFactionBasedOnRatio()
		};

	protected override EPlayerSide GetPlayerSide(WildSpawnType spawnType) =>
		spawnType switch
		{
			WildSpawnType.pmcUSEC => EPlayerSide.Usec,
			WildSpawnType.pmcBEAR => EPlayerSide.Bear,
			_ => throw new ArgumentException(
				"Must provide a PMC WildSpawnType (WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR).", nameof(spawnType))
		};

	protected override BotConfig GetBotConfig() =>
		botConfig ??= ConfigService.GetStartingBotConfig()!.Maps[ConfigService.GetMapLocation()].Pmc;

	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesPMC.Value);

	protected override List<string> GetZoneNames(string location) =>
		ConfigService.GetStartingBotConfig()!.Maps[location].Pmc.Zones;

	private static WildSpawnType GetPmcFactionBasedOnRatio() =>
		Random.Range(0, 100) < DefaultPluginVars.pmcFactionRatio.Value
			? WildSpawnType.pmcUSEC
			: WildSpawnType.pmcBEAR;
}