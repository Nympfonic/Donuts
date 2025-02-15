using Donuts.Spawning.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Random = UnityEngine.Random;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public sealed class PmcDataService : BotDataService
{
	public override DonutsSpawnType SpawnType { get; }
	public override string GroupChance => DefaultPluginVars.pmcGroupChance.Value;
	
	public override ReadOnlyCollection<BotDifficulty> BotDifficulties =>
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesPMC.Value);
	
	public PmcDataService([NotNull] BotConfigService configService) : base(configService)
	{
		SpawnType = DonutsSpawnType.Pmc;
		string mapLocation = configService.GetMapLocation();
		startingBotConfig = configService.GetAllMapsStartingBotConfigs()!.Maps[mapLocation].Pmc;
		botWaves = mapBotWaves.Pmc;
		botWavesByGroupNum = botWaves.ToLookup(wave => wave.GroupNum);
		waveGroupSize = GetWaveMinMaxGroupSize(botWaves);
		MaxBotLimit = configService.GetMaxBotLimit(SpawnType);
		startingSpawnPointsCache = new SpawnPointsCache(ZoneSpawnPoints, startingBotConfig.Zones);
	}
	
	public override int GetAliveBotsCount() => configService.CalculateAliveBotsCount(IsPmc);
	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesPMC.Value);
	
	protected override WildSpawnType GetWildSpawnType() =>
		DefaultPluginVars.pmcFaction.Value switch
		{
			"USEC" => WildSpawnType.pmcUSEC,
			"BEAR" => WildSpawnType.pmcBEAR,
			_ => GetPmcFactionBasedOnRatio()
		};
	
	protected override EPlayerSide GetPlayerSide(WildSpawnType wildSpawnType) =>
		wildSpawnType switch
		{
			WildSpawnType.pmcUSEC => EPlayerSide.Usec,
			WildSpawnType.pmcBEAR => EPlayerSide.Bear,
			_ => throw new ArgumentException(
				"Must provide a PMC WildSpawnType (WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR).", nameof(wildSpawnType))
		};
	
	private static bool IsPmc(WildSpawnType role) => role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR;
	private static WildSpawnType GetPmcFactionBasedOnRatio() =>
		Random.Range(0, 100) < DefaultPluginVars.pmcFactionRatio.Value
			? WildSpawnType.pmcUSEC
			: WildSpawnType.pmcBEAR;
}