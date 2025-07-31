using Cysharp.Text;
using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.PluginGUI.ImGUIToolkit;

namespace Donuts.PluginGUI.Pages;

internal class MainSettingsGeneralPage : ISettingsPage
{
	private int _pmcScenarioSelectionIndex;
	private int _scavScenarioSelectionIndex;
	
	// Flag to check if scenarios are loaded
	private bool _scenariosLoaded;
	
	public string Name => "General";
	
	public MainSettingsGeneralPage()
	{
		InitializeDropdownIndices();
		PluginGUIComponent.OnOpen += InitializeDropdownIndices;
		PluginGUIComponent.OnResetToDefaults += InitializeDropdownIndices;
	}
	
	public void Draw()
	{
		GUILayout.BeginHorizontal();
		GUILayout.BeginVertical();
		
		PluginEnabled.Value = Toggle(PluginEnabled.Name,
			PluginEnabled.ToolTipText, PluginEnabled.Value);
		GUILayout.Space(10);
		
		DespawnEnabledPMC.Value = Toggle(DespawnEnabledPMC.Name,
			DespawnEnabledPMC.ToolTipText, DespawnEnabledPMC.Value);
		GUILayout.Space(10);
		
		DespawnEnabledSCAV.Value = Toggle(DespawnEnabledSCAV.Name,
			DespawnEnabledSCAV.ToolTipText, DespawnEnabledSCAV.Value);
		GUILayout.Space(10);
		
		despawnInterval.Value = Slider(despawnInterval.Name,
			despawnInterval.ToolTipText, despawnInterval.Value, 0f, 1000f);
		GUILayout.Space(10);
		
		ShowRandomFolderChoice.Value = Toggle(ShowRandomFolderChoice.Name,
			ShowRandomFolderChoice.ToolTipText, ShowRandomFolderChoice.Value);
		GUILayout.Space(10);
		
		battleStateCoolDown.Value = Slider(battleStateCoolDown.Name,
			battleStateCoolDown.ToolTipText, battleStateCoolDown.Value, 0f, 1000f);
		GUILayout.Space(10);
		
		if (_scenariosLoaded)
		{
			_pmcScenarioSelectionIndex = Dropdown(pmcScenarioSelection, _pmcScenarioSelectionIndex);
			pmcScenarioSelection.Value = pmcScenarioSelection.Options[_pmcScenarioSelectionIndex];
			GUILayout.Space(10);
			
			_scavScenarioSelectionIndex = Dropdown(scavScenarioSelection, _scavScenarioSelectionIndex);
			scavScenarioSelection.Value = scavScenarioSelection.Options[_scavScenarioSelectionIndex];
		}
		else
		{
			GUILayout.Label("Loading PMC scenarios...");
			GUILayout.Label("Loading SCAV scenarios...");
		}
		
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
	}
	
	private void InitializeDropdownIndices()
	{
		if (HavePmcScenarioSelectionOptions())
		{
			_pmcScenarioSelectionIndex = FindSettingIndex(pmcScenarioSelection);
			if (_pmcScenarioSelectionIndex == -1)
			{
				if (debugLogging.Value)
				{
					DonutsPlugin.Logger.LogDebug("Warning: pmcScenarioSelectionIndex not found, defaulting to 0");
				}
				
				_pmcScenarioSelectionIndex = 0;
			}
		}
		else _pmcScenarioSelectionIndex = 0;
		
		if (HaveScavScenarioSelectionOptions())
		{
			_scavScenarioSelectionIndex = FindSettingIndex(scavScenarioSelection);
			if (_scavScenarioSelectionIndex == -1)
			{
				if (debugLogging.Value)
				{
					DonutsPlugin.Logger.LogDebug("Warning: scavScenarioSelectionIndex not found, defaulting to 0");
				}
				
				_scavScenarioSelectionIndex = 0;
			}
		}
		else _scavScenarioSelectionIndex = 0;
		
		_scenariosLoaded = HavePmcScenarioSelectionOptions() && HaveScavScenarioSelectionOptions();
		if (debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("{0}: {1}", nameof(_scenariosLoaded), _scenariosLoaded);
			DonutsPlugin.Logger.LogDebug(sb.ToString());
		}
	}
	
	private static bool HavePmcScenarioSelectionOptions() => pmcScenarioSelection?.Options?.Length > 0;
	private static bool HaveScavScenarioSelectionOptions() => scavScenarioSelection?.Options?.Length > 0;
}