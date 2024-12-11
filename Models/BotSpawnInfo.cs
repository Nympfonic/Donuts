using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Models;

public class BotSpawnInfo(PrepBotInfo botInfo, string zone, List<Vector3> spawnPoints)
{
	public WildSpawnType BotType { get; } = botInfo.SpawnType;
	public int GroupSize { get; } = botInfo.GroupSize;
	public List<Vector3> Coordinates { get; } = spawnPoints;
	public BotDifficulty Difficulty { get; } = botInfo.Difficulty;
	public EPlayerSide Faction { get; } = botInfo.Side;
	public string Zone { get; } = zone;
}
