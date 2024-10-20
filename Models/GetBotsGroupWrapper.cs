using EFT;

namespace Donuts.Models
{
	/// <summary>
	/// Custom <see cref="BotSpawner.GetGroupAndSetEnemies"/> wrapper that handles grouping bots into multiple groups within the same botzone.
	/// </summary>
	public class GetBotsGroupWrapper(BotSpawner botSpawner)
	{
		private BotsGroup group;

		public BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
		{
			// If we haven't found/created our BotsGroup yet, do so, and then lock it so nobody else can use it
			if (group == null)
			{
				group = botSpawner.GetGroupAndSetEnemies(bot, zone);
				group.Lock();
			}

			return group;
		}
	}
}
