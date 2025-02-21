using Cysharp.Text;
using Donuts.Spawning.Processors;
using Donuts.Utils;
using JetBrains.Annotations;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public sealed class PmcSpawnService : BotSpawnService
{
	public override DonutsSpawnType SpawnType { get; }
	
	public PmcSpawnService(BotConfigService configService, IBotDataService dataService) : base(configService, dataService)
	{
		SpawnType = DonutsSpawnType.Pmc;
		string mapLocation = this.configService.GetMapLocation();
		spawnCheckProcessor = new EntityVicinityCheck(mapLocation, dataService.AllAlivePlayers);
		spawnCheckProcessor.SetNext(new WallCollisionCheck())
			.SetNext(new GroundCheck());
	}
	
	protected override bool HasReachedHardCap(bool isHotspot)
	{
		int activeBots = dataService.GetAliveBotsCount();
		if (activeBots < dataService.MaxBotLimit || (isHotspot && DefaultPluginVars.hotspotIgnoreHardCapPMC.Value))
		{
			return false;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat(
				"{0} spawn not allowed due to {0} bot limit - skipping this spawn. Active {0} bots: {1}, {0} bot limit: {2}",
				SpawnType.Localized(), activeBots.ToString(), dataService.MaxBotLimit.ToString());
			logger.LogDebugDetailed(sb.ToString(), nameof(PmcSpawnService), nameof(HasReachedHardCap));
		}
		
		return true;
	}
	
	protected override bool IsHardStopEnabled() => DefaultPluginVars.hardStopOptionPMC.Value;
	
	protected override int GetHardStopTime() => DefaultPluginVars.useTimeBasedHardStop.Value
		? DefaultPluginVars.hardStopTimePMC.Value
		: DefaultPluginVars.hardStopPercentPMC.Value;
	
	protected override int GetBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		return BotHelper.GetBotGroupSize(DefaultPluginVars.pmcGroupChance.Value, minGroupSize, maxGroupSize);
	}
	
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostPMC.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsPMC.Value;
}