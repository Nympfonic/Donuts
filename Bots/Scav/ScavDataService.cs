using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System.Collections.ObjectModel;
using System.Linq;

namespace Donuts.Bots;

[UsedImplicitly]
public sealed class ScavDataService : BotDataService
{
	public ScavDataService([NotNull] BotConfigService configService) : base(configService)
	{
		spawnType = DonutsSpawnType.Scav;
		startingBotConfig = configService.GetAllMapsStartingBotConfigs()!.Maps[configService.GetMapLocation()].Scav;
		botWaves = mapBotWaves.Scav;
		botWavesByGroupNum = botWaves.ToLookup(wave => wave.GroupNum);
		waveGroupSize = GetWaveMinMaxGroupSize(botWaves);
		MaxBotLimit = configService.GetMaxBotLimit(spawnType);
		startingSpawnPointsCache = new SpawnPointsCache(ZoneSpawnPoints, startingBotConfig.Zones);
	}
	
	protected override ReadOnlyCollection<BotDifficulty> BotDifficulties =>
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesSCAV.Value);
	
	protected override string GroupChance => DefaultPluginVars.scavGroupChance.Value;
	
	public override int GetAliveBotsCount() => configService.CalculateAliveBotsCount(IsScav);
	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesSCAV.Value);
	
	protected override WildSpawnType GetWildSpawnType() => WildSpawnType.assault;
	protected override EPlayerSide GetPlayerSide(WildSpawnType wildSpawnType) => EPlayerSide.Savage;
	
	private static bool IsScav(WildSpawnType role) => role == WildSpawnType.assault;
}