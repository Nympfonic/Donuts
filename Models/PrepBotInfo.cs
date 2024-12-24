namespace Donuts.Models;

public class PrepBotInfo(
	BotDifficulty difficulty,
	bool isGroup = false,
	int groupSize = 1)
{
	public BotDifficulty Difficulty { get; } = difficulty;
	public BotCreationDataClass Bots { get; set; }
	public bool IsGroup { get; } = isGroup;
	public int GroupSize { get; } = groupSize;
}