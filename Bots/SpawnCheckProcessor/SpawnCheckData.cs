using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class SpawnCheckData(Vector3 position, string mapLocation, List<Player> alivePlayers)
{
	public readonly Vector3 position = position;
	public readonly string mapLocation = mapLocation;
	public readonly List<Player> alivePlayers = alivePlayers;

	public bool Success { get; set; }
}