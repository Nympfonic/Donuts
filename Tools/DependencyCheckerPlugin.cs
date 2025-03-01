using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.InputSystem;
using EFT.UI;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Donuts.Tools;

[BepInPlugin("com.dvize.DonutsDependencyChecker", "Donuts Dependency Checker", "1.0.0")]
[BepInDependency("xyz.drakia.waypoints", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.Arys.UnityToolkit", BepInDependency.DependencyFlags.SoftDependency)]
public class DependencyCheckerPlugin : BaseUnityPlugin
{
	private const float ERROR_WAITING_TIME = 60f;
	
	private readonly DependencyInfo[] _hardDependencies =
	[
		new("xyz.drakia.waypoints", "Drakia's Waypoints", new Version("0.0.0")),
		new("com.Arys.UnityToolkit", "UnityToolkit", new Version("1.2.0")),
	];
	
	private void Awake()
	{
		if (!ValidateDependencies(Logger, _hardDependencies, Config, out List<string> missingDependencies))
		{
			StartCoroutine(ShowDependencyErrors(missingDependencies));
			throw new Exception("Missing Donuts Dependencies");
		}
	}

	/// <summary>
	/// Check that all the BepInDependency entries for the given pluginType are available and instantiated. This allows a
	/// plugin to validate that its dependent plugins weren't disabled post-dependency check (Such as for the wrong EFT version)
	/// </summary>
	/// <param name="logger"></param>
	/// <param name="hardDependencies"></param>
	/// <param name="config"></param>
	/// <param name="missingDependencies"></param>
	/// <returns></returns>
	private static bool ValidateDependencies(
		[NotNull] ManualLogSource logger,
		[NotNull] DependencyInfo[] hardDependencies,
		[CanBeNull] ConfigFile config,
		[NotNull] out List<string> missingDependencies)
	{
		var noVersion = new Version("0.0.0");
		missingDependencies = new List<string>(hardDependencies.Length);
		
		if (hardDependencies.Length == 0)
		{
			return true;
		}
		
		var validationSuccess = true;
		
		foreach (DependencyInfo dependency in hardDependencies)
		{
			string dependencyVersion = dependency.version > noVersion
				? $" v{dependency.version}"
				: " Any version";
			
			if (!Chainloader.PluginInfos.TryGetValue(dependency.guid, out PluginInfo dependencyInfo) ||
				dependencyInfo == null ||
				dependencyInfo.Instance == null ||
				!dependencyInfo.Instance.enabled)
			{
				var notInstalledLogMessage = $"ERROR: {dependency.name} ({dependency.guid}) is not installed!";
				logger.LogError(notInstalledLogMessage);
				Chainloader.DependencyErrors.Add(notInstalledLogMessage);
				var notInstalledMessage =
					$"- {dependency.name} -- Required:{dependencyVersion}, Current: Not installed/Failed to load";
				missingDependencies.Add(notInstalledMessage);
				validationSuccess = false;
				continue;
			}
			
			if (dependencyInfo.Metadata.Version >= dependency.version)
			{
				continue;
			}
			
			var outdatedLogMessage =
				$"ERROR: Outdated version of {dependencyInfo.Metadata.Name} Required:{dependencyVersion}, Current: v{dependencyInfo.Metadata.Version}";
			logger.LogError(outdatedLogMessage);
			Chainloader.DependencyErrors.Add(outdatedLogMessage);
			var outdatedMessage =
				$"- {dependency.name} -- Required:{dependencyVersion}, Current: v{dependencyInfo.Metadata.Version}";
			missingDependencies.Add(outdatedMessage);
			validationSuccess = false;
		}
		
		if (!validationSuccess)
		{
			// This results in a bogus config entry in the BepInEx config file for the plugin, but it shouldn't hurt anything
			// We leave the "section" parameter empty so there's no section header drawn
			config?.Bind("", "MissingDeps", "", new ConfigDescription(
				"", null, new ConfigurationManagerAttributes
				{
					CustomDrawer = ErrorLabelDrawer,
					ReadOnly = true,
					HideDefaultButton = true,
					HideSettingName = true,
					Category = null
				}
			));
		}
		
		return validationSuccess;
	}
	
	private static void ErrorLabelDrawer(ConfigEntryBase entry)
	{
		var styleNormal = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			stretchWidth = true
		};

		var styleError = new GUIStyle(GUI.skin.label)
		{
			stretchWidth = true,
			alignment = TextAnchor.MiddleCenter,
			normal =
			{
				textColor = Color.red
			},
			fontStyle = FontStyle.Bold
		};

		// General notice that we're the wrong version
		GUILayout.BeginVertical();
		GUILayout.Label(entry.Description.Description, styleNormal, GUILayout.ExpandWidth(true));

		// Centered red disabled text
		GUILayout.Label("Plugin has been disabled!", styleError, GUILayout.ExpandWidth(true));
		GUILayout.EndVertical();
	}
	
	private static IEnumerator ShowDependencyErrors([NotNull] IReadOnlyList<string> missingDependencies)
	{
		var waitUntilPreloaderUIReady = new WaitUntil(() =>
			Singleton<PreloaderUI>.Instantiated && Singleton<PreloaderUI>.Instance.CanShowErrorScreen);
		yield return waitUntilPreloaderUIReady;
		
		const string title = "Donuts";
		Singleton<PreloaderUI>.Instance.ShowCriticalErrorScreen(
			header: title, message: string.Empty,
			buttonType: ErrorScreen.EButtonType.QuitButton,
			waitingTime: ERROR_WAITING_TIME,
			acceptCallback: Application.Quit,
			endTimeCallback: Application.Quit);
		
		var errorScreenList = Traverse.Create(Singleton<PreloaderUI>.Instance)
			.Field("_criticalErrorScreenContainer")
			.Field("_children")
			.GetValue<List<InputNode>>();
		
		var sb = new StringBuilder(100);
		sb.AppendLine("Donuts is missing the following dependencies:\n");
		
		foreach (string dependency in missingDependencies)
		{
			sb.AppendLine(dependency);
		}

		ErrorScreen errorScreenObj = null;
		foreach (InputNode inputNode in errorScreenList)
		{
			if (inputNode.isActiveAndEnabled &&
				inputNode is ErrorScreen errorScreen &&
				errorScreen.Caption.text == title)
			{
				errorScreenObj = errorScreen;
				RectTransform rect = inputNode.RectTransform();
				rect.sizeDelta = new Vector2(rect.sizeDelta.x + 300, rect.sizeDelta.y + 150);
				rect.SetAsLastSibling();
				
				Traverse.Create(errorScreen)
					.Field("string_1")
					.SetValue(sb.ToString());
				break;
			}
		}
		
		int currentErrorScreenCount = errorScreenList.Count;
		
		// Wait for other errors to show up
		var waitUntilMoreErrorScreens = new WaitUntil(() => errorScreenList.Count > currentErrorScreenCount);
		while (true)
		{
			yield return waitUntilMoreErrorScreens;
			currentErrorScreenCount = errorScreenList.Count;
			
			// Bring to the front
			if (errorScreenObj != null)
			{
				errorScreenObj.transform.SetAsLastSibling();
			}
		}
	}
	
#pragma warning disable 0169, 0414, 0649
	internal sealed class ConfigurationManagerAttributes
	{
		public bool? ShowRangeAsPercent;
		public Action<ConfigEntryBase> CustomDrawer;
		public CustomHotkeyDrawerFunc CustomHotkeyDrawer;
		public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);
		public bool? Browsable;
		public string Category;
		public object DefaultValue;
		public bool? HideDefaultButton;
		public bool? HideSettingName;
		public string Description;
		public string DispName;
		public int? Order;
		public bool? ReadOnly;
		public bool? IsAdvanced;
		public Func<object, string> ObjToStr;
		public Func<string, object> StrToObj;
	}
	
	private sealed class DependencyInfo(string guid, string name, Version version)
	{
		public readonly string guid = guid;
		public readonly string name = name;
		public readonly Version version = version;
	}
}