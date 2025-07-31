using Donuts.Models;
using Donuts.PluginGUI.Pages;
using System;
using System.Reflection;
using UnityEngine;

namespace Donuts.PluginGUI;

public class PluginGUIComponent : MonoBehaviour
{
	private bool _currentGuiState = DefaultPluginVars.ShowGUI;
	private ISettingsPage _settingsPage;
	private static bool s_stylesInitialized;

	// InitializeStyles static properties
	internal static GUIStyle WindowStyle { get; private set; }
	internal static GUIStyle LabelStyle { get; private set; }
	internal static GUIStyle ButtonStyle { get; private set; }
	internal static GUIStyle ButtonActiveStyle { get; private set; }
	internal static GUIStyle CloseButtonStyle { get; private set; }
	internal static GUIStyle SubTabButtonStyle { get; private set; }
	internal static GUIStyle SubTabButtonActiveStyle { get; private set; }
	internal static GUIStyle TextFieldStyle { get; private set; }
	internal static GUIStyle HorizontalSliderStyle { get; private set; }
	internal static GUIStyle HorizontalSliderThumbStyle { get; private set; }
	internal static GUIStyle ToggleButtonStyle { get; private set; }
	internal static GUIStyle TooltipStyle { get; private set; }

	private bool GuiEnabled
	{
		get
		{
			if (_currentGuiState != DefaultPluginVars.ShowGUI)
			{
				_currentGuiState = DefaultPluginVars.ShowGUI;
				if (_currentGuiState)
				{
					OnOpen?.Invoke();
				}
				else
				{
					OnClose?.Invoke();
				}
			}
			return _currentGuiState;
		}
	}

	internal static event Action OnOpen;
	internal static event Action OnClose;
	internal static event Action OnResetToDefaults;

	private void OnGUI()
	{
		if (!s_stylesInitialized)
		{
			InitializeStyles();
			_settingsPage = new DonutsSettingsPage();
			s_stylesInitialized = true;
		}
		
		if (!GuiEnabled)
		{
			return;
		}

		// Save the current GUI skin
		GUISkin originalSkin = GUI.skin;

		DefaultPluginVars.WindowRect = GUI.Window(123, DefaultPluginVars.WindowRect, MainWindowFunc, "", WindowStyle);
		GUI.FocusWindow(123);

		if (Event.current.isMouse)
		{
			Event.current.Use();
		}

		// Restore the original GUI skin
		GUI.skin = originalSkin;
	}
		
	private void Update()
	{
		// If showing the gui, disable mouse clicks affecting the game
		if (!GuiEnabled) return;
			
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		if (Input.anyKey)
		{
			Input.ResetInputAxes();
		}
	}

	// Shouldn't need to worry about this script being destroyed since it has the same lifetime as the Application
	// But just in case, we set the static event to null to avoid memory leaks
	private void OnDestroy()
	{
		OnResetToDefaults = null;
	}

