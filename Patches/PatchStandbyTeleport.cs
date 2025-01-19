using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityToolkit.Utils;

namespace Donuts.Patches;

[UsedImplicitly]
[DisablePatch]
internal class PatchStandbyTeleport : ModulePatch
{
	private static MethodInfo _method;
	private static readonly Dictionary<BotStandBy, Action> _methodDelegates = [];
	
	internal static Dictionary<BotStandBy, Action> MethodDelegates => _methodDelegates;

	protected override MethodBase GetTargetMethod()
	{
		Type standbyClassType = typeof(BotStandBy);
		_method = AccessTools.Method(standbyClassType, "method_1");
		return AccessTools.Method(standbyClassType, nameof(BotStandBy.UpdateNode));
	}

	[PatchPrefix]
	private static bool PatchPrefix(BotStandBy __instance, BotStandByType ___standByType, BotOwner ___botOwner_0)
	{
		// One-time setup cost for creating method delegates but subsequent invocations are much faster
		if (!_methodDelegates.ContainsKey(__instance))
		{
			var action = AccessTools.MethodDelegate<Action>(_method, __instance);
			_methodDelegates.Add(__instance, action);
		}
		
		if (!___botOwner_0.Settings.FileSettings.Mind.CAN_STAND_BY || !__instance.CanDoStandBy)
		{
			return false;
		}

		if (___standByType == BotStandByType.goToSave)
		{
			//_method.Invoke(__instance, []);
			_methodDelegates[__instance]();
		}

		return false;
	}
}