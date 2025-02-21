using BepInEx.Logging;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Utils.Logger;
using EFT.Communications;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using LogLevel = Donuts.Utils.Logger.LogLevel;
using Random = System.Random;

namespace Donuts.Utils;

internal static class DonutsHelper
{
	private static readonly Random _random;
	private static readonly DonutsLogger _modNotificationLogger;
	private static readonly DonutsLogger _fullLogger;
	
	static DonutsHelper()
	{
		_random = new Random(unchecked((int)EFTDateTimeClass.Now.Ticks));
		
		var loggerBuilder = new DonutsLoggerBuilder();
		_modNotificationLogger = loggerBuilder.SetLoggingLevel(LogLevel.Info)
			.AddNotificationToast(Color.cyan, ENotificationIconType.Alert)
			.Build();
		
		_fullLogger = loggerBuilder.SetLoggingLevel(LogLevel.Warning)
			.AddNotificationToast(Color.yellow, ENotificationIconType.Alert)
			.AddConsoleLogging()
			.Build();
	}
	
	/// <summary>
	/// Custom implementation of ReadAllTextAsync since it isn't available on .NET Framework 4.7.1
	/// </summary>
	/// <param name="path">A relative or absolute path for the file to be read.</param>
	/// <returns>A <c>UniTask</c> that represents the read file's entire contents as a single string.</returns>
	/// <exception cref="ArgumentException"><c>path</c> is null or empty.</exception>
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
	
