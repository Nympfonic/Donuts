using Donuts.Utils;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;

namespace Donuts.Patches.BotCreationDataClassPatches;

[UsedImplicitly]
public partial class BotCreationDataClassPatches
{
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
			AccessTools.Method(typeof(BotCreationDataClass), nameof(BotCreationDataClass.GetPosition));

		[PatchPrefix]
		private static bool PatchPrefix(ref GClass649 __result, List<GClass649> ___list_0)
		{
			int count = ___list_0.Count;
			if (count == 0)
			{
				__result = null;
			}
			else if (count == 1)
			{
				__result = ___list_0[0];
			}
			
			__result = ___list_0.PickRandomElement();
			return false;
		}
	}
}