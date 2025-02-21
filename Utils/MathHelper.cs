using System;
using System.Collections.Generic;
using System.Linq;

namespace Donuts.Utils;

internal static class MathHelper
{
	internal static int GetOutcomeWithProbability(double[] probabilities, int minGroupSize, int maxGroupSize)
	{
		double probabilitySum = probabilities.Sum();
		if (Math.Abs(probabilitySum - 1.0) > 0.0001)
		{
			throw new InvalidOperationException("Probabilities should sum up to 1.");
		}

		double probabilityThreshold = DonutsHelper.GetRandom().NextDouble();
		var cumulative = 0.0;
		int adjustedMaxCount = maxGroupSize - minGroupSize;
		for (var i = 0; i <= adjustedMaxCount; i++)
		{
			cumulative += probabilities[i];
			if (probabilityThreshold < cumulative)
			{
				return i;
			}
		}
		return adjustedMaxCount;
	}

	internal static double[] GetGroupProbabilities(IDictionary<string, int[]> groupChanceWeights, string groupChance)
	{
		if (!groupChanceWeights.TryGetValue(groupChance, out int[] relativeWeights))
		{
			throw new ArgumentException($"Invalid {nameof(groupChance)}: {groupChance}.");
		}

		double totalWeight = relativeWeights.Sum();
		var probabilities = new double[relativeWeights.Length];
		for (var i = 0; i < probabilities.Length; i++)
		{
			probabilities[i] = relativeWeights[i] / totalWeight;
		}
		return probabilities;
	}
}