using Cysharp.Text;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.ObjectModel;

namespace Donuts.Bots;

public class PmcBotSpawnService : BotSpawnService
{
	private ReadOnlyCollection<BotWave> _botWaves;
	
	protected override bool HasReachedHardCap(bool isHotspot)
	{
		int activeBots = GetAliveBotsCount();
		if (activeBots < DataService.MaxBotLimit || (isHotspot && DefaultPluginVars.hotspotIgnoreHardCapPMC.Value))
		{
			return false;
		}
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		sb.AppendFormat(
			"{0} spawn not allowed due to {0} bot limit - skipping this spawn. Active {0}s: {1}, {0} Bot Limit: {2}",
			DataService.SpawnType.ToString(), activeBots.ToString(), DataService.MaxBotLimit.ToString());
		Logger.LogDebugDetailed(sb.ToString(), nameof(PmcBotSpawnService), nameof(HasReachedHardCap));
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
	
	protected override ReadOnlyCollection<BotWave> GetBotWaves() => _botWaves ??= MapBotWaves.Pmc.AsReadOnly();
	protected override int GetAliveBotsCount() => ConfigService.CalculateAliveBotsCount(IsPmc);
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsPmc(role);
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledPMC.Value;
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostPMC.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsPMC.Value;
	
	private static bool IsPmc(WildSpawnType role) => role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR;
}