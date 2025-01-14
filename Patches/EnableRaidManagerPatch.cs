using Donuts.Bots;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches;

[UsedImplicitly]
internal class EnableRaidManagerPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() =>
		AccessTools.Method(typeof(BotsController), nameof(BotsController.AddActivePLayer));

	[PatchPostfix]
	private static void PatchPostfix() => DonutsRaidManager.Enable();
}