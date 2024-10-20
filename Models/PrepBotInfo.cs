using EFT;

namespace Donuts.Models
{
	public class PrepBotInfo(WildSpawnType spawnType, BotDifficulty difficulty, EPlayerSide side,
		bool isGroup = false, int groupSize = 1)
	{
		public WildSpawnType SpawnType { get; set; } = spawnType;
		public BotDifficulty Difficulty { get; set; } = difficulty;
		public EPlayerSide Side { get; set; } = side;
		public BotCreationDataClass Bots { get; set; }
		public bool IsGroup { get; set; } = isGroup;
		public int GroupSize { get; set; } = groupSize;
	}
}
