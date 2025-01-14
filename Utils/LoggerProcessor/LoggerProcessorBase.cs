using UnityToolkit.Structures;

namespace Donuts.Utils.LoggerProcessor;

public class LoggerProcessorBase : IProcessor<LoggerProcessorBase, LoggerData>
{
	private LoggerProcessorBase _nextProcessor;
	
	public LoggerProcessorBase SetNext(LoggerProcessorBase nextProcessor) => _nextProcessor = nextProcessor;
	public virtual void Process(LoggerData data) => _nextProcessor?.Process(data);
}