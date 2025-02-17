using Donuts.Utils;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;

using SpawnPointData = GClass649;

namespace Donuts.Patches.BotFixes;

[UsedImplicitly]
public class BotCreationDataClassPatches
{
	private static readonly Type _targetType = typeof(BotCreationDataClass);
	
	/*
	 * As of SPT 3.10.X
	 * ___list_0 is a List<GClass649> where GClass649 has a constructor signature:
	 * public GClass649(Vector3 pos, int corePointId, bool isUsed)
	 *	
	 * GClass649 also has 3 fields:
	 * public Vector3 position;
	 * public bool alreadyUsedToSpawn
	 * public int CorePointId;
	 *
	 * Purpose of this patch:
	 * Donuts currently only adds one spawn position to the BotCreationDataClass;
	 * The current method performs unnecessary code executions after the first GetPosition() call.
	 */
	[UsedImplicitly]
	public class GetPositionPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() =>
			AccessTools.Method(_targetType, nameof(BotCreationDataClass.GetPosition));

		[PatchPrefix]
		private static bool PatchPrefix(ref SpawnPointData __result, List<SpawnPointData> ___list_0)
		{
			__result = ___list_0.PickRandomElement();
			return false;
		}
	}
}