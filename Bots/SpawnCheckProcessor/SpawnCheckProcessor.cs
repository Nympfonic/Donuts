namespace Donuts.Bots.SpawnCheckProcessor;

public interface ISpawnCheckProcessor
{
	ISpawnCheckProcessor SetNext(ISpawnCheckProcessor nextProcessor);
	void Process(SpawnCheckData data);
}

public abstract class SpawnCheckProcessorBase : ISpawnCheckProcessor
{
	private ISpawnCheckProcessor _nextProcessor;

	public ISpawnCheckProcessor SetNext(ISpawnCheckProcessor nextProcessor) => _nextProcessor = nextProcessor;
	public virtual void Process(SpawnCheckData data) => _nextProcessor?.Process(data);
}