using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;

namespace Donuts.Patches;

[UsedImplicitly]
internal class CoverPointMasterNullRef : ModulePatch
{
	private static readonly List<CustomNavigationPoint> _emptyNavPoints = [];
	
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(CoverPointMaster), nameof(CoverPointMaster.GetClosePoints));
	}

	[PatchPrefix]
	private static bool PatchPrefix(BotOwner bot, ref List<CustomNavigationPoint> __result)
	{
		if (bot == null || bot.Covers == null)
		{
			__result = _emptyNavPoints; // Return an empty list or handle as needed
			return false;
		}

		return true;
	}
}