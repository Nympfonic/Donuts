using UnityEngine;
using static Donuts.DefaultPluginVars;

namespace Donuts.PluginGUI.Pages;

internal class DebuggingSettingsPage : ISettingsPage
{
    public string Name => "Debugging";
    
    public void Draw()
    {
        GUILayout.Space(30);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        
        // Add toggles for DebugGizmos and gizmoRealSize
        debugLogging.Value = ImGUIToolkit.Toggle(debugLogging.Name, debugLogging.ToolTipText, debugLogging.Value);
        GUILayout.Space(10);
        
        DebugGizmos.Value = ImGUIToolkit.Toggle(DebugGizmos.Name, DebugGizmos.ToolTipText, DebugGizmos.Value);
        GUILayout.Space(10);
        
        gizmoRealSize.Value = ImGUIToolkit.Toggle(gizmoRealSize.Name, gizmoRealSize.ToolTipText, gizmoRealSize.Value);
        GUILayout.Space(10);
        
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }
}