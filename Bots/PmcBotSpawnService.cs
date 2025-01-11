using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.Generic;

namespace Donuts.Bots;

public class PmcBotSpawnService : BotSpawnService
{
	protected override bool HasReachedHardCap()
	{
		int activeBots = GetAliveBotsCount();
		if (activeBots < DataService.MaxBotLimit || DefaultPluginVars.hotspotIgnoreHardCapPMC.Value)
		{
			return false;
		}
#if DEBUG
		Logger.LogDebug(string.Format(
			"{0} spawn not allowed due to {0} bot limit - skipping this spawn. Active {0}s: {0}, {0} Bot Limit: {0}",
			DataService.SpawnType.ToString()));
#endif
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

	protected override List<BotWave> GetBotWavesList() => MapBotWaves.Pmc;
	protected override int GetAliveBotsCount() => BotHelper.GetAliveBotsCount(IsPmc);
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsPmc(role);
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledPMC.Value;
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostPMC.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsPMC.Value;
	
	private static bool IsPmc(WildSpawnType role) => role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR;
}