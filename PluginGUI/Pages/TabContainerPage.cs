using System.Collections.Generic;
using UnityEngine;

namespace Donuts.PluginGUI.Pages;

public abstract class TabContainerPage : ISettingsPage
{
	private readonly GUIStyle _tabStyle;
	private readonly GUIStyle _tabActiveStyle;
	private int _selectedTabIndex;
	
	public abstract string Name { get; }
	protected List<ISettingsPage> Tabs { get; } = [];

	protected TabContainerPage(GUIStyle tabStyle, GUIStyle tabActiveStyle)
	{
		_tabStyle = tabStyle;
		_tabActiveStyle = tabActiveStyle;
	}

	public virtual void Draw()
	{
		// Initialize the custom styles for the dropdown
		GUILayout.Space(30);
		GUILayout.BeginHorizontal();
		
		// Left-hand navigation menu for sub-tabs
		GUILayout.BeginVertical(GUILayout.Width(150));
		GUILayout.Space(20);
		DrawTabButtons();
		GUILayout.EndVertical();

		// Space between menu and subtab pages
		GUILayout.Space(40);

		// Right-hand content area for selected sub-tab
		GUILayout.BeginVertical();
		DrawSelectedTabContent();
		GUILayout.EndVertical();
		
		GUILayout.EndHorizontal();
	}

	protected void DrawSelectedTabContent()
	{
		Tabs[_selectedTabIndex].Draw();
	}

	protected void DrawTabButtons()
	{
		int tabCount = Tabs.Count;
		for (var i = 0; i < tabCount; i++)
		{
			GUIStyle currentStyle = _tabStyle;
			if (_selectedTabIndex == i)
			{
				currentStyle = _tabActiveStyle; 
			}

			if (GUILayout.Button(Tabs[i].Name, currentStyle))
			{
				_selectedTabIndex = i;
			}
		}
	}
}