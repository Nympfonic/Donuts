using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace Donuts.Patches;

/// <summary>
/// Patch <see cref="Player.FirearmController.IsTriggerPressed"/> to prevent player from shooting while in Donuts' F9 menu.
/// </summary>
[UsedImplicitly]
internal class PlayerFireControlPatchGetter : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		Type playerType = typeof(Player.FirearmController);
		return AccessTools.PropertyGetter(playerType, nameof(Player.FirearmController.IsTriggerPressed));
	}
	
	[PatchPrefix]
	private static bool PatchPrefix(ref bool __result)
	{
		if (DefaultPluginVars.ShowGUI)
		{
			__result = false;
			return false;
		}
		return true; // Continue with the original getter
	}
}

[UsedImplicitly]
internal class PlayerFireControlPatchSetter : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		Type playerType = typeof(Player.FirearmController);
		return AccessTools.PropertySetter(playerType, nameof(Player.FirearmController.IsTriggerPressed));
	}
	
	[PatchPrefix]
	private static bool PatchPrefix(ref bool value)
	{
		if (DefaultPluginVars.ShowGUI)
		{
			value = false;
			return false;
		}
		return true; // Continue with the original setter
	}
}