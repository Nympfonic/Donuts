using EFT;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class PlayerLineOfSightSpawnCheckProcessor : SpawnCheckProcessorBase
{
	public override void Process(SpawnCheckData data)
	{
		foreach (Player player in data.AlivePlayers)
		{
			if (player.IsAI)
			{
				continue;
			}

			// If the spawn position is in at least one player's line of sight, cancel the spawn
			if (IsInPlayerLineOfSight(player, data.Position))
			{
				data.Success = false;
				return;
			}
		}

		data.Success = true;
		base.Process(data);
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