using EFT.Communications;
using System;
using UnityEngine;

namespace Donuts.Utils.Logger;

public class NotificationLoggerProcessor(
	Color? textColor = null,
	ENotificationIconType iconType = ENotificationIconType.Default,
	ENotificationDurationType durationType = ENotificationDurationType.Long) : LoggerProcessorBase
{
	private readonly Color _textColor = textColor ?? Color.white;
	
	public override bool Process(LoggerMessage data)
	{
		try
		{
			NotificationManagerClass.DisplayMessageNotification(data.message, durationType, iconType, _textColor);
		}
		catch (Exception ex)
		{
			DonutsPlugin.Logger.LogException(nameof(NotificationLoggerProcessor), nameof(Process), ex);
		}
		
		return base.Process(data);
	}
}