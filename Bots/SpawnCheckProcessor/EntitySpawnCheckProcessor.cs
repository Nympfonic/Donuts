using EFT;
using JetBrains.Annotations;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class EntitySpawnCheckProcessor(
	[NotNull] string mapLocation,
	[NotNull] ReadOnlyCollection<Player> alivePlayers) : SpawnCheckProcessorBase
{
	public override bool Process(Vector3 spawnPoint)
	{
		bool checkPlayerVicinity = DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value;
		bool checkBotVicinity = DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsBool.Value;
		
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
			
			// If it's a bot
			if (player.IsAI)
			{
				if (checkBotVicinity && IsEntityTooClose(actualSqrMagnitude, minSqrMagnitudeBot))
				{
					return false;
				}
				
				continue;
			}
			
			// If it's a player
			if ((checkPlayerVicinity && IsEntityTooClose(actualSqrMagnitude, minSqrMagnitudePlayer)) ||
				IsInPlayerLineOfSight(player, spawnPoint))
			{
				return false;
			}
		}
		
		return base.Process(spawnPoint);
	}
	
	private static bool IsEntityTooClose(float actualSqrMagnitude, float minSqrMagnitude)
	{
		return actualSqrMagnitude < minSqrMagnitude;
	}
	
	private static bool IsInPlayerLineOfSight(Player player, Vector3 spawnPosition)
	{
		EnemyPart playerHead = player.MainParts[BodyPartType.head];
		Vector3 playerHeadDirection = playerHead.Position - spawnPosition;
		
		return Physics.Raycast(spawnPosition, playerHeadDirection, out RaycastHit hitInfo,
				playerHeadDirection.magnitude, LayerMaskClass.HighPolyWithTerrainMask) &&
			hitInfo.collider == playerHead.Collider.Collider;
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