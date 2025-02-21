using JetBrains.Annotations;
using System.Collections.Generic;

namespace Donuts.Spawning.Models;

public class BotCreationDataCache(int capacity = 0) : Dictionary<PrepBotInfo.Key, Queue<PrepBotInfo>>(capacity)
{
	public void Enqueue(PrepBotInfo.Key key, PrepBotInfo prepBotInfo)
	{
		if (!TryGetValue(key, out Queue<PrepBotInfo> botDataQueue))
		{
			var newQueue = new Queue<PrepBotInfo>();
			newQueue.Enqueue(prepBotInfo);
			Add(key, newQueue);
			return;
		}
		
		if (botDataQueue == null)
		{
			this[key] = new Queue<PrepBotInfo>();
		}
		
		this[key].Enqueue(prepBotInfo);
	}
	
	public bool TryDequeue(PrepBotInfo.Key key, [CanBeNull] out PrepBotInfo prepBotInfo)
	{
		if (!TryPeek(key, out prepBotInfo))
		{
			return false;
		}
		
		this[key].Dequeue();
		return true;
	}
	
	public bool TryPeek(PrepBotInfo.Key key, out PrepBotInfo prepBotInfo)
	{
		if (!TryGetValue(key, out Queue<PrepBotInfo> botDataQueue) || botDataQueue == null || botDataQueue.Count == 0)
		{
			prepBotInfo = null;
			return false;
		}
		
		prepBotInfo = botDataQueue.Peek();
		return true;
	}
}