using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Models;

public class BotSpawnInfo(PrepBotInfo botInfo, string zone, List<Vector3> spawnPoints)
{
	public int GroupSize { get; } = botInfo.GroupSize;
	public List<Vector3> Coordinates { get; } = spawnPoints;
	public string Zone { get; } = zone;
}
