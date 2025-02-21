using Comfort.Common;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Reflection;

namespace Donuts.Patches;

[UsedImplicitly]
internal class GenerateStartingBotsPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		// Type baseGameType = typeof(BaseLocalGame<EftGamePlayerOwner>);
		Type gameType = DonutsPlugin.FikaEnabled
			? AccessTools.TypeByName("Fika.Core.Coop.GameMode.CoopGame")
			: typeof(LocalGame);
		
		return AccessTools.Method(gameType, "vmethod_5");
	}
	
	[PatchPostfix]
	private static void PatchPostfix(ref IEnumerator __result)
	{
		if (!Singleton<AbstractGame>.Instance.InRaid) return;
		
		if (Singleton<DonutsRaidManager>.Instantiated && DonutsRaidManager.IsBotSpawningEnabled)
		{
			__result = Singleton<DonutsRaidManager>.Instance.DonutsRaidLoadingTask(__result); // Thanks danW
		}
	}
}