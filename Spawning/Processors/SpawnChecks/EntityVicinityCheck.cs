﻿using Cysharp.Text;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Donuts.Spawning.Processors;

public class EntityVicinityCheck(
	[NotNull] string mapLocation,
	[NotNull] ReadOnlyCollection<Player> alivePlayers,
	[CanBeNull] HashSet<WildSpawnType> botTypesToIgnore = null) : SpawnCheckProcessorBase
{
	public override bool Process(Vector3 spawnPoint)
	{
		bool checkPlayerVicinityMin = DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value;
		bool checkPlayerVicinityMax = DefaultPluginVars.globalMaxSpawnDistanceFromPlayerBool.Value;
		bool checkBotVicinity = DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsBool.Value;
		
		float minDistancePlayer = GetMinDistanceFromPlayer(mapLocation);
		float minSqrMagnitudePlayer = minDistancePlayer * minDistancePlayer;
		
		float maxDistancePlayer = GetMaxDistanceFromPlayer(mapLocation);
		float maxSqrMagnitudePlayer = maxDistancePlayer * maxDistancePlayer;
		
		float minDistanceBot = GetMinDistanceFromOtherBots(mapLocation);
		float minSqrMagnitudeBot = minDistanceBot * minDistanceBot;
		
		for (int i = alivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = alivePlayers[i];
			if (player == null || !player.IsAlive())
			{
				continue;
			}
			
			float actualSqrMagnitude = (((IPlayer)player).Position - spawnPoint).sqrMagnitude;
			
			// If it's a bot
			if (player.IsAI)
			{
				if (botTypesToIgnore?.Contains(player.Profile.Info.Settings.Role) == true)
				{
					continue;
				}
				
				if (checkBotVicinity && IsEntityTooClose(actualSqrMagnitude, minSqrMagnitudeBot))
				{
					if (DefaultPluginVars.debugLogging.Value)
					{
						using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
						sb.AppendFormat("Bot \"{0}\" ({1}) is too close to the spawn point, aborting this wave spawn!",
							player.Profile.Nickname, player.ProfileId);
						DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(EntityVicinityCheck), nameof(Process));
					}
					
					return false;
				}
				
				continue;
			}
			
			// If it's a player
			if (checkPlayerVicinityMin && IsEntityTooClose(actualSqrMagnitude, minSqrMagnitudePlayer) ||
				checkPlayerVicinityMax && IsEntityTooFar(actualSqrMagnitude, maxSqrMagnitudePlayer))
			{
				if (DefaultPluginVars.debugLogging.Value)
				{
					using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
					sb.AppendFormat("Human player \"{0}\" ({1}) is too close to the spawn point, aborting this wave spawn!",
						player.Profile.Nickname, player.ProfileId);
					DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(EntityVicinityCheck), nameof(Process));
				}
				
				return false;
			}
			
			if (IsInPlayerLineOfSight(player, spawnPoint))
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
	
	private static bool IsEntityTooFar(float actualSqrMagnitude, float maxSqrMagnitude)
	{
		if (maxSqrMagnitude <= 0)
		{
			return false;
		}
		
		return actualSqrMagnitude > maxSqrMagnitude;
	}
	
	private static bool IsInPlayerLineOfSight(Player player, Vector3 spawnPosition)
	{
		EnemyPart playerHead = player.MainParts[BodyPartType.head];
		Vector3 playerHeadDirection = playerHead.Position - spawnPosition;
		
		bool isInLineOfSight = Physics.Raycast(spawnPosition, playerHeadDirection, out RaycastHit hitInfo,
				playerHeadDirection.magnitude, LayerMaskClass.HighPolyWithTerrainMask) &&
			hitInfo.collider == playerHead.Collider.Collider;
		
		if (DefaultPluginVars.debugLogging.Value && isInLineOfSight)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Human player \"{0}\" ({1}) has line of sight to the spawn point, aborting this wave spawn!",
				player.Profile.Nickname, player.ProfileId);
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(EntityVicinityCheck), nameof(Process));
		}
		
		return isInLineOfSight;
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
			_ => 50f
		};

	private static float GetMaxDistanceFromPlayer(string mapLocation) =>
		mapLocation switch
		{
			"bigmap" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerCustoms.Value,
			"factory4_day" or "factory4_night" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerStreets.Value,
			"sandbox" or "sandbox_high" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerShoreline.Value,
			"woods" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerWoods.Value,
			"laboratory" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMaxSpawnDistanceFromPlayerInterchange.Value,
			_ => 0f
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
			_ => 0f
		};
}