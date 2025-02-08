using Donuts.Models;
using EFT.UI;
using JetBrains.Annotations;
using SPT.SinglePlayer.Utils.InRaid;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Donuts.Bots;

public static class PresetSelector
{
	[CanBeNull]
	public static string GetWeightedScenarioSelection()
	{
		string scenarioSelection = DefaultPluginVars.pmcScenarioSelection.Value;
		if (RaidChangesUtil.IsScavRaid)
		{
			if (DefaultPluginVars.debugLogging.Value)
			{
				ConsoleScreen.LogWarning("This is a Scav raid; using Scav raid preset selector");
			}
			
			scenarioSelection = DefaultPluginVars.scavScenarioSelection.Value;
		}

		Folder selectedScenario = DefaultPluginVars.PmcScenarios.FindScenario(scenarioSelection)
			?? DefaultPluginVars.PmcRandomScenarios.FindScenario(scenarioSelection);

		return selectedScenario != null ? SelectPreset(selectedScenario) : null;
	}

	[CanBeNull]
	private static Folder FindScenario([NotNull] this List<Folder> scenarios, [NotNull] string scenarioName)
	{
		foreach (Folder scenario in scenarios)
		{
			if (scenario.Name == scenarioName || scenario.RandomScenarioConfig == scenarioName)
			{
				return scenario;
			}
		}
		return null;
	}
	
	[CanBeNull]
	private static string SelectPreset([NotNull] Folder folder)
	{
		List<Preset> presets = folder.Presets;
		if (presets == null || presets.Count == 0)
		{
			return folder.Name;
		}

		int totalWeight = presets.Sum(preset => preset.Weight);
		int randomWeight = Random.Range(0, totalWeight);

		var cumulativeWeight = 0;
		foreach (Preset preset in presets)
		{
			cumulativeWeight += preset.Weight;
			if (randomWeight < cumulativeWeight)
			{
				return preset.Name;
			}
		}

		// In case something goes wrong, return the last preset as a fallback
		Preset fallbackPreset = presets.LastOrDefault();
		if (fallbackPreset == null)
		{
			ConsoleScreen.LogError("ERROR: No Fallback Preset");
			return null;
		}

		ConsoleScreen.LogWarning($"Fallback Preset: {fallbackPreset.Name}");
		return fallbackPreset.Name;
	}
}