using Comfort.Common;
using Cysharp.Text;
using EFT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Random = UnityEngine.Random;

namespace Donuts.Utils;

internal static class BotHelper
{
	// Cache to avoid constantly allocating memory
	private static readonly List<BotDifficulty> _invalidDifficulty = [];
	private static readonly List<BotDifficulty> _asOnlineDifficulties = [BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard];
	private static readonly List<BotDifficulty> _easyDifficulty = [BotDifficulty.easy];
	private static readonly List<BotDifficulty> _normalDifficulty = [BotDifficulty.normal];
	private static readonly List<BotDifficulty> _hardDifficulty = [BotDifficulty.hard];
	private static readonly List<BotDifficulty> _impossibleDifficulty = [BotDifficulty.impossible];
	
	private static readonly string[] _groupChances = ["None", "Low", "Default", "High", "Max"];

	internal static ReadOnlyCollection<BotDifficulty> GetSettingDifficulties(string difficultySetting)
	{
		List<BotDifficulty> difficulties;
		difficultySetting = difficultySetting.ToLower();
		switch (difficultySetting)
		{
			case "asonline":
				difficulties = _asOnlineDifficulties;
				break;
			case "easy":
				difficulties = _easyDifficulty;
				break;
			case "normal":
				difficulties = _normalDifficulty;
				break;
			case "hard":
				difficulties = _hardDifficulty;
				break;
			case "impossible":
				difficulties = _impossibleDifficulty;
				break;
			default:
				using (var sb = ZString.CreateUtf8StringBuilder())
				{
					sb.AppendFormat("{0} {1}::{2}: Unsupported difficulty setting: {3}", DateTime.Now.ToLongTimeString(),
						nameof(BotHelper), nameof(GetSettingDifficulties), difficultySetting);
					DonutsPlugin.Logger.LogError(sb.ToString());
				}
				difficulties = _invalidDifficulty;
				break;
		}
		return difficulties.AsReadOnly();
	}

	/// <summary>
	/// Gets the number of alive bots. A predicate can be specified to filter for specific bot types, but is optional.
	/// If there's no predicate, find just scavs/pmc's
	/// </summary>
	internal static int GetAliveBotsCount(Func<WildSpawnType, bool> predicate = null)
	{
		GameWorld gameWorld = Singleton<GameWorld>.Instance;
		if (gameWorld == null)
		{
			return -1;
		}

		var count = 0;
		List<Player> allAlivePlayers = gameWorld.AllAlivePlayersList;
		if (predicate == null && DefaultPluginVars.HardCapIgnoresBoss.Value)
		{
			List<WildSpawnType> filteredTypes = [WildSpawnType.assault, WildSpawnType.pmcBEAR, WildSpawnType.pmcUSEC];
			allAlivePlayers = gameWorld.AllAlivePlayersList.Where((p) => filteredTypes.Contains(p.Profile.Info.Settings.Role)).ToList();
		}
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			if (player == null || !player.IsAI) continue;

			WildSpawnType role = player.Profile.Info.Settings.Role;
			if (predicate == null || predicate(role))
			{
				count++;
			}
		}
		return count;
	}

	internal static int GetBotGroupSize(string pluginGroupChance, int minGroupSize, int maxGroupSize, int maxCap = int.MaxValue)
	{
		if (maxGroupSize < minGroupSize)
		{
			throw new ArgumentException("The max group size is less than the min group size");
		}
		
		if (pluginGroupChance == "Random")
		{
			pluginGroupChance = _groupChances.PickRandomElement();
		}

		int groupSize = pluginGroupChance switch
		{
			"None" => minGroupSize,
			"Max" => maxGroupSize,
			_ => GetGroupChance(pluginGroupChance, minGroupSize, maxGroupSize)
		};

		int clampedValue = Math.Min(groupSize, maxCap);
		return clampedValue;
	}

	private static int GetGroupChance(string pmcGroupChance, int minGroupSize, int maxGroupSize)
	{
		if (maxGroupSize < minGroupSize)
		{
			throw new ArgumentException("The max group size is less than the min group size");
		}
		
		double[] probabilities = MathHelper.GetGroupProbabilities(DefaultPluginVars.GroupChanceWeights, pmcGroupChance);
		
		return MathHelper.GetOutcomeWithProbability(probabilities, minGroupSize, maxGroupSize) + minGroupSize;
	}

	/// <summary>
	/// Generates a number between the <see cref="min"/> and <see cref="max"/> values,
	/// then clamps the number by <see cref="clampMax"/>.
	/// </summary>
	/// <param name="min">The inclusive lower bound.</param>
	/// <param name="max">The inclusive upper bound.</param>
	/// <param name="clampMax">The result, if higher than <see cref="max"/>, will be clamped to this value.</param>
	internal static int GetRandomBotCap(int min, int max, int clampMax = int.MaxValue)
	{
		if (max < min)
		{
			throw new ArgumentException("The max must be greater than min");
		}
		
		int botCap = Random.Range(min, max);
		return Math.Min(botCap, clampMax);
	}
}