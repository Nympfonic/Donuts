using EFT;

namespace Donuts.Models;

/// <summary>
/// Custom <see cref="BotSpawner.GetGroupAndSetEnemies"/> wrapper that handles grouping bots into multiple groups within the same bot zone.
/// </summary>
public class GetBotsGroupWrapper(BotSpawner botSpawner)
{
	private BotsGroup _group;

	public BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
	{
		// If we haven't found/created our BotsGroup yet, do so, and then lock it so nobody else can use it
		if (_group == null)
		{
			_group = botSpawner.GetGroupAndSetEnemies(bot, zone);
			_group.Lock();
		}
		return _group;
	}
}