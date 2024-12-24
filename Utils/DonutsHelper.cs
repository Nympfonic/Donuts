using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using EFT.Communications;
using EFT.UI;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Donuts.Utils;

internal static class DonutsHelper
{
	private static readonly Random _random = new(unchecked((int)EFTDateTimeClass.Now.Ticks));
	
	/// <summary>
	/// Custom implementation of ReadAllTextAsync since it isn't available on .NET Framework 4.7.1
	/// </summary>
	/// <param name="path">A relative or absolute path for the file to be read.</param>
	/// <returns>A UniTask that represents the read file's entire contents as a single string.</returns>
	/// <exception cref="ArgumentException">path is null or empty.</exception>
	internal static async UniTask<string> ReadAllTextAsync(string path)
	{
		if (string.IsNullOrEmpty(path))
			throw new ArgumentException("Empty path name is not legal.", nameof(path));

		using var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096,
			useAsync: true);
		using var streamReader = new StreamReader(sourceStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		// detectEncodingFromByteOrderMarks allows you to handle files with BOM correctly,
		// otherwise you may get chinese characters even when your text does not contain any

		return await streamReader.ReadToEndAsync();
	}

	internal static void NotifyLog(
		[NotNull] this ManualLogSource logger,
		[NotNull] string message,
		bool logToConsole = true)
	{
		logger.LogError(message);
		if (logToConsole)
		{
			ConsoleScreen.LogError(message);
		}
	}

	/// <summary>
	/// Output error message to BepInEx client logs, with the option to output to the EFT console and notify the player in-game.
	/// </summary>
	/// <param name="logger">The BepInEx logger to be used.</param>
	/// <param name="message">Text to be output.</param>
	/// <param name="logToConsole">Should the message be logged to the EFT console?</param>
	/// <param name="notifyPlayer">Should the player be notified in-game via the EFT toast notification?</param>
	internal static void NotifyLogError(
		[NotNull] this ManualLogSource logger,
		[NotNull] string message,
		bool logToConsole = true,
		bool notifyPlayer = true)
	{
		logger.NotifyLog(message, logToConsole);
		if (notifyPlayer)
		{
			DisplayNotification(message, Color.yellow, ENotificationIconType.Alert);
		}
	}

	internal static void NotifyModSettingsStatus([NotNull] string message)
	{
		DisplayNotification(message, Color.cyan, ENotificationIconType.Alert);
	}

	internal static void DisplayNotification(
		[NotNull] string message,
		Color color,
		ENotificationIconType iconType = ENotificationIconType.Default)
	{
		try
		{
			NotificationManagerClass.DisplayMessageNotification(message, ENotificationDurationType.Long, iconType, color);
		}
		catch (Exception ex)
		{
			DonutsPlugin.Logger.LogError(string.Format("Exception thrown in {0}::{1}: {2}\n{3}",
				nameof(NotificationManagerClass), nameof(NotificationManagerClass.DisplayMessageNotification),
				ex.Message, ex.StackTrace));
		}
	}

	[NotNull]
	internal static List<T> ShuffleElements<T>([NotNull] this IEnumerable<T> source)
	{
		return source.ToList().ShuffleElements();
	}

	[NotNull]
	internal static List<T> ShuffleElements<T>([NotNull] this List<T> source)
	{
		int n = source.Count;
		while (n > 1)
		{
			n--;
			int k = _random.Next(n + 1);
			(source[k], source[n]) = (source[n], source[k]);
		}
		return source;
	}

	[CanBeNull]
	internal static T PickRandomElement<T>([NotNull] this IEnumerable<T> source)
	{
		return source.ToList().PickRandomElement();
	}

	[CanBeNull]
	internal static T PickRandomElement<T>([NotNull] this IReadOnlyList<T> source)
	{
		int n = source.Count;
		if (n == 0)
			return default;
		int randomIndex = _random.Next(n);
		return source[randomIndex];
	}

	[NotNull]
	internal static Random GetRandom() => _random;
}