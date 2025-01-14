using Donuts.Bots;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches;

/// <summary>
/// Re-initializes each new game
/// </summary>
[UsedImplicitly]
internal class StartSpawningRaidManagerPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));

	[PatchPostfix]
	private static void PatchPostfix()
	{
		DonutsRaidManager raidManager = MonoBehaviourSingleton<DonutsRaidManager>.Instance;
		if (raidManager == null)
		{
			Logger.LogError($"Singleton<{nameof(DonutsRaidManager)}> is not instantiated");
			return;
		}
		
		if (!DonutsRaidManager.IsBotSpawningEnabled)
		{
#if DEBUG
			Logger.LogInfo("Running as Fika client or something catastrophic happened to the BotsController, skipping DonutsRaidManager::Initialize"); 
#endif
			return;
		}
			
		raidManager.StartBotSpawnController().Forget();
	}
}