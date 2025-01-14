using BepInEx.Logging;
using EFT.Communications;
using UnityEngine;

namespace Donuts.Utils.LoggerProcessor;

public class NotificationLoggerData(
	string message,
	ManualLogSource logSource = null,
	LogLevel logLevel = LogLevel.Info,
	Color? color = null,
	ENotificationIconType iconType = ENotificationIconType.Default,
	ENotificationDurationType durationType = ENotificationDurationType.Long) : LoggerData(message, logSource, logLevel)
{
	public readonly Color color = color ?? Color.white;
	public readonly ENotificationIconType iconType = iconType;
	public readonly ENotificationDurationType durationType = durationType;
}