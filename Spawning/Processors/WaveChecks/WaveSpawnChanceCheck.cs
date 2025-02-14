using Cysharp.Text;
using Donuts.Spawning.Models;
using Donuts.Utils;
using UnityEngine;

namespace Donuts.Spawning.Processors;

public class WaveSpawnChanceCheck : WaveSpawnProcessorBase
{
	public override bool Process(BotWave data)
	{
		if (IsSpawnChanceSuccessful(data.SpawnChance))
		{
			return base.Process(data);
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogDebugDetailed(
				$"Resetting timer for GroupNum {data.GroupNum.ToString()}, reason: Spawn chance check failed.",
				nameof(WaveSpawnChanceCheck), nameof(Process));
		}
		
		return false;
	}
	
	private static bool IsSpawnChanceSuccessful(int spawnChance)
	{
		int randomValue = Random.Range(0, 100);
		bool canSpawn = randomValue < spawnChance;
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("SpawnChance: {0}, RandomValue: {1}, CanSpawn: {2}", spawnChance.ToString(),
				randomValue.ToString(), canSpawn.ToString());
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(WaveSpawnChanceCheck), nameof(IsSpawnChanceSuccessful));
		}
		
		return canSpawn;
	}
}