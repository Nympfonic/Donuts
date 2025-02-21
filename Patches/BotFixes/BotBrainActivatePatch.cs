using Donuts.Spawning;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Patches.BotFixes;

[UsedImplicitly]
public class BotBrainActivatePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		// Target method is the method where all the bot logic is activated
		return AccessTools.Method(typeof(BotOwner), "method_10");
	}
	
	[PatchPostfix]
	private static void PatchPostfix(BotOwner __instance)
	{
		EventBus.Raise(RegisterBotEvent.Create(__instance));
	}
}