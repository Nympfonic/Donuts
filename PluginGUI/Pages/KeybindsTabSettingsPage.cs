using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.PluginGUI.ImGUIToolkit;

namespace Donuts.PluginGUI.Pages;

internal class KeybindsTabSettingsPage : ISettingsPage
{
    public string Name => "Keybinds";

    public void Draw()
    {
        // Draw general spawn settings
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();

        // Draw Keybind settings
        CreateSpawnMarkerKey.Value = KeybindField(CreateSpawnMarkerKey.Name, CreateSpawnMarkerKey.ToolTipText,
            CreateSpawnMarkerKey.Value);
        DeleteSpawnMarkerKey.Value = KeybindField(DeleteSpawnMarkerKey.Name, DeleteSpawnMarkerKey.ToolTipText,
            DeleteSpawnMarkerKey.Value);
        WriteToFileKey.Value = KeybindField(WriteToFileKey.Name, WriteToFileKey.ToolTipText, WriteToFileKey.Value);

        // Draw Toggle setting
        saveNewFileOnly.Value = Toggle(saveNewFileOnly.Name, saveNewFileOnly.ToolTipText, saveNewFileOnly.Value);

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }
}