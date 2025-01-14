using Cysharp.Text;
using System;

namespace Donuts.Utils.LoggerProcessor;

public class NotificationLoggerProcessor : LoggerProcessorBase
{
	public override void Process(LoggerData data)
	{
		if (data is NotificationLoggerData notificationData)
		{
			try
			{
				NotificationManagerClass.DisplayMessageNotification(notificationData.message,
					notificationData.durationType, notificationData.iconType, notificationData.color);
			}
			catch (Exception ex)
			{
				data.logSource.LogException(nameof(NotificationLoggerProcessor), nameof(Process), ex);
			}
		}
		else
		{
			using var sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Using {0} but not passing in data of type {1}! Skipping processor!",
				nameof(NotificationLoggerProcessor), nameof(NotificationLoggerData));
			data.logSource.LogError(sb.ToString());
		}
		
		base.Process(data);
	}
}