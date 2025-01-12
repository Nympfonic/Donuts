using Donuts.Models;
using Donuts.Utils;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Donuts;

internal static class DonutsConfiguration
{
	private static readonly List<FieldInfo> _settingFields = AccessTools.GetDeclaredFields(typeof(DefaultPluginVars));

	internal static List<FieldInfo> GetSettingFields() => _settingFields;

	internal static void ImportConfig(string directoryPath)
	{
		// Get the path of the currently executing assembly
		string configFilePath = Path.Combine(directoryPath, "Config", "DefaultPluginVars.json");

		if (!File.Exists(configFilePath))
		{
			DonutsPlugin.Logger.LogError($"Config file not found: {configFilePath}, creating a new one...");
			ExportConfig();
			return;
		}

		string json = File.ReadAllText(configFilePath);
		ImportConfigFromJson(json);
	}

	internal static void ExportConfig()
	{
		string configDirectory = Path.Combine(DonutsPlugin.directoryPath, "Config");
		if (!Directory.Exists(configDirectory))
		{
			Directory.CreateDirectory(configDirectory);
		}

		string configFilePath = Path.Combine(configDirectory, "DefaultPluginVars.json");
		//DefaultPluginVars.WindowRect = windowRect;
		string json = ExportToJson();
		File.WriteAllText(configFilePath, json);
	}

	private static string ExportToJson()
	{
		var settingsDictionary = new Dictionary<string, object>();

		foreach (FieldInfo field in _settingFields)
		{
			Type fieldType = field.FieldType;
			if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(Setting<>))
			{
				continue;
			}

			AddSettingToDictionary(settingsDictionary, field);
		}

		// Add windowRect position and size to the dictionary
		settingsDictionary["windowRectX"] = DefaultPluginVars.WindowRect.x;
		settingsDictionary["windowRectY"] = DefaultPluginVars.WindowRect.y;
		settingsDictionary["windowRectWidth"] = DefaultPluginVars.WindowRect.width;
		settingsDictionary["windowRectHeight"] = DefaultPluginVars.WindowRect.height;

		return JsonConvert.SerializeObject(settingsDictionary, Formatting.Indented);
	}

	private static void ImportConfigFromJson(string json)
	{
		var settingsDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

		foreach (FieldInfo field in _settingFields)
		{
			if (!field.FieldType.IsGenericType ||
				field.FieldType.GetGenericTypeDefinition() != typeof(Setting<>) ||
				!settingsDictionary.TryGetValue(field.Name, out object value))
			{
				continue;
			}

			// Store the scenario selection values to later initialize
			if (field.Name == nameof(DefaultPluginVars.pmcScenarioSelection))
			{
				DefaultPluginVars.PmcScenarioSelectionValue = value.ToString();
#if DEBUG
				DonutsPlugin.Logger.LogDebug($"Setting {nameof(DefaultPluginVars.PmcScenarioSelectionValue)} to {value}");	
#endif
				continue;
			}

			if (field.Name == nameof(DefaultPluginVars.scavScenarioSelection))
			{
				DefaultPluginVars.ScavScenarioSelectionValue = value.ToString();
#if DEBUG
				DonutsPlugin.Logger.LogDebug($"Setting {nameof(DefaultPluginVars.ScavScenarioSelectionValue)} to {value}");	
#endif
				continue;
			}

			ApplySetting(field, value);
		}

		// Load windowRect position and size from the dictionary, with defaults if not present
		if (settingsDictionary.TryGetValue("windowRectX", out object windowRectX) &&
		    settingsDictionary.TryGetValue("windowRectY", out object windowRectY) &&
		    settingsDictionary.TryGetValue("windowRectWidth", out object windowRectWidth) &&
		    settingsDictionary.TryGetValue("windowRectHeight", out object windowRectHeight))
		{
			DefaultPluginVars.WindowRect = new Rect(
				Convert.ToSingle(windowRectX),
				Convert.ToSingle(windowRectY),
				Convert.ToSingle(windowRectWidth),
				Convert.ToSingle(windowRectHeight)
			);
		}
		else
		{
			// Apply default values if any of the windowRect values are missing
			DefaultPluginVars.WindowRect = new Rect(20, 20, 1664, 936);
		}

		// Ensure the arrays are initialized before creating the settings
		DefaultPluginVars.pmcScenarioCombinedArray ??= [];
		DefaultPluginVars.scavScenarioCombinedArray ??= [];
	}

	private static void AddSettingToDictionary(IDictionary<string, object> settingsDictionary, FieldInfo field)
	{
		object settingValue = field.GetValue(null);
		if (settingValue == null)
		{
			return;
		}

		PropertyInfo valueProperty = settingValue.GetType().GetProperty("Value");
		if (valueProperty == null)
		{
			return;
		}

		object value = valueProperty.GetValue(settingValue);
		settingsDictionary[field.Name] = value;
	}

	private static void ApplySetting(FieldInfo settingField, object value)
	{
		object settingValue = settingField.GetValue(null);
		if (settingValue == null)
		{
			DonutsPlugin.Logger.LogError($"Setting value for field {settingField.Name} is null.");
			return;
		}

		PropertyInfo valueProperty = settingValue.GetType().GetProperty("Value");
		if (valueProperty == null)
		{
			DonutsPlugin.Logger.LogError($"Value property for setting {settingField.Name} is not found.");
			return;
		}

		try
		{
			Type fieldType = settingField.FieldType.GetGenericArguments()[0];
			if (fieldType == typeof(KeyCode))
			{
				valueProperty.SetValue(settingValue, Enum.Parse(typeof(KeyCode), value.ToString()));
			}
			else
			{
				object convertedValue = Convert.ChangeType(value, fieldType);
				valueProperty.SetValue(settingValue, convertedValue);
			}
		}
		catch (Exception ex)
		{
			DonutsPlugin.Logger.LogException(nameof(DonutsConfiguration), nameof(ApplySetting), ex);
		}
	}
}