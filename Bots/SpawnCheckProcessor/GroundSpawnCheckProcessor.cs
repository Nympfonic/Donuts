using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class GroundSpawnCheckProcessor : SpawnCheckProcessorBase
{
	public override void Process(SpawnCheckData data)
	{
		var ray = new Ray(data.position, Vector3.down);
		if (!Physics.Raycast(ray, 10f, LayerMaskClass.HighPolyWithTerrainMask))
		{
			data.Success = false;
			return;
		}
		
		data.Success = true;
		base.Process(data);
	}
}