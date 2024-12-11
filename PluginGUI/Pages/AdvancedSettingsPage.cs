using Donuts.Utils;
using UnityEngine;
using static Donuts.DefaultPluginVars;

namespace Donuts.PluginGUI.Pages;

internal class AdvancedSettingsPage : ISettingsPage
{
    public string Name => "Advanced Settings";

    public void Draw()
    {
        GUILayout.Space(30);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();

        // Slider for maxRaidDelay
        maxRaidDelay.Value = ImGUIToolkit.Slider(maxRaidDelay.Name, maxRaidDelay.ToolTipText, maxRaidDelay.Value,
	        maxRaidDelay.MinValue, maxRaidDelay.MaxValue);

        // Slider for replenishInterval
        replenishInterval.Value = ImGUIToolkit.Slider(replenishInterval.Name, replenishInterval.ToolTipText,
            replenishInterval.Value, replenishInterval.MinValue, replenishInterval.MaxValue);

        // Slider for maxSpawnTriesPerBot
        maxSpawnTriesPerBot.Value = ImGUIToolkit.Slider(maxSpawnTriesPerBot.Name, maxSpawnTriesPerBot.ToolTipText,
            maxSpawnTriesPerBot.Value, maxSpawnTriesPerBot.MinValue, maxSpawnTriesPerBot.MaxValue);

        // Slider for despawnInterval
        despawnInterval.Value = ImGUIToolkit.Slider(despawnInterval.Name, despawnInterval.ToolTipText,
            despawnInterval.Value, despawnInterval.MinValue, despawnInterval.MaxValue);

        groupWeightDistroLow.Value = ImGUIToolkit.TextField(groupWeightDistroLow.Name, groupWeightDistroLow.ToolTipText,
	        groupWeightDistroLow.Value);
        groupWeightDistroDefault.Value = ImGUIToolkit.TextField(groupWeightDistroDefault.Name,
	        groupWeightDistroDefault.ToolTipText, groupWeightDistroDefault.Value);
        groupWeightDistroHigh.Value = ImGUIToolkit.TextField(groupWeightDistroHigh.Name,
	        groupWeightDistroHigh.ToolTipText, groupWeightDistroHigh.Value);

        GUILayout.Space(150);

        // Reset to Default Values button
        if (GUILayout.Button("Reset to Default Values", PluginGUIComponent.CloseButtonStyle, GUILayout.Width(250),
	        GUILayout.Height(50)))
        {
	        ResetToDefault();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private static void ResetToDefault()
    {
	    PluginGUIComponent.ResetSettingsToDefaults();
	    const string msg = "All Donuts settings have been reset to default values, but they still need to be saved.";
	    DonutsHelper.NotifyModSettingsStatus(msg);
	    DonutsPlugin.Logger.LogWarning(msg);
	    PluginGUIComponent.RestartPluginGUI();
    }
}