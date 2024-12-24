namespace Donuts.Models;

public class SpawnTypeConfig(
	string difficultySetting,
	BotConfig botConfig,
	int maxBotCount)
{
	public string DifficultySetting { get; } = difficultySetting;
	public BotConfig BotConfig { get; } = botConfig;
	public int MaxBotCount { get; } = maxBotCount;
}