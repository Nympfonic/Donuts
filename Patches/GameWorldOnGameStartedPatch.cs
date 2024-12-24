using Comfort.Common;
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
internal class GameWorldOnGameStartedPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));

	[PatchPrefix]
	private static void PatchPrefix()
	{
		if (!Singleton<DonutsRaidManager>.Instantiated)
		{
			Logger.LogError($"Singleton<{nameof(DonutsRaidManager)}> is not instantiated");
			return;
		}

		Singleton<DonutsRaidManager>.Instance.StartBotSpawnController();
	}
}