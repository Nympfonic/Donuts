using UnityEngine;

namespace Donuts.Spawning.Processors;

public class GroundCheck : SpawnCheckProcessorBase
{
	public override bool Process(Vector3 spawnPoint)
	{
		var ray = new Ray(spawnPoint, Vector3.down);
		return Physics.Raycast(ray, 10f, LayerMaskClass.HighPolyWithTerrainMask) && base.Process(spawnPoint);
	}
}