using EFT;
using JetBrains.Annotations;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public class ScavDespawnService(BotConfigService configService, IBotDataService dataService)
	: BotDespawnService(configService, dataService)
{
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledSCAV.Value;
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsScav(role);
	
	private static bool IsScav(WildSpawnType role) => role == WildSpawnType.assault;
}