using Donuts.Models;
using Donuts.Utils;
using EFT;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public class PmcBotDataService : BotDataService
{
	private BotConfig _botConfig;
	private List<BotDifficulty> _botDifficulties;
	
	public override DonutsSpawnType SpawnType => DonutsSpawnType.Pmc;

	protected override BotConfig BotConfig =>
		_botConfig ??= ConfigService.GetStartingBotConfig()!.Maps[ConfigService.GetMapLocation()].Pmc;
	
	protected override List<BotDifficulty> BotDifficulties =>
		_botDifficulties ??= BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesPMC.Value.ToLower());

	protected override string GroupChance => DefaultPluginVars.pmcGroupChance.Value;

	public override WildSpawnType GetWildSpawnType() =>
		DefaultPluginVars.pmcFaction.Value switch
		{
			"USEC" => WildSpawnType.pmcUSEC,
			"BEAR" => WildSpawnType.pmcBEAR,
			_ => GetPmcFactionBasedOnRatio()
		};

	public override EPlayerSide GetPlayerSide(WildSpawnType spawnType) =>
		spawnType switch
		{
			WildSpawnType.pmcUSEC => EPlayerSide.Usec,
			WildSpawnType.pmcBEAR => EPlayerSide.Bear,
			_ => throw new ArgumentException(
				"Must provide a PMC WildSpawnType (WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR).", nameof(spawnType))
		};

	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesPMC.Value);

	protected override List<string> GetZoneNames(string location)
	{
		return ConfigService.GetStartingBotConfig()!.Maps[location].Pmc.Zones;
	}

	private static WildSpawnType GetPmcFactionBasedOnRatio() =>
		Random.Range(0, 100) < DefaultPluginVars.pmcFactionRatio.Value
			? WildSpawnType.pmcUSEC
			: WildSpawnType.pmcBEAR;
}