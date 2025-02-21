using Donuts.Utils;
using UnityEngine;
using static Donuts.DefaultPluginVars;

namespace Donuts.PluginGUI.Pages;

public class DonutsSettingsPage : TabContainerPage
{
	private bool _isDragging;
	private Vector2 _dragOffset;

	private const float RESIZE_HANDLE_SIZE = 30f;
	private bool _isResizing;
	private Vector2 _resizeStartPos;
	
	private Vector2 _scrollPosition = Vector2.zero;
	
	public override string Name => "Donuts Configuration";

	public DonutsSettingsPage() : base(PluginGUIComponent.ButtonStyle, PluginGUIComponent.ButtonActiveStyle)
	{
		Tabs.Add(new MainSettingsPage());
		Tabs.Add(new SpawnSettingsPage());
		Tabs.Add(new AdvancedSettingsPage());
		Tabs.Add(new SpawnPointMakerSettingsPage());
		Tabs.Add(new DebuggingSettingsPage());
	}

	public override void Draw()
	{
		// Manually draw the window title centered at the top
		Rect titleRect = new(0, 0, WindowRect.width, 20);
		GUI.Label(titleRect, Name, new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			fontSize = 20,
			fontStyle = FontStyle.Bold,
			normal = { textColor = Color.white }
		});

		GUILayout.BeginVertical();
		
		_scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
		GUILayout.BeginVertical();
		DrawTabButtons();
		GUILayout.EndVertical();
		DrawSelectedTabContent();
		GUILayout.EndScrollView();
		
		DrawFooter();
		GUILayout.EndVertical();

		HandleWindowDragging();
		HandleWindowResizing();

		GUI.DragWindow(new Rect(0, 0, WindowRect.width, 20));
	}
	
	private static void DrawFooter()
	{
		GUILayout.FlexibleSpace();
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();

		var greenButtonStyle = new GUIStyle(PluginGUIComponent.ButtonStyle)
		{
			normal = { background = PluginGUIComponent.MakeTex(1, 1, new Color(0.0f, 0.5f, 0.0f)), textColor = Color.white },
			fontSize = 20,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter
		};

		if (GUILayout.Button("Save All Changes", greenButtonStyle, GUILayout.Width(250), GUILayout.Height(50)))
		{
			DonutsConfiguration.ExportConfig();
			DonutsHelper.NotifyModSettingsStatus("All Donuts settings have been saved.");
		}

		GUILayout.Space(RESIZE_HANDLE_SIZE);

		GUILayout.EndHorizontal();
	}
	
	private void HandleWindowDragging()
	{
		if (Event.current.type == EventType.MouseDown &&
			new Rect(0, 0, WindowRect.width, 20).Contains(Event.current.mousePosition))
		{
			_isDragging = true;
			_dragOffset = Event.current.mousePosition;
		}

		if (!_isDragging) return;

		if (Event.current.type == EventType.MouseUp)
		{
			_isDragging = false;
		}
		else if (Event.current.type == EventType.MouseDrag)
		{
			Rect rect = WindowRect;
			rect.position += Event.current.mousePosition - _dragOffset;
			WindowRect = rect;
		}
	}

	private void HandleWindowResizing()
	{
		Rect resizeHandleRect = new(
			WindowRect.width - RESIZE_HANDLE_SIZE,
			WindowRect.height - RESIZE_HANDLE_SIZE,
			RESIZE_HANDLE_SIZE,
			RESIZE_HANDLE_SIZE
		);
		GUI.DrawTexture(resizeHandleRect, Texture2D.whiteTexture);

		if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
		{
			_isResizing = true;
			_resizeStartPos = Event.current.mousePosition;
			Event.current.Use();
		}

		if (!_isResizing) return;

		if (Event.current.type == EventType.MouseUp)
		{
			_isResizing = false;
		}
		else if (Event.current.type == EventType.MouseDrag)
		{
			Vector2 delta = Event.current.mousePosition - _resizeStartPos;
			Rect rect = WindowRect;
			rect.width = Mathf.Max(300, WindowRect.width + delta.x);
			rect.height = Mathf.Max(200, WindowRect.height + delta.y);
			WindowRect = rect;
			_resizeStartPos = Event.current.mousePosition;
			Event.current.Use();
		}
		else if (Event.current.type == EventType.MouseMove)
		{
			Event.current.Use();
		}
	}
}