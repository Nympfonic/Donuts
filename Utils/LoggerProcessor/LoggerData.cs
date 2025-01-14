using BepInEx.Logging;

namespace Donuts.Utils.LoggerProcessor;

public class LoggerData(string message, ManualLogSource logSource = null, LogLevel logLevel = LogLevel.Info)
{
	public readonly string message = message;
	public readonly ManualLogSource logSource = logSource ?? DonutsPlugin.Logger;
	public readonly LogLevel logLevel = logLevel;
}