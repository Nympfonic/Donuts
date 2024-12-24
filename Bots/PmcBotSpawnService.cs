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
		if (!DefaultPluginVars.hardStopOptionPMC.Value)
		{
			return false;
		}

		int hardStopTime = DefaultPluginVars.hardStopTimePMC.Value;
		int hardStopPercent = DefaultPluginVars.hardStopPercentPMC.Value;

		bool result = HasReachedHardStopTime(hardStopTime, hardStopPercent);
#if DEBUG
		if (result) Logger.LogDebug("PMC spawn not allowed due to raid time conditions - skipping this spawn");
#endif
		return result;
	}

	protected override int GetBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		return BotHelper.GetBotGroupSize(DefaultPluginVars.pmcGroupChance.Value, minGroupSize, maxGroupSize);
	}

	protected override List<BotWave> GetBotWavesList() => MapBotWaves.Pmc;
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledPMC.Value;
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostPMC.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsPMC.Value;
	protected override bool IsCorrectSpawnType(WildSpawnType role) => role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR;
}