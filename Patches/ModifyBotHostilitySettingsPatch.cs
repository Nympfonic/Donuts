using Cysharp.Text;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Linq;
using System.Reflection;
using UnityToolkit.Utils;

namespace Donuts.Patches.PmcHostilityFix;

[UsedImplicitly]
public class ModifyBotHostilitySettingsPatch : ModulePatch
{
	private static readonly WildSpawnType[] _emptySpawnTypes = [];
	
	protected override MethodBase GetTargetMethod() =>
		AccessTools.Method(typeof(RaidSettings), nameof(RaidSettings.Apply));

	[PatchPostfix]
	private static void PatchPostfix(LocationSettingsClass.Location ____selectedLocation)
	{
		AdditionalHostilitySettings[] hostilitySettings =
			____selectedLocation.BotLocationModifier?.AdditionalHostilitySettings;

		if (hostilitySettings == null) return;

		foreach (AdditionalHostilitySettings setting in hostilitySettings)
		{
			if (setting.BotRole != WildSpawnType.pmcBEAR && setting.BotRole != WildSpawnType.pmcUSEC)
			{
				continue;
			}
			
#if DEBUG
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendLine(setting.BotRole);
			sb.AppendLine(setting.BearPlayerBehaviour.ToString());
			sb.AppendLine(setting.UsecPlayerBehaviour.ToString());
			sb.AppendLine(setting.SavagePlayerBehaviour.ToString());
			if (setting.AlwaysEnemies != null && setting.AlwaysEnemies.Length > 0)
			{
				sb.AppendLine(string.Join(", ", setting.AlwaysEnemies));
			}
			if (setting.AlwaysFriends != null && setting.AlwaysFriends.Length > 0)
			{
				sb.AppendLine(string.Join(", ", setting.AlwaysFriends));
			}
			if (setting.Neutral != null && setting.Neutral.Length > 0)
			{
				sb.AppendLine(string.Join(", ", setting.Neutral));
			}

			if (setting.Warn != null && setting.Warn.Length > 0)
			{
				sb.AppendLine(string.Join(", ", setting.Warn));
			}
			if (setting.ChancedEnemies != null && setting.ChancedEnemies.Length > 0)
			{
				for (var i = 0; i < setting.ChancedEnemies.Length; i++)
				{
					AdditionalHostilitySettings.ChancedEnemy enemy = setting.ChancedEnemies[i];
					sb.Append(enemy.Role);
					if (i + 1 < setting.ChancedEnemies.Length)
					{
						sb.Append(", ");
					}
				}
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