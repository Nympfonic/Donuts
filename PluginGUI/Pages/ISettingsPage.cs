namespace Donuts.PluginGUI.Pages;

public interface ISettingsPage
{
    abstract string Name { get; }
    void Draw();
}