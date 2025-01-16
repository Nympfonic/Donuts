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
		// For the rest of the bots in the same group, check if the bot should be added to other bot groups' allies/enemies list
		// This is normally performed in BotSpawner::GetGroupAndSetEnemies(BotOwner, BotZone)
		else
		{
			botSpawner.method_5(bot);
		}
		return _group;
	}
}