	public static void InitializeStyles()
	{
		Texture2D windowBackgroundTex = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 1f));
		Texture2D buttonNormalTex = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f));
		Texture2D buttonHoverTex = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 1f));
		Texture2D buttonActiveTex = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f, 1f));
		Texture2D closeButtonTex = MakeTex(1, 1, new Color(0.5f, 0.0f, 0.0f));
		Texture2D subTabNormalTex = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 1f));
		Texture2D subTabHoverTex = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.5f, 1f));
		Texture2D subTabActiveTex = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.7f, 1f));
		Texture2D toggleEnabledTex = MakeTex(1, 1, Color.red);
		Texture2D toggleDisabledTex = MakeTex(1, 1, Color.gray);
		Texture2D tooltipStyleBackgroundTex = MakeTex(1, 1, new Color(0.0f, 0.5f, 1.0f));

		WindowStyle = new GUIStyle(GUI.skin.window)
		{
			normal = { background = windowBackgroundTex, textColor = Color.white },
			focused = { background = windowBackgroundTex, textColor = Color.white },
			active = { background = windowBackgroundTex, textColor = Color.white },
			hover = { background = windowBackgroundTex, textColor = Color.white },
			onNormal = { background = windowBackgroundTex, textColor = Color.white },
			onFocused = { background = windowBackgroundTex, textColor = Color.white },
			onActive = { background = windowBackgroundTex, textColor = Color.white },
			onHover = { background = windowBackgroundTex, textColor = Color.white },
		};

		LabelStyle = new GUIStyle(GUI.skin.label)
		{
			normal = { textColor = Color.white },
			fontSize = 20,
			fontStyle = FontStyle.Bold,
		};

		ButtonStyle = new GUIStyle(GUI.skin.button)
		{
			normal = { background = buttonNormalTex, textColor = Color.white },
			hover = { background = buttonHoverTex, textColor = Color.white },
			active = { background = buttonActiveTex, textColor = Color.white },
			fontSize = 22,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
		};

		ButtonActiveStyle = new GUIStyle(ButtonStyle)
		{
			normal = { background = buttonActiveTex, textColor = Color.yellow },
			hover = { background = buttonHoverTex, textColor = Color.yellow },
			active = { background = buttonActiveTex, textColor = Color.yellow },
		};

		CloseButtonStyle = new GUIStyle(ButtonStyle)
		{
			normal = { background = closeButtonTex, textColor = Color.white },
			fontSize = 20,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter
		};

		SubTabButtonStyle = new GUIStyle(ButtonStyle)
		{
			normal = { background = subTabNormalTex, textColor = Color.white },
			hover = { background = subTabHoverTex, textColor = Color.white },
			active = { background = subTabActiveTex, textColor = Color.white },
		};

		SubTabButtonActiveStyle = new GUIStyle(SubTabButtonStyle)
		{
			normal = { background = subTabActiveTex, textColor = Color.yellow },
			hover = { background = subTabHoverTex, textColor = Color.yellow },
			active = { background = subTabActiveTex, textColor = Color.yellow },
		};

		TextFieldStyle = new GUIStyle(GUI.skin.textField)
		{
			fontSize = 18,
			normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f)) }
		};

		HorizontalSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
		HorizontalSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
		{
			normal = { background = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.7f, 1f)) }
		};

		ToggleButtonStyle = new GUIStyle(GUI.skin.toggle)
		{
			normal = { background = toggleDisabledTex, textColor = Color.white },
			onNormal = { background = toggleEnabledTex, textColor = Color.white },
			hover = { background = toggleDisabledTex, textColor = Color.white },
			onHover = { background = toggleEnabledTex, textColor = Color.white },
			active = { background = toggleDisabledTex, textColor = Color.white },
			onActive = { background = toggleEnabledTex, textColor = Color.white },
			focused = { background = toggleDisabledTex, textColor = Color.white },
			onFocused = { background = toggleEnabledTex, textColor = Color.white },
			fontSize = 22,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(10, 10, 10, 10),
			margin = new RectOffset(0, 0, 0, 0)
		};

		TooltipStyle = new GUIStyle(GUI.skin.box)
		{
			fontSize = 18,
			wordWrap = true,
			normal = { background = tooltipStyleBackgroundTex, textColor = Color.white },
			fontStyle = FontStyle.Bold
		};
	}

	internal static Texture2D MakeTex(int width, int height, Color col)
	{
		var pix = new Color[width * height];
		for (int i = 0, len = pix.Length; i < len; i++)
		{
			pix[i] = col;
		}
		var result = new Texture2D(width, height);
		result.SetPixels(pix);
		result.Apply();
		return result;
	}

	internal static void RestartPluginGUI()
	{
		if (DonutsPlugin.s_pluginGUIComponent == null) return;
			
		DonutsPlugin.s_pluginGUIComponent.enabled = false;
		DonutsPlugin.s_pluginGUIComponent.enabled = true;
	}

	internal static void ResetSettingsToDefaults()
	{
		foreach (FieldInfo field in DonutsConfiguration.GetSettingFields())
		{
			Type fieldType = field.FieldType;
			if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(Setting<>))
				continue;

			object settingValue = field.GetValue(null);
			Type settingValueType = settingValue.GetType();
			PropertyInfo valueProperty = settingValueType.GetProperty("Value");
			PropertyInfo defaultValueProperty = settingValueType.GetProperty("DefaultValue");
			object defaultValue = defaultValueProperty?.GetValue(settingValue);
			valueProperty?.SetValue(settingValue, defaultValue);
		}
			
		OnResetToDefaults?.Invoke();

		// Reset dropdown indices
		//MainSettingsPage.InitializeDropdownIndices();

		// Reset dropdown indices for spawn point maker settings
		//SpawnPointMakerSettingsPage.InitializeDropdownIndices();
	}

	private void MainWindowFunc(int windowID)
	{
		_settingsPage?.Draw();
	}
}