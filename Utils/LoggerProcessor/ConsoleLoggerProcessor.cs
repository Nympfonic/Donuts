using EFT.UI;

namespace Donuts.Utils.LoggerProcessor;

public class ConsoleLoggerProcessor : LoggerProcessorBase
{
	public override bool Process(LoggerData data)
	{
		switch (data.logLevel)
		{
			case LogLevel.Info:
			case LogLevel.Debug:
				ConsoleScreen.Log(data.message);
				break;
			case LogLevel.Warning:
				ConsoleScreen.LogWarning(data.message);
				break;
			case LogLevel.Error:
				ConsoleScreen.LogError(data.message);
				break;
		}
		
		return base.Process(data);
	}
}