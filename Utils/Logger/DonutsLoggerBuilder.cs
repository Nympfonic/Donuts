using EFT.Communications;
using UnityEngine;
using UnityToolkit.Structures;

namespace Donuts.Utils.Logger;

public class DonutsLoggerBuilder
{
	private DonutsLogger _logger;
	private LoggerProcessorBase _processor;
	private ProcessorBase<LoggerMessage> _endOfChain;
	
	private LogLevel _logLevel = LogLevel.Info;
	
	public DonutsLoggerBuilder()
	{
		Reset();
	}
	
	public void Reset()
	{
		_logger = new DonutsLogger();
		_processor = null;
		_endOfChain = null;
	}
	
	public DonutsLoggerBuilder SetLoggingLevel(LogLevel level)
	{
		_logLevel = level;
		return this;
	}
	
	public DonutsLoggerBuilder AddConsoleLogging()
	{
		if (_processor == null)
		{
			_processor = new ConsoleLoggerProcessor();
			_endOfChain = _processor;
			return this;
		}
		
		_endOfChain = _endOfChain.SetNext(new ConsoleLoggerProcessor());
		return this;
	}
	
	public DonutsLoggerBuilder AddNotificationToast(
		Color? textColor = null,
		ENotificationIconType iconType = ENotificationIconType.Default,
		ENotificationDurationType durationType = ENotificationDurationType.Long)
	{
		if (_processor == null)
		{
			_processor = new NotificationLoggerProcessor(textColor, iconType, durationType);
			_endOfChain = _processor;
			return this;
		}
		
		_endOfChain = _endOfChain.SetNext(new NotificationLoggerProcessor(textColor, iconType, durationType));
		return this;
	}
	
	public DonutsLogger Build()
	{
		_logger.SetLoggingLevel(_logLevel);
		_logger.SetProcessor(_processor);
		DonutsLogger logger = _logger;
		Reset();
		return logger;
	}
}