using Donuts.Spawning.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System.Collections.ObjectModel;
using System.Linq;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public sealed class ScavDataService : BotDataService
{
	public override DonutsSpawnType SpawnType { get; }
	public override string GroupChance => DefaultPluginVars.scavGroupChance.Value;
	
	public override ReadOnlyCollection<BotDifficulty> BotDifficulties =>
		BotHelper.GetSettingDifficulties(DefaultPluginVars.botDifficultiesSCAV.Value);
	
	public ScavDataService([NotNull] BotConfigService configService) : base(configService)
	{
		SpawnType = DonutsSpawnType.Scav;
		string mapLocation = configService.GetMapLocation();
		startingBotConfig = configService.GetAllMapsStartingBotConfigs()!.Maps[mapLocation].Scav;
		botWaves = mapBotWaves.Scav;
		botWavesByGroupNum = botWaves.ToLookup(wave => wave.GroupNum);
		waveGroupSize = GetWaveMinMaxGroupSize(botWaves);
		MaxBotLimit = configService.GetMaxBotLimit(SpawnType);
		startingSpawnPointsCache = new SpawnPointsCache(ZoneSpawnPoints, startingBotConfig.Zones);
	}
	
	public override int GetAliveBotsCount() => configService.CalculateAliveBotsCount(IsScav);
	public override BotDifficulty GetBotDifficulty() => GetBotDifficulty(DefaultPluginVars.botDifficultiesSCAV.Value);
	
	protected override WildSpawnType GetWildSpawnType() => WildSpawnType.assault;
	protected override EPlayerSide GetPlayerSide(WildSpawnType wildSpawnType) => EPlayerSide.Savage;
	
	private static bool IsScav(WildSpawnType role) => role == WildSpawnType.assault;
}