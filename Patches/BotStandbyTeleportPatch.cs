using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches;

/// <summary>
/// Patches the <see cref="BotStandBy"/> state to prevent bots from teleporting away from their spawn location if
/// they're too far from their target core point.
/// </summary>
[UsedImplicitly]
internal class BotStandbyTeleportPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(BotStandBy), nameof(BotStandBy.UpdateNode));
	}
	
	[PatchPrefix]
	private static bool PatchPrefix(BotStandBy __instance, BotStandByType ___standByType, BotOwner ___botOwner_0)
	{
		if (!___botOwner_0.Settings.FileSettings.Mind.CAN_STAND_BY || !__instance.CanDoStandBy)
		{
			return false;
		}
		
		if (___standByType == BotStandByType.goToSave)
		{
			__instance.method_1();
		}
		
		return false;
	}
}