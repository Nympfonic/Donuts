using BepInEx.Logging;
using JetBrains.Annotations;

namespace Donuts.Utils.Logger;

public class DonutsLogger
{
	[NotNull] private readonly ManualLogSource _logSource = DonutsPlugin.Logger;
	[CanBeNull] private LoggerProcessorBase _processor;

	private LogLevel _logLevel = LogLevel.Info;
	
	public void SetProcessor([CanBeNull] LoggerProcessorBase processor)
	{
		_processor = processor;
	}
	
	public void SetLoggingLevel(LogLevel level)
	{
		_logLevel = level;
	}
	
	public void Log(LoggerMessage data)
	{
		switch (_logLevel)
		{
			case LogLevel.Info:
				_logSource.LogInfo(data.message);
				break;
			case LogLevel.Debug:
				_logSource.LogDebug(data.message);
				break;
			case LogLevel.Warning:
				_logSource.LogWarning(data.message);
				break;
			case LogLevel.Error:
				_logSource.LogError(data.message);
				break;
		}
		_processor?.Process(data);
	}
}