﻿using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Bots;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches;

/// <summary>
/// Re-initializes each new game
/// </summary>
[UsedImplicitly]
internal class StartSpawningRaidManagerPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));

	[PatchPostfix]
	private static void PatchPostfix()
	{
		if (DonutsPlugin.FikaEnabled)
		{
			InitializeRaidManagerFika().Forget();
		}
		else
		{
			if (!Singleton<DonutsRaidManager>.Instantiated)
			{
				Logger.LogError($"Singleton<{nameof(DonutsRaidManager)}> is not instantiated");
				return;
			}
			
			MonoBehaviourSingleton<DonutsRaidManager>.Instance.StartBotSpawnController();
		}
	}

	private static async UniTaskVoid InitializeRaidManagerFika()
	{
        if (!DonutsRaidManager.IsBotSpawningEnabled)
        {
#if DEBUG
            Logger.LogInfo("Running as Fika client or something catastrophic happened to the BotsController, skipping DonutsRaidManager::Initialize"); 
#endif
            return;
        }

		await DonutsRaidManager.Initialize();
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.StartBotSpawnController();
	}
}