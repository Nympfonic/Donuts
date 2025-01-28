﻿using Donuts.Models;
using Donuts.Utils;
using EFT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public class PmcBotDataService : BotDataService
{
	public override DonutsSpawnType SpawnType => DonutsSpawnType.Pmc;

	public override StartingBotConfig StartingBotConfig =>
		startingBotConfig ??= ConfigService.GetAllMapsStartingBotConfigs()!.Maps[ConfigService.GetMapLocation()].Pmc;

	protected override ReadOnlyCollection<BotDifficulty> BotDifficulties { get; } =
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesPMC.Value);

	protected override string GroupChance => DefaultPluginVars.pmcGroupChance.Value;
	
	protected override List<BotWave> GetBotWaves() => MapBotWaves.Pmc;
	
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

	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesPMC.Value);

	private static WildSpawnType GetPmcFactionBasedOnRatio() =>
		Random.Range(0, 100) < DefaultPluginVars.pmcFactionRatio.Value
			? WildSpawnType.pmcUSEC
			: WildSpawnType.pmcBEAR;
}