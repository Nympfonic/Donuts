using Cysharp.Text;
using Donuts.Utils;
using UnityEngine;

namespace Donuts.Spawning.Processors;

public class GroundCheck : SpawnCheckProcessorBase
{
	public override bool Process(Vector3 spawnPoint)
	{
		var ray = new Ray(spawnPoint, Vector3.down);
		bool isNearGround = Physics.Raycast(ray, 10f, LayerMaskClass.HighPolyWithTerrainMask);
		
		if (DefaultPluginVars.debugLogging.Value && !isNearGround)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Spawn point {0} is not near the ground, aborting wave spawn! Please adjust the spawn point in the ZoneSpawnPoints json!",
				spawnPoint);
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(GroundCheck), nameof(Process));
		}
		
		return isNearGround && base.Process(spawnPoint);
	}
}