namespace Donuts.PluginGUI.Pages;

internal class SpawnPointMakerSettingsPage : TabContainerPage
{
    public override string Name => "Spawn Point Maker";

    public SpawnPointMakerSettingsPage() : base(PluginGUIComponent.SubTabButtonStyle, PluginGUIComponent.SubTabButtonActiveStyle)
    {
        Tabs.Add(new KeybindsTabSettingsPage());
        Tabs.Add(new SpawnSetupTabSettingsPage());
    }
}