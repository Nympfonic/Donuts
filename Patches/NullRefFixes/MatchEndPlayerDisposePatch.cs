using Donuts.Utils;
using EFT;
using EFT.AssetsManager;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Donuts.Patches.NullRefFixes;

/// <summary>
/// Patches a static <see cref="BaseLocalGame{TPlayerOwner}"/> method, which handles disposing of player game objects at
/// the end of a raid, to avoid NREs.
/// </summary>
[UsedImplicitly]
internal class MatchEndPlayerDisposePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		// Method used by SPT for finding BaseLocalGame
		return AccessTools.Method(typeof(BaseLocalGame<EftGamePlayerOwner>),
			nameof(BaseLocalGame<EftGamePlayerOwner>.smethod_4));
	}

	[PatchPrefix]
	private static bool PatchPrefix(IDictionary<string, Player> players)
	{
		foreach (Player player in players.Values)
		{
			if (player == null) continue;

			try
			{
				player.Dispose();
				AssetPoolObject.ReturnToPool(player.gameObject);
			}
			catch (Exception ex)
			{
				DonutsPlugin.Logger.LogException(nameof(MatchEndPlayerDisposePatch), nameof(PatchPrefix), ex);
			}
		}
		players.Clear();

		return false;
	}
}