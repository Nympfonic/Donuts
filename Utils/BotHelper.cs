using Cysharp.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
				DonutsPlugin.Logger.LogErrorDetailed($"Unsupported difficulty setting: {difficultySetting}",
					nameof(BotHelper), nameof(GetSettingDifficulties));
				difficulties = _invalidDifficulty;
				break;
		}
		return difficulties.AsReadOnly();
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
}