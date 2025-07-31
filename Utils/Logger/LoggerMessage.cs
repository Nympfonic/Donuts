using JetBrains.Annotations;

namespace Donuts.Utils.Logger;

public readonly struct LoggerMessage([NotNull] string message, LogLevel logLevel = LogLevel.Warning)
{
	[NotNull] public readonly string message = message;
	public readonly LogLevel logLevel = logLevel;
}