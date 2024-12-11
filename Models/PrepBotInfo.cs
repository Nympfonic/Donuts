using EFT;

namespace Donuts.Models;

public class PrepBotInfo(
	WildSpawnType spawnType,
	BotDifficulty difficulty,
	EPlayerSide side,
	bool isGroup = false,
	int groupSize = 1)
{
	public WildSpawnType SpawnType { get; } = spawnType;
	public BotDifficulty Difficulty { get; } = difficulty;
	public EPlayerSide Side { get; } = side;
	public BotCreationDataClass Bots { get; set; }
	public bool IsGroup { get; } = isGroup;
	public int GroupSize { get; } = groupSize;
	
	
}