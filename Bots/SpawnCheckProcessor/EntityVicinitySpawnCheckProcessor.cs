using EFT;
using JetBrains.Annotations;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class EntityVicinitySpawnCheckProcessor(
	[NotNull] string mapLocation,
	[NotNull] ReadOnlyCollection<Player> alivePlayers) : SpawnCheckProcessorBase
{
	public override bool Process(Vector3 spawnPoint)
	{
		bool checkPlayerVicinity = DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value;
		bool checkBotVicinity = DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsBool.Value;
		
		if (!checkPlayerVicinity && !checkBotVicinity)
		{
			return base.Process(spawnPoint);
		}
		
		float minDistancePlayer = GetMinDistanceFromPlayer(mapLocation);
		float minSqrMagnitudePlayer = minDistancePlayer * minDistancePlayer;
		
		float minDistanceBot = GetMinDistanceFromOtherBots(mapLocation);
		float minSqrMagnitudeBot = minDistanceBot * minDistanceBot;
		
		for (int i = alivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = alivePlayers[i];
			if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
			{
				continue;
			}
			
			float actualSqrMagnitude = (((IPlayer)player).Position - spawnPoint).sqrMagnitude;
			
			if (IsEntityTooClose(player, checkPlayerVicinity, checkBotVicinity, actualSqrMagnitude,
				minSqrMagnitudePlayer, minSqrMagnitudeBot))
			{
				return false;
			}
		}
		
		return base.Process(spawnPoint);
	}
	
	private static bool IsEntityTooClose(
		[NotNull] Player player,
		bool checkPlayerVicinity,
		bool checkBotVicinity,
		float actualSqrMagnitude,
		float minSqrMagnitudePlayer,
		float minSqrMagnitudeBot)
	{
		return (!player.IsAI && checkPlayerVicinity && actualSqrMagnitude < minSqrMagnitudePlayer) ||
			(player.IsAI && checkBotVicinity && actualSqrMagnitude < minSqrMagnitudeBot);
	}
	
	private static float GetMinDistanceFromPlayer(string mapLocation) =>
		mapLocation switch
		{
			"bigmap" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerCustoms.Value,
			"factory4_day" or "factory4_night" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerStreets.Value,
			"sandbox" or "sandbox_high" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerShoreline.Value,
			"woods" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerWoods.Value,
			"laboratory" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerInterchange.Value,
			_ => 50f,
		};
	
	private static float GetMinDistanceFromOtherBots(string mapLocation) =>
		mapLocation switch
		{
			"bigmap" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsCustoms.Value,
			"factory4_day" or "factory4_night" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsStreets.Value,
			"sandbox" or "sandbox_high" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsShoreline.Value,
			"woods" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsWoods.Value,
			"laboratory" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsInterchange.Value,
			_ => 0f,
		};
}