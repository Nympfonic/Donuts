using Comfort.Common;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityToolkit.Extensions;

namespace Donuts.Patches;

[UsedImplicitly]
internal class GenerateStartingBotsPatch : ModulePatch
{
	/// <remarks>Target method has a return type of <see cref="IEnumerator"/> and has a single <see cref="Action"/> type parameter.</remarks>
	protected override MethodBase GetTargetMethod()
	{
		Type baseGameType = typeof(BaseLocalGame<EftGamePlayerOwner>);
		return AccessTools.Method(baseGameType, "vmethod_5");
	}
	
	[PatchPostfix]
	private static void PatchPostfix(ref IEnumerator __result)
	{
		if (!Singleton<AbstractGame>.Instance.InRaid) return;
		
		if (Singleton<DonutsRaidManager>.Instantiated && DonutsRaidManager.IsBotSpawningEnabled)
		{
			__result = AddIterationsToWaitForBotGenerators(__result); // Thanks danW
		}
	}
	
	private static IEnumerator AddIterationsToWaitForBotGenerators(IEnumerator originalTask)
	{
		DonutsRaidManager raidManager = Singleton<DonutsRaidManager>.Instance;
		
		Logger.LogWarning("Donuts is waiting for bot preparation to complete...");
		float startTime = Time.time;
		float lastLogTime = startTime;
		var waitInterval = new WaitForEndOfFrame();
		
		while (raidManager.OrNull()?.CanStartRaid == false)
		{
			yield return waitInterval; // Check at end of every frame
			
			float currentTime = Time.time;
			
			// Log every 2 seconds instead of every second to avoid spamming logs
			if (currentTime - lastLogTime >= 2f)
			{
				lastLogTime = currentTime;
				DonutsRaidManager.Logger.LogWarning("Donuts still waiting...");
			}
		}
		
		// Continue with the original task
		DonutsRaidManager.Logger.LogWarning("Donuts bot preparation is complete.");
		yield return originalTask;
	}
}