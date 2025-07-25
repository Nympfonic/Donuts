using Cysharp.Text;
using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches;

[UsedImplicitly]
internal class BotOwnerCreatePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(BotOwner), nameof(BotOwner.Create));
	}
	
	[PatchPostfix]
	private static void PatchPostfix(Player player)
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Unsubscribed human player-related event handlers from player {0} ({1})'s events",
				player.Profile.Nickname, player.ProfileId);
			Logger.LogDebugDetailed(sb.ToString(), nameof(BotOwnerCreatePatch), nameof(PatchPostfix));
		}
		
		player.BeingHitAction -= DonutsRaidManager.TakingDamageCombatCooldown;
		player.OnPlayerDeadOrUnspawn -= DonutsRaidManager.DisposePlayerSubscriptions;
	}
}