﻿using Donuts.Models;
using Donuts.Utils;
using EFT;
using System.Collections.ObjectModel;

namespace Donuts.Bots;

public class ScavBotSpawnService : BotSpawnService
{
	private ReadOnlyCollection<BotWave> _botWaves;
	
	protected override bool HasReachedHardCap()
	{
		int activeBots = GetAliveBotsCount();
		if (activeBots < DataService.MaxBotLimit || DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value)
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

	protected override bool IsHardStopEnabled() => DefaultPluginVars.hardStopOptionSCAV.Value;
	
	protected override int GetHardStopTime() => DefaultPluginVars.useTimeBasedHardStop.Value
		? DefaultPluginVars.hardStopTimeSCAV.Value
		: DefaultPluginVars.hardStopPercentSCAV.Value;

	protected override int GetBotGroupSize(int minGroupSize, int maxGroupSize)
	{
		return BotHelper.GetBotGroupSize(DefaultPluginVars.scavGroupChance.Value, minGroupSize, maxGroupSize);
	}
	
	protected override ReadOnlyCollection<BotWave> GetBotWaves() => _botWaves ??= MapBotWaves.Scav.AsReadOnly();
	protected override int GetAliveBotsCount() => BotHelper.GetAliveBotsCount(IsScav);
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsScav(role);
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledSCAV.Value;
	protected override bool IsHotspotBoostEnabled() => DefaultPluginVars.hotspotBoostSCAV.Value;
	protected override int GetMaxBotRespawns() => DefaultPluginVars.maxRespawnsSCAV.Value;
	
	private static bool IsScav(WildSpawnType role) => role == WildSpawnType.assault;
}