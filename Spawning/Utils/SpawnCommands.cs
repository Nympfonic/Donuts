using Comfort.Common;
using Donuts.Utils;
using EFT;
using EFT.Console.Core;
using EFT.UI;
using System.Collections.Generic;
using UnityToolkit.Extensions;

namespace Donuts.Spawning.Utils;

public class SpawnCommands
{
	[ConsoleCommand("despawnallbots", description: "Despawn all bots immediately")]
	public static void DespawnAllBots()
	{
		if (!CheckIfInRaid())
		{
			return;
		}
		
		List<Player> allAlivePlayers = Singleton<GameWorld>.Instance.AllAlivePlayersList;
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			if (player == null || !player.IsAI || !player.IsAlive())
			{
				continue;
			}
			
			BotOwner botOwner = player.AIData.BotOwner;
			((BaseLocalGame<EftGamePlayerOwner>)Singleton<AbstractGame>.Instance).BotDespawn(botOwner);
		}
	}
	
	private static bool CheckIfInRaid()
	{
		if (Singleton<AbstractGame>.Instance.OrNull()?.InRaid is null or false)
		{
			ConsoleScreen.LogError("Must be in raid to execute this command");
			return false;
		}
		
		return true;
	}
}