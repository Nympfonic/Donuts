using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Bots.SpawnCheckProcessor;

public struct SpawnCheckData(Vector3 position, string mapLocation, List<Player> alivePlayers)
{
	public Vector3 Position { get; } = position;
	public string MapLocation { get; } = mapLocation;
	public List<Player> AlivePlayers { get; } = alivePlayers;
	public bool Success { get; set; }
}