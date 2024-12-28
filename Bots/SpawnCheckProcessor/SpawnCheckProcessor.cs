using UnityToolkit.Structures;

namespace Donuts.Bots.SpawnCheckProcessor;

public abstract class SpawnCheckProcessorBase : IProcessor<SpawnCheckProcessorBase, SpawnCheckData>
{
	private SpawnCheckProcessorBase _nextProcessor;

	public SpawnCheckProcessorBase SetNext(SpawnCheckProcessorBase nextProcessor) => _nextProcessor = nextProcessor;
	public virtual void Process(SpawnCheckData data) => _nextProcessor?.Process(data);
}