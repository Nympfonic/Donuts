using Comfort.Common;
using Donuts.Bots;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Donuts.Patches;

[UsedImplicitly]
internal class DelayedGameStartPatch : ModulePatch
{
	/// <summary>
	/// Target method has a return type of <see cref="IEnumerator"/> and has a single <see cref="Action"/> type parameter.
	/// </summary>
	protected override MethodBase GetTargetMethod()
	{
		Type baseGameType = typeof(BaseLocalGame<EftGamePlayerOwner>);
		return AccessTools.Method(baseGameType, "vmethod_5");
	}

	[PatchPostfix]
	private static void PatchPostfix(ref IEnumerator __result)
	{
		if (!Singleton<AbstractGame>.Instance.InRaid) return;

		if (DonutsRaidManager.IsBotSpawningEnabled)
		{
			__result = AddIterationsToWaitForBotGenerators(__result); // Thanks danW
		}
	}

	private static IEnumerator AddIterationsToWaitForBotGenerators(IEnumerator originalTask)
	{
		// Now also wait for all bots to be fully initialized
		Logger.LogWarning("Donuts is waiting for bot preparation to complete...");
		float startTime = Time.time;
		float lastLogTime = startTime;
		float maxWaitTime = DefaultPluginVars.maxRaidDelay.Value;
		WaitForEndOfFrame waitInterval = new();

		while (!DonutsRaidManager.IsBotPreparationComplete)
		{
			yield return waitInterval; // Check at end of every frame

			float currentTime = Time.time;
			if (currentTime - startTime >= maxWaitTime)
			{
				Logger.LogWarning("Max raid delay time reached. Proceeding with raid start, some bots might spawn late!");
				break;
			}

			if (currentTime - lastLogTime < 1f)
			{
				lastLogTime = currentTime;
				Logger.LogWarning("Donuts still waiting...");
			}
		}

		// Continue with the original task
		Logger.LogWarning("Donuts bot preparation is complete...");
		yield return originalTask;
	}
}