using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Donuts.Patches;

[UsedImplicitly]
public class GetClosestGroupPointPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() => AccessTools.FirstMethod(typeof(AICoversData), IsTargetMethod);

	private static bool IsTargetMethod(MethodInfo mi)
	{
		return mi.Name == nameof(AICoversData.GetClosest) && mi.GetParameters().Length == 1;
	}

	[PatchPrefix]
	private static bool PatchPrefix(Vector3 pos, List<GroupPoint> ___Points, ref GroupPoint __result)
	{
		if (___Points.Count == 0)
		{
			__result = null;
			return false;
		}

		int nearEntity = GetNearEntity(___Points, ref pos);
		__result = ___Points[nearEntity];
		return false;
	}

	private static int GetNearEntity(List<GroupPoint> points, ref Vector3 pos)
	{
		int count = points.Count;
		var nearest = float.MaxValue;
		var index = 0;
		for (var i = 0; i < count; i++)
		{
			Vector3 vector = points[i].Position - pos;
			float sqrDistance = vector.x * vector.x + vector.y * vector.y + vector.z * vector.z;
			if (sqrDistance <= nearest)
			{
				nearest = sqrDistance;
				index = i;
			}
		}
		return index;
	}
}