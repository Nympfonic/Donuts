using Cysharp.Threading.Tasks;
using Donuts.Models;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.PluginGUI;

public static class ImGUIToolkit
{
	private static readonly Dictionary<int, bool> _dropdownStates = [];
	private static readonly Dictionary<int, bool> _accordionStates = [];
	private static readonly Dictionary<int, KeyCode> _newKeybinds = [];
	private static readonly Dictionary<int, bool> _keybindStates = [];
	private static bool _isSettingKeybind; // Flag to indicate if setting a keybind

	private static bool _stylesInitialized;

	public static int Dropdown<T>(Setting<T> setting, int selectedIndex)
	{
		EnsureStylesInitialized();

		if (setting.OptionsInvalid())
			return selectedIndex;

		if (selectedIndex >= setting.Options.Length)
		{
			selectedIndex = 0;
		}

		int dropdownId = GUIUtility.GetControlID(FocusType.Passive);

		if (!_dropdownStates.ContainsKey(dropdownId))
		{
			_dropdownStates[dropdownId] = false;
		}

		GUILayout.BeginHorizontal();

		GUIContent labelContent = new(setting.Name, setting.ToolTipText);
		GUILayout.Label(labelContent, PluginGUIComponent.LabelStyle, GUILayout.Width(200));

		GUIStyle currentDropdownStyle = _dropdownStates[dropdownId]
			? PluginGUIComponent.SubTabButtonActiveStyle
			: PluginGUIComponent.SubTabButtonStyle;

		GUIContent buttonContent = new(setting.Options[selectedIndex]?.ToString(), setting.ToolTipText);
		if (GUILayout.Button(buttonContent, currentDropdownStyle, GUILayout.Width(300)))
		{
			_dropdownStates[dropdownId] = !_dropdownStates[dropdownId];
		}

		GUILayout.EndHorizontal();

		if (_dropdownStates[dropdownId])
		{
			GUILayout.BeginHorizontal();
			GUILayout.Space(209);

			GUILayout.BeginVertical();

			for (var i = 0; i < setting.Options.Length; i++)
			{
				GUIContent optionContent = new(setting.Options[i]?.ToString(), setting.ToolTipText);
				if (!GUILayout.Button(optionContent, PluginGUIComponent.SubTabButtonStyle, GUILayout.Width(300)))
					continue;
					
				selectedIndex = i;
				setting.Value = setting.Options[i];
				_dropdownStates[dropdownId] = false;
			}

			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		ShowTooltip();

		return selectedIndex;
	}

	public static float Slider(string label, string toolTip, float value, float min, float max)
	{
		EnsureStylesInitialized();

		GUILayout.BeginHorizontal();

		GUIContent labelContent = new(label, toolTip);
		GUILayout.Label(labelContent, PluginGUIComponent.LabelStyle, GUILayout.Width(200));

		value = GUILayout.HorizontalSlider(
			value,
			min,
			max,
			PluginGUIComponent.HorizontalSliderStyle,
			PluginGUIComponent.HorizontalSliderThumbStyle,
			GUILayout.Width(300)
		);

		GUI.SetNextControlName("floatTextField");
		string textFieldValue = GUILayout.TextField(
			value.ToString("F2"),
			PluginGUIComponent.TextFieldStyle,
			GUILayout.Width(100)
		);
		if (float.TryParse(textFieldValue, out float newValue))
		{
			value = Mathf.Clamp(newValue, min, max);
		}

		GUILayout.EndHorizontal();
		ShowTooltip();

		// Check for Enter key press (both Enter keys) to escape focus
		if (Event.current.isKey &&
		    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
		    GUI.GetNameOfFocusedControl() == "floatTextField")
		{
			GUI.FocusControl(null);
		}

		// Check for mouse click outside the control to escape focus
		if (Event.current.type != EventType.MouseDown || GUI.GetNameOfFocusedControl() != "floatTextField")
			return value;
			
		// Check if the mouse click is outside the text field
		Rect textFieldRect = GUILayoutUtility.GetLastRect();
		if (!textFieldRect.Contains(Event.current.mousePosition))
		{
			GUI.FocusControl(null);
		}

		return value;
	}

	public static int Slider(string label, string toolTip, int value, int min, int max)
	{
		EnsureStylesInitialized();

		GUILayout.BeginHorizontal();

		GUIContent labelContent = new(label, toolTip);
		GUILayout.Label(labelContent, PluginGUIComponent.LabelStyle, GUILayout.Width(200));

		value = (int)GUILayout.HorizontalSlider(
			value,
			min,
			max,
			PluginGUIComponent.HorizontalSliderStyle,
			PluginGUIComponent.HorizontalSliderThumbStyle,
			GUILayout.Width(300)
		);

		GUI.SetNextControlName("intTextField");
		string textFieldValue = GUILayout.TextField(value.ToString(), PluginGUIComponent.TextFieldStyle, GUILayout.Width(100));
		if (int.TryParse(textFieldValue, out int newValue))
		{
			value = Mathf.Clamp(newValue, min, max);
		}

		GUILayout.EndHorizontal();
		ShowTooltip();

		// Check for Enter key press (both Enter keys) to escape focus
		if (Event.current.isKey &&
		    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
		    GUI.GetNameOfFocusedControl() == "intTextField")
		{
			GUI.FocusControl(null);
		}

		// Check for mouse click outside the control to escape focus
		if (Event.current.type != EventType.MouseDown || GUI.GetNameOfFocusedControl() != "intTextField")
			return value;
			
		// Check if the mouse click is outside the text field
		Rect textFieldRect = GUILayoutUtility.GetLastRect();
		if (!textFieldRect.Contains(Event.current.mousePosition))
		{
			GUI.FocusControl(null);
		}

		return value;
	}

	public static string TextField(string label, string toolTip, string text, int maxLength = 50)
	{
		// Create GUIContent for the label with tooltip
		GUIContent labelContent = new(label, toolTip);
		GUILayout.BeginHorizontal();

		GUILayout.Label(labelContent, PluginGUIComponent.LabelStyle, GUILayout.Width(200));

		GUI.SetNextControlName("textField");
		string newText = GUILayout.TextField(text, maxLength, PluginGUIComponent.TextFieldStyle, GUILayout.Width(300));

		// Ensure the text does not exceed the maximum length
		if (newText.Length > maxLength)
		{
			newText = newText.Substring(0, maxLength);
		}

		// Check for Enter key press to escape focus
		if (Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "textField")
		{
			GUI.FocusControl(null);
		}

		GUILayout.EndHorizontal();

		return newText;
	}

	public static bool Toggle(string label, string toolTip, bool value)
	{
		EnsureStylesInitialized();

		GUILayout.BeginHorizontal();
		GUIContent labelContent = new(label, toolTip);
		GUILayout.Label(labelContent, PluginGUIComponent.LabelStyle, GUILayout.Width(200));
		GUILayout.Space(10);

		GUIContent toggleContent = new(value ? "YES" : "NO", toolTip);
		bool newValue = GUILayout.Toggle(value, toggleContent, PluginGUIComponent.ToggleButtonStyle,
			GUILayout.Width(150), GUILayout.Height(35));

		GUILayout.EndHorizontal();

		ShowTooltip();

		return newValue;
	}

	public static bool Button(string label, string toolTip, GUIStyle style = null)
	{
		EnsureStylesInitialized();

		style ??= PluginGUIComponent.ButtonStyle;

		GUIContent buttonContent = new(label, toolTip);
		bool result = GUILayout.Button(buttonContent, style, GUILayout.Width(200));

		ShowTooltip();

		return result;
	}

	public static void Accordion(string label, string toolTip, Action drawContents)
	{
		EnsureStylesInitialized();

		int accordionId = GUIUtility.GetControlID(FocusType.Passive);

		if (!_accordionStates.ContainsKey(accordionId))
		{
			_accordionStates[accordionId] = false;
		}

		GUIContent buttonContent = new(label, toolTip);
		if (GUILayout.Button(buttonContent, PluginGUIComponent.ButtonStyle))
		{
			_accordionStates[accordionId] = !_accordionStates[accordionId];
		}

		if (_accordionStates[accordionId])
		{
			GUILayout.BeginVertical(GUI.skin.box);
			drawContents();
			GUILayout.EndVertical();
		}

		ShowTooltip();
	}

	public static KeyCode KeybindField(string label, string toolTip, KeyCode currentKey)
	{
		EnsureStylesInitialized();

		int keybindId = GUIUtility.GetControlID(FocusType.Passive);

		if (!_keybindStates.ContainsKey(keybindId))
		{
			_keybindStates[keybindId] = false;
			_newKeybinds[keybindId] = currentKey;
		}

		GUILayout.BeginHorizontal();
		GUIContent labelContent = new(label, toolTip);
		GUILayout.Label(labelContent, PluginGUIComponent.LabelStyle, GUILayout.Width(200));
		GUILayout.Space(10);

		if (_keybindStates[keybindId])
		{
			GUIContent waitingContent = new("Press any key...", toolTip);
			GUILayout.Button(waitingContent, PluginGUIComponent.ButtonStyle, GUILayout.Width(200));
			_isSettingKeybind = true;
		}
		else
		{
			GUIContent keyContent = new(currentKey.ToString(), toolTip);
			if (GUILayout.Button(keyContent, PluginGUIComponent.ButtonStyle, GUILayout.Width(200)))
			{
				_keybindStates[keybindId] = true;
				_isSettingKeybind = true;
			}
		}

		if (GUILayout.Button("Clear", GUILayout.Width(90)))
		{
			currentKey = KeyCode.None;
		}

		GUILayout.EndHorizontal();

		if (_keybindStates[keybindId])
		{
			Event e = Event.current;
			if (e.isKey)
			{
				_newKeybinds[keybindId] = e.keyCode;
				_keybindStates[keybindId] = false;
				currentKey = e.keyCode;
				// TODO: Verify using UniTask instead of Task works here, otherwise revert
				UniTask.Delay(1000, true).ContinueWith(() => _isSettingKeybind = false);
			}
		}

		ShowTooltip();

		return currentKey;
	}

	public static bool IsSettingKeybind() => _isSettingKeybind;

	private static void ShowTooltip()
	{
		if (string.IsNullOrEmpty(GUI.tooltip)) return;
			
		Vector2 mousePosition = Event.current.mousePosition;
		Vector2 size = PluginGUIComponent.TooltipStyle.CalcSize(new GUIContent(GUI.tooltip));
		size.y = PluginGUIComponent.TooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), size.x);
		Rect tooltipRect = new(mousePosition.x, mousePosition.y - size.y, size.x, size.y);
		GUI.Box(tooltipRect, GUI.tooltip, PluginGUIComponent.TooltipStyle);
	}

	public static int FindSettingIndex<T>(Setting<T> setting)
	{
		if (setting == null)
		{
			DonutsPlugin.Logger.LogError($"{nameof(ImGUIToolkit)}::{nameof(FindSettingIndex)}: {nameof(setting)} is null.");
			return -1;
		}

		for (var i = 0; i < setting.Options.Length; i++)
		{
			if (EqualityComparer<T>.Default.Equals(setting.Options[i], setting.Value))
				return i;
		}
		DonutsPlugin.Logger.LogError($"{nameof(ImGUIToolkit)}::{nameof(FindSettingIndex)}: Value '{setting.Value}' not found in Options for setting '{setting.Name}'");
		return -1;
	}
		
	private static void EnsureStylesInitialized()
	{
		if (_stylesInitialized) return;
			
		PluginGUIComponent.InitializeStyles();
		_stylesInitialized = true;
	}
}