	/// <summary>
	/// <see cref="ManualLogSource.LogDebug"/> but also provides current time, executing type name and method name.
	/// </summary>
	/// <param name="logSource">The log source.</param>
	/// <param name="message">The message to output to the log.</param>
	/// <param name="typeName">Name of the executing type.</param>
	/// <param name="methodName">Name of the executing method.</param>
	internal static void LogDebugDetailed(
		[NotNull] this ManualLogSource logSource,
		[NotNull] string message,
		[NotNull] string typeName,
		[NotNull] string methodName)
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("{0} [{1}::{2}] {3}", DateTime.Now.ToLongTimeString(), typeName, methodName, message);
			logSource.LogWarning(sb.ToString());
		}
	}
	
	/// <summary>
	/// <see cref="ManualLogSource.LogError"/> but also provides current time, executing type name and method name.
	/// </summary>
	/// <param name="logSource">The log source.</param>
	/// <param name="message">The message to output to the log.</param>
	/// <param name="typeName">Name of the executing type.</param>
	/// <param name="methodName">Name of the executing method.</param>
	internal static void LogErrorDetailed(
		[NotNull] this ManualLogSource logSource,
		[NotNull] string message,
		[NotNull] string typeName,
		[NotNull] string methodName)
	{
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		sb.AppendFormat("{0} [{1}::{2}] {3}", DateTime.Now.ToLongTimeString(), typeName, methodName, message);
		logSource.LogError(sb.ToString());
	}
	
	/// <summary>
	/// Logs the given exception while also providing current time, executing type name and method name.
	/// </summary>
	/// <param name="logSource"></param>
	/// <param name="typeName">Name of the executing type.</param>
	/// <param name="methodName">Name of the executing method.</param>
	/// <param name="ex">Exception to output to the log.</param>
	internal static void LogException(
		[NotNull] this ManualLogSource logSource,
		[NotNull] string typeName,
		[NotNull] string methodName,
		[NotNull] Exception ex)
	{
		var msg = $"{DateTime.Now.ToLongTimeString()} [{typeName}::{methodName}] {ex.Message}\n{ex.StackTrace}";
		logSource.LogError(msg);
	}
	
	/// <summary>
	/// Output error message to BepInEx client log, to the EFT console and notify the player in-game.
	/// </summary>
	/// <param name="message">Text to be output.</param>
	internal static void NotifyLogError([NotNull] string message)
	{
		_fullLogger.Log(new LoggerMessage(message));
	}
	
	/// <summary>
	/// Outputs a warning message to BepInEx client log and notify the player in-game. Used for Donuts' F9 Config GUI.
	/// </summary>
	/// <inheritdoc cref="NotifyLogError"/>
	internal static void NotifyModSettingsStatus([NotNull] string message)
	{
		_modNotificationLogger.Log(new LoggerMessage(message, LogLevel.Info));
	}
	
	/// <summary>
	/// Displays an in-game notification to the player
	/// </summary>
	/// <param name="message">Text to be output.</param>
	/// <param name="color">Text color.</param>
	/// <param name="iconType">Notification icon type, shown on the left of the text.</param>
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
			DonutsPlugin.Logger.LogException(nameof(DonutsHelper), nameof(DisplayNotification), ex);
		}
	}
	
	/// <summary>
	/// Shuffles elements in the specified array.
	/// </summary>
	/// <param name="source">The array to shuffle.</param>
	/// <param name="createNewArray">Whether a new array should be created to perform the shuffle on.</param>
	/// <typeparam name="T">The type of array.</typeparam>
	/// <returns>An array with shuffled elements.</returns>
	[NotNull]
	internal static T[] ShuffleElements<T>(this T[] source, bool createNewArray = false)
	{
		int arrayLength = source.Length;
		if (arrayLength <= 1)
		{
			return source;
		}
		
		T[] array;
		if (createNewArray)
		{
			array = new T[arrayLength];
			Array.Copy(source, array, arrayLength);
		}
		else
		{
			array = source;
		}
		
		while (arrayLength > 1)
		{
			arrayLength--;
			int k = _random.Next(arrayLength + 1);
			(array[k], array[arrayLength]) = (array[arrayLength], array[k]);
		}
		
		return array;
	}
	
	/// <summary>
	/// Creates a list from the specified collection and performs the shuffle on the new list.
	/// </summary>
	/// <param name="source">The collection to shuffle.</param>
	/// <typeparam name="T">The type the collection stores.</typeparam>
	/// <returns>A new list with shuffled elements.</returns>
	[NotNull]
	internal static List<T> ShuffleElements<T>([NotNull] this IEnumerable<T> source) => source.ToList().ShuffleElements();
	
	/// <summary>
	/// Shuffles elements in the specified list.
	/// </summary>
	/// <param name="source">The list to shuffle.</param>
	/// <param name="createNewList">Whether or not a new list should be created to perform the shuffle on.</param>
	/// <typeparam name="T">The type of list.</typeparam>
	/// <returns>A list with shuffled elements.</returns>
	[NotNull]
	internal static List<T> ShuffleElements<T>([NotNull] this List<T> source, bool createNewList = false)
	{
		int count = source.Count;
		if (count <= 1)
		{
			return source;
		}
		
		List<T> list = createNewList ? source.ToList() : source;
		
		while (count > 1)
		{
			count--;
			int k = _random.Next(count + 1);
			(list[k], list[count]) = (list[count], list[k]);
		}
		
		return list;
	}
	
	/// <summary>
	/// Gets a random element from the collection.
	/// </summary>
	/// <param name="source">The collection to operate on.</param>
	/// <typeparam name="T">The type the collection stores.</typeparam>
	/// <returns>A random element from the collection or null if the collection is empty.</returns>
	/// <remarks>This will create a new list to perform <see cref="PickRandomElement{T}(System.Collections.Generic.IReadOnlyList{T})"/> on.</remarks>
	[CanBeNull]
	internal static T PickRandomElement<T>([NotNull] this IEnumerable<T> source) => source.ToList().PickRandomElement();
	
	/// <summary>
	/// Gets a random element from the list.
	/// </summary>
	/// <param name="source">The list to operate on.</param>
	/// <typeparam name="T">The type the list stores.</typeparam>
	/// <returns>A random element from the list or null if the list is empty.</returns>
	[CanBeNull]
	internal static T PickRandomElement<T>([NotNull] this IReadOnlyList<T> source) => source.PickRandomElement(out _);
	
	[CanBeNull]
	internal static T PickRandomElement<T>([NotNull] this IReadOnlyList<T> source, out int index)
	{
		int count = source.Count;
		
		if (count == 0)
		{
			index = -1;
			return default;
		}
		
		if (count == 1)
		{
			index = 0;
			return source[0];
		}
		
		int randomIndex = _random.Next(count);
		index = randomIndex;
		return source[randomIndex];
	}
	
	[NotNull]
	internal static Random GetRandom() => _random;
}