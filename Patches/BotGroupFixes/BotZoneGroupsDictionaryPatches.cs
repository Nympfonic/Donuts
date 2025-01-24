using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

using BotZoneGroupData = GClass539;

namespace Donuts.Patches.BotGroupFixes;

[UsedImplicitly]
public class BotZoneGroupsDictionaryPatches
{
	private static readonly Type _targetType = typeof(BotZoneGroupsDictionary);
	
	/// <summary>
	/// Patches <see cref="BotZoneGroupsDictionary.TryGetValue(BotZone, EPlayerSide, WildSpawnType, out BotsGroup, bool)"/>
	/// to get rid of an if statement which calls Sum() on a list but does nothing with this.
	/// </summary>
	[UsedImplicitly]
	public class TryGetValuePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.FirstMethod(_targetType, IsTargetMethod);
		}
		
		private static bool IsTargetMethod(MethodInfo mi) => mi.Name == "TryGetValue" && mi.GetParameters().Length == 5;
		
		[PatchPrefix]
		private static bool PatchPrefix(
			BotZoneGroupsDictionary __instance,
			ref bool __result,
			BotZone zone,
			EPlayerSide side,
			WildSpawnType spawnType,
			out BotsGroup group,
			bool isBossOrFollower)
		{
			if (__instance.TryGetValue(zone, out BotZoneGroupData botZoneGroupData))
			{
				BotsGroup botsGroup = botZoneGroupData.Group(side, isBossOrFollower, spawnType);
				if (botsGroup != null && !botsGroup.Locked)
				{
					group = botsGroup;
					__result = true;
					return false;
				}
			}
			
			group = null;
			__result = false;
			return false;
		}
	}
}