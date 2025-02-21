using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.PluginGUI.ImGUIToolkit;

namespace Donuts.PluginGUI.Pages;

internal class MainSettingsBotAttributesPage : ISettingsPage
{
	private int _botDifficultiesPmcIndex;
	private int _botDifficultiesScavIndex;
	private int _botDifficultiesOtherIndex;
	
	private int _pmcFactionIndex;
	private int _forceAllBotTypeIndex;
	
	public string Name => "Bot Attributes";

	public MainSettingsBotAttributesPage()
	{
		InitializeDropdownIndices();
		PluginGUIComponent.OnOpen += InitializeDropdownIndices;
		PluginGUIComponent.OnResetToDefaults += InitializeDropdownIndices;
	}
	
	public void Draw()
	{
		// Draw other spawn settings
		GUILayout.BeginHorizontal();
		GUILayout.BeginVertical();

		// Add spacing before each control to ensure proper vertical alignment
		_botDifficultiesPmcIndex = Dropdown(botDifficultiesPMC, _botDifficultiesPmcIndex);
		botDifficultiesPMC.Value = botDifficultiesPMC.Options[_botDifficultiesPmcIndex];

		GUILayout.Space(10); // Add vertical space

		_botDifficultiesScavIndex = Dropdown(botDifficultiesSCAV, _botDifficultiesScavIndex);
		botDifficultiesSCAV.Value = botDifficultiesSCAV.Options[_botDifficultiesScavIndex];

		GUILayout.Space(10); // Add vertical space

		_botDifficultiesOtherIndex = Dropdown(botDifficultiesOther, _botDifficultiesOtherIndex);
		botDifficultiesOther.Value = botDifficultiesOther.Options[_botDifficultiesOtherIndex];

		GUILayout.Space(10); // Add vertical space

		_pmcFactionIndex = Dropdown(pmcFaction, _pmcFactionIndex);
		pmcFaction.Value = pmcFaction.Options[_pmcFactionIndex];

		GUILayout.Space(10); // Add vertical space

		_forceAllBotTypeIndex = Dropdown(forceAllBotType, _forceAllBotTypeIndex);
		forceAllBotType.Value = forceAllBotType.Options[_forceAllBotTypeIndex];

		GUILayout.Space(10); // Add vertical space

		pmcFactionRatio.Value = Slider(pmcFactionRatio.Name, pmcFactionRatio.ToolTipText, pmcFactionRatio.Value, 0, 100);

		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
	}

	private void InitializeDropdownIndices()
	{
		_botDifficultiesPmcIndex = FindSettingIndex(botDifficultiesPMC);
		_botDifficultiesScavIndex = FindSettingIndex(botDifficultiesSCAV);
		_botDifficultiesOtherIndex = FindSettingIndex(botDifficultiesOther);

		_pmcFactionIndex = FindSettingIndex(pmcFaction);
		_forceAllBotTypeIndex = FindSettingIndex(forceAllBotType);
	}
}