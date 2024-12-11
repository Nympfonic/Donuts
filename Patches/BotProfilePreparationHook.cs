using System.Reflection;
using Donuts.Bots;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Donuts.Patches;

internal class BotProfilePreparationHook : ModulePatch
{
	protected override MethodBase GetTargetMethod() =>
		AccessTools.Method(typeof(BotsController), nameof(BotsController.AddActivePLayer));

	[PatchPrefix]
	private static void PatchPrefix() => DonutsRaidManager.Enable();
}