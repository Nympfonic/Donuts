using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class WallSpawnCheckProcessor : SpawnCheckProcessorBase
{
	private const string WALL_OBJECT_NAME_UPPER = "WALLS";
	private readonly Collider[] _spawnCheckColliderBuffer = new Collider[50];
	private readonly Vector3 _boxCheckScale = Vector3.one;
	
	public override void Process(SpawnCheckData data)
	{
		int size = Physics.OverlapBoxNonAlloc(data.Position, _boxCheckScale, _spawnCheckColliderBuffer,
			Quaternion.identity, LayerMaskClass.LowPolyColliderLayer);

		if (size <= 0)
		{
			data.Success = true;
			base.Process(data);
			return;
		}
		
		for (var i = 0; i < size; i++)
		{
			Transform currentTransform = _spawnCheckColliderBuffer[i].transform;
			// Recursively check for "WALLS" string in the game object's name, going upwards to root of hierarchy
			while (currentTransform != null)
			{
				if (currentTransform.gameObject.name.ToUpper().Contains(WALL_OBJECT_NAME_UPPER))
				{
					data.Success = false;
					return;
				}

				currentTransform = currentTransform.parent;
			}
		}
		
		data.Success = true;
		base.Process(data);
	}
}