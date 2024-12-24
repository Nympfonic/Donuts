using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches;

/// <summary>
/// Clear static fields/properties so the next raid won't try to access null data, which could lead to issues and crashes.
/// </summary>
[UsedImplicitly]
internal class GameWorldDisposePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.Dispose));

	[PatchPostfix]
	private static void PatchPostfix()
	{
		PatchStandbyTeleport.MethodDelegates.Clear();
	}
}