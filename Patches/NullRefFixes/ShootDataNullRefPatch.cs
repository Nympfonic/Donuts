using Cysharp.Text;
using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Donuts.Patches.NullRefFixes;

/// <summary>
/// Patches a <see cref="ShootData"/> method to prevent NREs.
/// </summary>
[UsedImplicitly]
internal class ShootDataNullRefPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ShootData), nameof(ShootData.method_0));
	
	[PatchPrefix]
	private static bool PatchPrefix(ShootData __instance, BotOwner ____owner)
	{
		// Check for null references in necessary fields
		if (____owner == null)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("BotOwner ID {0} -> ShootData.method_0(): _owner is null.", ____owner.Id.ToString());
				DonutsPlugin.Logger.LogErrorDetailed(sb.ToString(), nameof(ShootDataNullRefPatch), nameof(PatchPrefix));
			}
			
			return false;
		}
		
		if (____owner.WeaponRoot == null)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("BotOwner ID {0} -> ShootData.method_0(): _owner.WeaponRoot is null.", ____owner.Id.ToString());
				DonutsPlugin.Logger.LogErrorDetailed(sb.ToString(), nameof(ShootDataNullRefPatch), nameof(PatchPrefix));
			}
			
			return false;
		}
		
		return true;
	}
}