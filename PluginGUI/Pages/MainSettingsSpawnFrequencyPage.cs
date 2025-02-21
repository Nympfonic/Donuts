using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.PluginGUI.ImGUIToolkit;

namespace Donuts.PluginGUI.Pages;

internal class MainSettingsSpawnFrequencyPage : ISettingsPage
{
	private int _pmcGroupChanceIndex;
	private int _scavGroupChanceIndex;
	
	public string Name => "Spawn Frequency";

	public MainSettingsSpawnFrequencyPage()
	{
		InitializeDropdownIndices();
		PluginGUIComponent.OnOpen += InitializeDropdownIndices;
		PluginGUIComponent.OnResetToDefaults += InitializeDropdownIndices;
	}
	
	public void Draw()
	{
		GUILayout.BeginHorizontal();
		GUILayout.BeginVertical();

		HardCapEnabled.Value = Toggle(HardCapEnabled.Name, HardCapEnabled.ToolTipText, HardCapEnabled.Value);
		GUILayout.Space(10);

		useTimeBasedHardStop.Value = Toggle(useTimeBasedHardStop.Name, useTimeBasedHardStop.ToolTipText,
			useTimeBasedHardStop.Value);
		GUILayout.Space(10);

		hardStopOptionPMC.Value = Toggle(hardStopOptionPMC.Name, hardStopOptionPMC.ToolTipText, hardStopOptionPMC.Value);
		GUILayout.Space(10);

		if (useTimeBasedHardStop.Value)
		{
			hardStopTimePMC.Value = Slider(hardStopTimePMC.Name, hardStopTimePMC.ToolTipText, hardStopTimePMC.Value, 0, 10000);
		}
		else
		{
			hardStopPercentPMC.Value = Slider(hardStopPercentPMC.Name, hardStopPercentPMC.ToolTipText,
				hardStopPercentPMC.Value, 0, 100);
		}

		GUILayout.Space(10);

		hardStopOptionSCAV.Value = Toggle(hardStopOptionSCAV.Name, hardStopOptionSCAV.ToolTipText, hardStopOptionSCAV.Value);
		GUILayout.Space(10);

		if (useTimeBasedHardStop.Value)
		{
			hardStopTimeSCAV.Value = Slider(hardStopTimeSCAV.Name, hardStopTimeSCAV.ToolTipText, hardStopTimeSCAV.Value,
				0, 10000);
		}
		else
		{
			hardStopPercentSCAV.Value = Slider(hardStopPercentSCAV.Name, hardStopPercentSCAV.ToolTipText,
				hardStopPercentSCAV.Value, 0, 100);
		}

		GUILayout.Space(10);

		maxRespawnsPMC.Value = Slider(maxRespawnsPMC.Name, maxRespawnsPMC.ToolTipText, maxRespawnsPMC.Value, 0, 100);
		GUILayout.Space(10);

		maxRespawnsSCAV.Value = Slider(maxRespawnsSCAV.Name, maxRespawnsSCAV.ToolTipText, maxRespawnsSCAV.Value, 0, 100);
		GUILayout.Space(10);

		GUILayout.EndVertical();
		GUILayout.BeginVertical();

		coolDownTimer.Value = Slider(coolDownTimer.Name, coolDownTimer.ToolTipText, coolDownTimer.Value, 0f, 1000f);
		GUILayout.Space(10);

		hotspotBoostPMC.Value = Toggle(hotspotBoostPMC.Name, hotspotBoostPMC.ToolTipText, hotspotBoostPMC.Value);
		GUILayout.Space(10);

		hotspotBoostSCAV.Value = Toggle(hotspotBoostSCAV.Name, hotspotBoostSCAV.ToolTipText, hotspotBoostSCAV.Value);
		GUILayout.Space(10);

		hotspotIgnoreHardCapPMC.Value = Toggle(hotspotIgnoreHardCapPMC.Name, hotspotIgnoreHardCapPMC.ToolTipText,
			hotspotIgnoreHardCapPMC.Value);
		GUILayout.Space(10);

		hotspotIgnoreHardCapSCAV.Value = Toggle(hotspotIgnoreHardCapSCAV.Name, hotspotIgnoreHardCapSCAV.ToolTipText,
			hotspotIgnoreHardCapSCAV.Value);
		GUILayout.Space(10);

		GUILayout.EndVertical();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.BeginVertical();

		_pmcGroupChanceIndex = Dropdown(pmcGroupChance, _pmcGroupChanceIndex);
		pmcGroupChance.Value = pmcGroupChance.Options[_pmcGroupChanceIndex];
		GUILayout.Space(10);

		_scavGroupChanceIndex = Dropdown(scavGroupChance, _scavGroupChanceIndex);
		scavGroupChance.Value = scavGroupChance.Options[_scavGroupChanceIndex];

		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
	}

	private void InitializeDropdownIndices()
	{
		_pmcGroupChanceIndex = FindSettingIndex(pmcGroupChance);
		_scavGroupChanceIndex = FindSettingIndex(scavGroupChance);
	}
}