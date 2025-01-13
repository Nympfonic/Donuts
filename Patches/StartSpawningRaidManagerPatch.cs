using Comfort.Common;
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
		Logger.LogDebug($"{nameof(DonutsPlugin.FikaEnabled)}: {DonutsPlugin.FikaEnabled.ToString()}");
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
		await DonutsRaidManager.Initialize();
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.StartBotSpawnController();
	}
}