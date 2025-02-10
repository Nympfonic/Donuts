using Cysharp.Text;
using Donuts.Bots;
using Donuts.Utils;
using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Models;

public class SpawnPointsCache
{
	[NotNull] private readonly ZoneSpawnPoints _zoneSpawnPoints;
	[NotNull] private readonly string[] _zoneNames;
	
	private Vector3[] _cachedSpawnPoints;
	private Queue<Vector3> _spawnPointsToUse;
	
	public SpawnPointsCache([NotNull] ZoneSpawnPoints zoneSpawnPoints, [NotNull] string[] zoneNames)
	{
		_zoneSpawnPoints = zoneSpawnPoints;
		_zoneNames = zoneNames;
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning($"SpawnPointsCache Zone Names: {string.Join(", ", zoneNames)}");
		}
		
		InitializeSpawnPoints();
	}
	
	public Vector3? GetUnusedSpawnPoint()
	{
		if (_zoneSpawnPoints.Count == 0)
		{
			return null;
		}
		
		if (_spawnPointsToUse.Count == 0)
		{
			ResetSpawnPoints();
		}
		
		Vector3 spawnPoint = _spawnPointsToUse.Dequeue();
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning($"Retrieved unused spawn point: {spawnPoint.ToString()}");
		}
		
		return spawnPoint;
	}

	private void InitializeSpawnPoints()
	{
		// Merge zones into a hashset to ensure no duplicates
		var mergedZones = new HashSet<string>();
		foreach (string zoneName in _zoneNames)
		{
			// Check for exact keyword matches
			if (_zoneSpawnPoints.TryGetKeywordZoneMappings(zoneName, out HashSet<string> keywordZoneNames))
			{
				mergedZones.UnionWith(keywordZoneNames!);
			}
			// Otherwise just add the zone name as is
			else
			{
				mergedZones.Add(zoneName);
			}
		}
		
		// Populate the hashset with a randomized order of spawn points
		var mergedSpawnPoints = new HashSet<Vector3>();
		foreach (string zoneName in mergedZones.ShuffleElements())
		{
			List<Vector3> spawnPoints = _zoneSpawnPoints[zoneName]!.ShuffleElements();
			mergedSpawnPoints.UnionWith(spawnPoints);
		}
		
		// Cache the spawn points and create new queue to be used
		_cachedSpawnPoints = new Vector3[mergedSpawnPoints.Count];
		mergedSpawnPoints.CopyTo(_cachedSpawnPoints);
		_spawnPointsToUse = new Queue<Vector3>(_cachedSpawnPoints);
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning("Initialized spawn points cache");
		}
	}
	
	private void ResetSpawnPoints()
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning("Resetting spawn points cache");
		}
		
		// Already cached so we just create a new queue, passing the cached array into it
		if (_cachedSpawnPoints != null)
		{
			_cachedSpawnPoints = _cachedSpawnPoints.ShuffleElements();
			_spawnPointsToUse = new Queue<Vector3>(_cachedSpawnPoints);
			return;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning(
				$"{nameof(_cachedSpawnPoints)} is unexpectedly null. Initializing new cache...");
		}
		
		InitializeSpawnPoints();
	}
}