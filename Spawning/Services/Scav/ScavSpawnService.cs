using Cysharp.Text;
using Donuts.Spawning.Processors;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public class ScavSpawnService : BotSpawnService
{
	public ScavSpawnService(BotConfigService configService, IBotDataService dataService) : base(configService, dataService)
	{
		spawnType = DonutsSpawnType.Scav;
		
		string mapLocation = this.configService.GetMapLocation();
		spawnCheckProcessor = new EntityVicinityCheck(mapLocation, dataService.AllAlivePlayers,
			botTypesToIgnore: [WildSpawnType.assault, WildSpawnType.marksman, WildSpawnType.assaultGroup]);
		spawnCheckProcessor.SetNext(new WallCollisionCheck())
			.SetNext(new GroundCheck());
	}

	protected override bool HasReachedHardCap(bool isHotspot)
	{
		int activeBots = dataService.GetAliveBotsCount();
		if (activeBots < dataService.MaxBotLimit || (isHotspot && DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value))
		{
			return false;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat(
				"{0} spawn not allowed due to {0} bot limit - skipping this spawn. Active {0}s: {1}, {0} Bot Limit: {2}",
				spawnType.ToString(), activeBots.ToString(), dataService.MaxBotLimit.ToString());
			logger.LogDebugDetailed(sb.ToString(), nameof(ScavSpawnService), nameof(HasReachedHardCap));
		}
		
		return true;
	}
	
	protected override bool IsHardStopEnabled() => DefaultPluginVars.hardStopOptionSCAV.Value;
	
	protected override int GetHardStopTime() => DefaultPluginVars.useTimeBasedHardStop.Value
		? DefaultPluginVars.hardStopTimeSCAV.Value
		: DefaultPluginVars.hardStopPercentSCAV.Value;
	
	protected override int GetBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		return BotHelper.GetBotGroupSize(DefaultPluginVars.scavGroupChance.Value, minGroupSize, maxGroupSize);
	}
	
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostSCAV.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsSCAV.Value;
}