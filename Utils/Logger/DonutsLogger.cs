using BepInEx.Logging;
using JetBrains.Annotations;

namespace Donuts.Utils.Logger;

public class DonutsLogger
{
	[NotNull] private readonly ManualLogSource _logSource = DonutsPlugin.Logger;
	[CanBeNull] private LoggerProcessorBase _processor;
	
	public void SetProcessor([CanBeNull] LoggerProcessorBase processor)
	{
		_processor = processor;
	}
	
	public void Log(string message, LogLevel logLevel = LogLevel.Info)
	{
		switch (logLevel)
		{
			case LogLevel.Info:
				_logSource.LogInfo(message);
				break;
			case LogLevel.Debug:
				_logSource.LogDebug(message);
				break;
			case LogLevel.Warning:
				_logSource.LogWarning(message);
				break;
			case LogLevel.Error:
				_logSource.LogError(message);
				break;
		}
		
		_processor?.Process(new LoggerMessage(message, logLevel));
	}
}