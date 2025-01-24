using Cysharp.Text;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using UnityToolkit.Utils;

namespace Donuts.Patches;

/// <summary>
/// TODO: Allow customizing AdditionalHostilitySettings in-game with Donuts without needing to restart game
/// </summary>
[UsedImplicitly]
[DisablePatch]
public class ModifyBotHostilitySettingsPatch : ModulePatch
{
	// private static readonly WildSpawnType[] _emptySpawnTypes = [];
	
	protected override MethodBase GetTargetMethod()
	{
		Type targetType = typeof(BaseLocalGame<EftGamePlayerOwner>);
		return AccessTools.Property(targetType, "Location_0").GetSetMethod();
	}
	
	[PatchPostfix]
	private static void PatchPostfix(LocationSettingsClass.Location ___location_0)
	{
		AdditionalHostilitySettings[] hostilitySettings =
			___location_0.BotLocationModifier?.AdditionalHostilitySettings;
		
		if (hostilitySettings == null) return;
		
		foreach (AdditionalHostilitySettings setting in hostilitySettings)
		{
			if (setting.BotRole != WildSpawnType.pmcBEAR && setting.BotRole != WildSpawnType.pmcUSEC)
			{
				continue;
			}
			
#if DEBUG
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("BotRole: {0}\n", setting.BotRole);
			sb.AppendFormat("BearPlayerBehaviour: {0}\n", setting.BearPlayerBehaviour);
			sb.AppendFormat("UsecPlayerBehaviour: {0}\n", setting.UsecPlayerBehaviour);
			sb.AppendFormat("SavagePlayerBehaviour: {0}\n", setting.SavagePlayerBehaviour);
			if (setting.AlwaysEnemies != null && setting.AlwaysEnemies.Length > 0)
			{
				sb.AppendFormat("AlwaysEnemies:\n{0}\n", string.Join("\n", setting.AlwaysEnemies));
			}
			if (setting.AlwaysFriends != null && setting.AlwaysFriends.Length > 0)
			{
				sb.AppendFormat("AlwaysFriends:\n{0}\n", string.Join("\n", setting.AlwaysFriends));
			}
			if (setting.Neutral != null && setting.Neutral.Length > 0)
			{
				sb.AppendFormat("Neutral:\n{0}\n", string.Join("\n", setting.Neutral));
			}
			if (setting.Warn != null && setting.Warn.Length > 0)
			{
				sb.AppendFormat("Warn:\n{0}\n", string.Join("\n", setting.Warn));
			}
			if (setting.ChancedEnemies != null && setting.ChancedEnemies.Length > 0)
			{
				sb.AppendFormat("ChancedEnemies:\n{0}\n", string.Join("\n", setting.ChancedEnemies.Select(e => e.Role)));
			}
			Logger.LogDebug(sb.ToString());
#endif
			
			// if (setting.BotRole == WildSpawnType.pmcBEAR)
			// {
			// 	ModifyBearSettings(setting);
			// }
			// else if (setting.BotRole == WildSpawnType.pmcUSEC)
			// {
			// 	ModifyUsecSettings(setting);
			// }
			// setting.ChancedEnemies = [];
		}
	}

	private static void ModifyBearSettings(AdditionalHostilitySettings setting)
	{
		setting.BearPlayerBehaviour = EWarnBehaviour.AlwaysEnemies;
		setting.UsecPlayerBehaviour = EWarnBehaviour.AlwaysEnemies;
		setting.SavagePlayerBehaviour = EWarnBehaviour.AlwaysEnemies;
		setting.AlwaysEnemies =
		[
			WildSpawnType.pmcUSEC,
			WildSpawnType.pmcBEAR,
			WildSpawnType.assault,
			WildSpawnType.pmcBot,
			WildSpawnType.exUsec,
			WildSpawnType.bossBoar,
			WildSpawnType.bossBully,
			WildSpawnType.bossGluhar,
			WildSpawnType.bossKilla,
			WildSpawnType.bossKnight,
			WildSpawnType.bossKojaniy,
			WildSpawnType.bossKolontay,
			WildSpawnType.bossPartisan,
			WildSpawnType.bossSanitar,
			WildSpawnType.bossTagilla
		];
	}

	private static void ModifyUsecSettings(AdditionalHostilitySettings setting)
	{
		setting.BearPlayerBehaviour = EWarnBehaviour.AlwaysEnemies;
		setting.UsecPlayerBehaviour = EWarnBehaviour.AlwaysEnemies;
		setting.SavagePlayerBehaviour = EWarnBehaviour.AlwaysEnemies;
		setting.AlwaysEnemies =
		[
			WildSpawnType.pmcUSEC,
			WildSpawnType.pmcBEAR,
			WildSpawnType.assault,
			WildSpawnType.pmcBot,
			WildSpawnType.bossBoar,
			WildSpawnType.bossBully,
			WildSpawnType.bossGluhar,
			WildSpawnType.bossKilla,
			WildSpawnType.bossKnight,
			WildSpawnType.bossKojaniy,
			WildSpawnType.bossKolontay,
			WildSpawnType.bossPartisan,
			WildSpawnType.bossSanitar,
			WildSpawnType.bossTagilla
		];
	}
}