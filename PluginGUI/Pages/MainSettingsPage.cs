namespace Donuts.PluginGUI.Pages;

internal class MainSettingsPage : TabContainerPage
{
	public override string Name => "Main Settings";

	public MainSettingsPage() : base(PluginGUIComponent.SubTabButtonStyle, PluginGUIComponent.SubTabButtonActiveStyle)
	{
		Tabs.Add(new MainSettingsGeneralPage());
		Tabs.Add(new MainSettingsSpawnFrequencyPage());
		Tabs.Add(new MainSettingsBotAttributesPage());
	}
}