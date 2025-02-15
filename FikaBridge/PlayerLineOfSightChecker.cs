using Comfort.Common;
using EFT;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using System;
using UnityEngine;

namespace Donuts.FikaBridge;

public class PlayerLineOfSightChecker
{
	private readonly FikaClient _fikaClient;
	private readonly GameWorld _gameWorld;
	
	private readonly Camera _playerCamera;
	private readonly Plane[] _cameraPlanes;
	
	public PlayerLineOfSightChecker(FikaClient fikaClient)
	{
		_fikaClient = fikaClient;
		_gameWorld = Singleton<GameWorld>.Instance;
		_playerCamera = CameraClass.Instance.Camera;
		_cameraPlanes = GeometryUtility.CalculateFrustumPlanes(_playerCamera);
	}
	
	public bool CheckLineOfSightToPlayer(string profileId)
	{
		_gameWorld.allAlivePlayersByID.TryGetValue(profileId, out Player player);
		if (player == null || player.IsYourPlayer || player.HealthController == null || !player.HealthController.IsAlive)
		{
			return false;
		}
		
		return false;
	}
	
	private bool IsVisibleInCamera(Camera cam, Vector3 worldPosition)
	{
		Vector3 viewportPoint = cam.WorldToViewportPoint(worldPosition);
		return viewportPoint.z > 0 && viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
			viewportPoint.y >= 0 && viewportPoint.y <= 1;
	}
}