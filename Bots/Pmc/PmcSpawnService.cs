using Cysharp.Text;
using Donuts.Bots.Processors;
using Donuts.Utils;
using JetBrains.Annotations;

namespace Donuts.Bots;

[UsedImplicitly]
public class PmcSpawnService : BotSpawnService
{
	public PmcSpawnService(BotConfigService configService, IBotDataService dataService) : base(configService, dataService)
	{
		spawnType = DonutsSpawnType.Pmc;
		
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
				"{0} spawn not allowed due to {0} bot limit - skipping this spawn. Active {0}s: {1}, {0} Bot Limit: {2}",
				spawnType.ToString(), activeBots.ToString(), dataService.MaxBotLimit.ToString());
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