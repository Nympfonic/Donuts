namespace Donuts.Models;

public class SpawnTypeConfig(
	string difficultySetting,
	StartingBotConfig startingBotConfig,
	int maxBotCount)
{
	public string DifficultySetting { get; } = difficultySetting;
	public StartingBotConfig StartingBotConfig { get; } = startingBotConfig;
	public int MaxBotCount { get; } = maxBotCount;
}