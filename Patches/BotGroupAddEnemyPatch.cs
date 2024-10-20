using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;

// Stolen from DanW's Questing Bots, thanks Dan!

namespace Donuts.Patches
{
	internal class BotGroupAddEnemyPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(BotsGroup), nameof(BotsGroup.AddEnemy));
		}

		[PatchPrefix]
		private static bool PatchPrefix(BotsGroup __instance, IPlayer person, EBotEnemyCause cause)
		{
			// Don't add invalid enemies
			if (person == null || (person.IsAI && person.AIData?.BotOwner?.GetPlayer == null))
			{
				return false;
			}

			// We only care about bot groups adding you as an enemy
			if (!person.IsYourPlayer)
			{
				return true;
			}

			// This only matters in Scav raids
			// TODO: This might also matter in PMC raids if a mod adds groups that are friendly to the player
			//if (!Aki.SinglePlayer.Utils.InRaid.RaidChangesUtil.IsScavRaid)
			//{
			//	return true;
			//}

			// We only care about one enemy cause
			if (cause != EBotEnemyCause.pmcBossKill)
			{
				return true;
			}

			// Get the ID's of all group members
			List<BotOwner> groupMemberList = [];
			for (int m = 0; m < __instance.MembersCount; m++)
			{
				groupMemberList.Add(__instance.Member(m));
			}
			//string[] groupMemberIDs = groupMemberList.Select(m => m.Profile.Id).ToArray();

			return true;
		}
	}
}
