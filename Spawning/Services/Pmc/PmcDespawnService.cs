using EFT;
using JetBrains.Annotations;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public class PmcDespawnService(BotConfigService configService, IBotDataService dataService)
	: BotDespawnService(configService, dataService)
{
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledPMC.Value;
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsPmc(role);
	
	private static bool IsPmc(WildSpawnType role) => role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR;
}