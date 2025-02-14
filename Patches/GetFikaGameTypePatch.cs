using Comfort.Common;
using Donuts.Spawning;
using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityToolkit.Utils;

namespace Donuts.Patches;

[UsedImplicitly]
[DisablePatch]
internal class GetFikaGameTypePatch : ModulePatch
{
	internal static Type FikaGameType { get; private set; }
	
	protected override MethodBase GetTargetMethod() =>
		AccessTools.Method(typeof(Singleton<AbstractGame>), nameof(Singleton<AbstractGame>.Create));

	[PatchPostfix]
	private static void PatchPostfix(AbstractGame instance)
	{
		Type type = instance.GetType();
		DonutsPlugin.Logger.LogDebugDetailed($"Game type: {type.Name}", nameof(GetFikaGameTypePatch), nameof(PatchPostfix));
		
		if (type == typeof(LocalGame))
		{
			DonutsRaidManager.Logger.LogError("Something went wrong! Fika is detected but LocalGame is being created instead of CoopGame?");
			return;
		}
		
		FikaGameType = type;
	}
}