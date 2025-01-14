namespace Donuts.Utils.LoggerProcessor;

public class BepInExLoggerProcessor : LoggerProcessorBase
{
	public override void Process(LoggerData data)
	{
		switch (data.logLevel)
		{
			case LogLevel.Info:
				data.logSource.LogInfo(data.message);
				break;
			case LogLevel.Warning:
				data.logSource.LogWarning(data.message);
				break;
			case LogLevel.Error:
				data.logSource.LogError(data.message);
				break;
			case LogLevel.Debug:
				data.logSource.LogDebug(data.message);
				break;
		}
		
		base.Process(data);
	}
}