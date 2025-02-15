using EFT;
using JetBrains.Annotations;

namespace Donuts.Spawning.Services;

[UsedImplicitly]
public sealed class ScavDespawnService(BotConfigService configService, IBotDataService dataService)
	: BotDespawnService(configService, dataService)
{
	public override DonutsSpawnType SpawnType { get; } = DonutsSpawnType.Scav;
	
	protected override bool IsDespawnBotEnabled() => DefaultPluginVars.DespawnEnabledSCAV.Value;
	protected override bool IsCorrectSpawnType(WildSpawnType role) => IsScav(role);
	
	private static bool IsScav(WildSpawnType role) => role == WildSpawnType.assault;
}