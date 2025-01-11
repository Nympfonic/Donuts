using EFT;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public class SpawnCheckData(Vector3 position, string mapLocation, ReadOnlyCollection<Player> alivePlayers)
{
	public readonly Vector3 position = position;
	public readonly string mapLocation = mapLocation;
	public readonly ReadOnlyCollection<Player> alivePlayers = alivePlayers;

	public bool Success { get; set; }
}