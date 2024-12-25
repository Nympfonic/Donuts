using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.Generic;

namespace Donuts.Bots;

public class ScavBotSpawnService : BotSpawnService
{
	protected override bool HasReachedHardCap()
	{
		int activeBots = GetAliveBotsCount();
		if (activeBots < DataService.MaxBotLimit || DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value)
		{
			return true;
		}
#if DEBUG
		Logger.LogDebug(string.Format(
			"{0} spawn not allowed due to {0} bot limit - skipping this spawn. Active {0}s: {0}, {0} Bot Limit: {0}",
			DataService.SpawnType.ToString()));
#endif
		return false;
	}

	protected override bool HasReachedHardStopTime()
	{
		if (!DefaultPluginVars.hardStopOptionSCAV.Value)
		{
			return false;
		}

		int hardStopTime = DefaultPluginVars.hardStopTimeSCAV.Value;
		int hardStopPercent = DefaultPluginVars.hardStopPercentSCAV.Value;

		bool result = HasReachedHardStopTime(hardStopTime, hardStopPercent);
#if DEBUG
		if (result) Logger.LogDebug("Scav spawn not allowed due to raid time conditions - skipping this spawn");
#endif
		return result;
	}

	protected override int GetBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		return BotHelper.GetBotGroupSize(DefaultPluginVars.scavGroupChance.Value, minGroupSize, maxGroupSize);
	}
	
	protected override List<BotWave> GetBotWavesList() => MapBotWaves.Scav;
	protected override int GetAliveBotsCount() => BotHelper.GetAliveBotsCount(IsScav);
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsScav(role);
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledSCAV.Value;
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostSCAV.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsSCAV.Value;
	
	private static bool IsScav(WildSpawnType role) => role == WildSpawnType.assault;
}