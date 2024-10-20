using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace Donuts.Models;

public class BotSpawnInfo(WildSpawnType botType, int groupSize, List<Vector3> coordinates,
	BotDifficulty difficulty, EPlayerSide faction, string zone)
{
	public WildSpawnType BotType { get; set; } = botType;
	public int GroupSize { get; set; } = groupSize;
	public List<Vector3> Coordinates { get; set; } = coordinates;
	public BotDifficulty Difficulty { get; set; } = difficulty;
	public EPlayerSide Faction { get; set; } = faction;
	public string Zone { get; set; } = zone;
}
