using EFT;
using JetBrains.Annotations;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class PlayerLineOfSightSpawnCheckProcessor(
	[NotNull] ReadOnlyCollection<Player> alivePlayers) : SpawnCheckProcessorBase
{
	public override bool Process(Vector3 spawnPoint)
	{
		for (int i = alivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = alivePlayers[i];
			if (player == null || player.IsAI || player.HealthController == null || !player.HealthController.IsAlive)
			{
				continue;
			}
			
			// If the spawn position is in at least one player's line of sight, cancel the spawn
			if (IsInPlayerLineOfSight(player, spawnPoint))
			{
				return false;
			}
		}
		
		return base.Process(spawnPoint);
	}
	
	private static bool IsInPlayerLineOfSight(Player player, Vector3 spawnPosition)
	{
		EnemyPart playerHead = player.MainParts[BodyPartType.head];
		Vector3 playerHeadDirection = playerHead.Position - spawnPosition;
		
		return Physics.Raycast(spawnPosition, playerHeadDirection, out RaycastHit hitInfo,
				playerHeadDirection.magnitude, LayerMaskClass.HighPolyWithTerrainMask) &&
			hitInfo.collider == playerHead.Collider.Collider;
	}
}