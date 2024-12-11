using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.PluginGUI.ImGUIToolkit;

namespace Donuts.PluginGUI.Pages;

internal class SpawnSetupTabSettingsPage : ISettingsPage
{
	private int _wildSpawnsIndex;

	public string Name => "Spawn Setup";

	public SpawnSetupTabSettingsPage()
	{
		InitializeDropdownIndices();
		PluginGUIComponent.OnResetToDefaults += InitializeDropdownIndices;
	}

	public void Draw()
	{
		// Draw advanced spawn settings
		GUILayout.BeginHorizontal();

		// First column
		GUILayout.BeginVertical();

		// Define the position and size for the spawnName text field
		spawnName.Value = TextField(spawnName.Name, spawnName.ToolTipText, spawnName.Value);
		GUILayout.Space(10);

		groupNum.Value = Slider(groupNum.Name, groupNum.ToolTipText,
			groupNum.Value, groupNum.MinValue, groupNum.MaxValue);
		GUILayout.Space(10);

		// Dropdown for wildSpawns
		_wildSpawnsIndex = ImGUIToolkit.Dropdown(wildSpawns, _wildSpawnsIndex);
		wildSpawns.Value = wildSpawns.Options[_wildSpawnsIndex];
		GUILayout.Space(10);

		minSpawnDist.Value = Slider(minSpawnDist.Name, minSpawnDist.ToolTipText,
			minSpawnDist.Value, minSpawnDist.MinValue, minSpawnDist.MaxValue);
		GUILayout.Space(10);

		maxSpawnDist.Value = Slider(maxSpawnDist.Name, maxSpawnDist.ToolTipText,
			maxSpawnDist.Value, maxSpawnDist.MinValue, maxSpawnDist.MaxValue);
		GUILayout.Space(10);

		botTriggerDistance.Value = Slider(botTriggerDistance.Name, botTriggerDistance.ToolTipText,
			botTriggerDistance.Value, botTriggerDistance.MinValue, botTriggerDistance.MaxValue);
		GUILayout.Space(10);

		GUILayout.EndVertical();

		// Second column
		GUILayout.BeginVertical();

		botTimerTrigger.Value = Slider(botTimerTrigger.Name, botTimerTrigger.ToolTipText,
			botTimerTrigger.Value, botTimerTrigger.MinValue, botTimerTrigger.MaxValue);
		GUILayout.Space(10);

		maxRandNumBots.Value = Slider(maxRandNumBots.Name, maxRandNumBots.ToolTipText,
			maxRandNumBots.Value, maxRandNumBots.MinValue, maxRandNumBots.MaxValue);
		GUILayout.Space(10);

		spawnChance.Value = Slider(spawnChance.Name, spawnChance.ToolTipText,
			spawnChance.Value, spawnChance.MinValue, spawnChance.MaxValue);
		GUILayout.Space(10);

		maxSpawnsBeforeCooldown.Value = Slider(maxSpawnsBeforeCooldown.Name,
			maxSpawnsBeforeCooldown.ToolTipText, maxSpawnsBeforeCooldown.Value,
			maxSpawnsBeforeCooldown.MinValue, maxSpawnsBeforeCooldown.MaxValue);
		GUILayout.Space(10);

		ignoreTimerFirstSpawn.Value = Toggle(
			ignoreTimerFirstSpawn.Name,
			ignoreTimerFirstSpawn.ToolTipText,
			ignoreTimerFirstSpawn.Value
		);
		GUILayout.Space(10);

		minSpawnDistanceFromPlayer.Value = Slider(minSpawnDistanceFromPlayer.Name,
			minSpawnDistanceFromPlayer.ToolTipText, minSpawnDistanceFromPlayer.Value,
			minSpawnDistanceFromPlayer.MinValue, minSpawnDistanceFromPlayer.MaxValue);
		GUILayout.Space(10);

		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
	}

	private void InitializeDropdownIndices()
	{
		_wildSpawnsIndex = FindSettingIndex(wildSpawns);
	}
}