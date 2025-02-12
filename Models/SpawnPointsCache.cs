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
	
	private Queue<Vector3>[] _cachedSpawnPoints;
	private Queue<Vector3>[] _spawnPointsToUse;
	
	private int _zoneCount;
	private int _currentIndex;
	
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
		
		Queue<Vector3> spawnPointQueue = _spawnPointsToUse[_currentIndex];
		if (spawnPointQueue.Count == 0)
		{
			ResetSpawnPoints(_currentIndex);
		}
		
		_currentIndex = (_currentIndex + 1) % _zoneCount;
		Vector3 spawnPoint = spawnPointQueue.Dequeue();
		
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
		
		// Cache the spawn points and create new queues to be used
		_zoneCount = mergedZones.Count;
		_cachedSpawnPoints = new Queue<Vector3>[_zoneCount];
		
		var index = 0;
		foreach (string zoneName in mergedZones)
		{
			var queue = new Queue<Vector3>(_zoneSpawnPoints[zoneName]!.ShuffleElements());
			_cachedSpawnPoints[index++] = new Queue<Vector3>(queue);
		}
		
		_cachedSpawnPoints.ShuffleElements();
		
		_spawnPointsToUse = new Queue<Vector3>[_zoneCount];
		for (var i = 0; i < _zoneCount; i++)
		{
			_spawnPointsToUse[i] = new Queue<Vector3>(_cachedSpawnPoints[i]);
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning("Initialized spawn points cache");
		}
	}
	
	private void ResetSpawnPoints(int index)
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning("Resetting spawn points cache");
		}
		
		// Already cached so we just create a new queue, passing the cached array into it
		if (_cachedSpawnPoints != null && _cachedSpawnPoints.Length > 0)
		{
			foreach (Vector3 spawnPoint in _cachedSpawnPoints[index].ShuffleElements())
			{
				_spawnPointsToUse[index].Enqueue(spawnPoint);
			}
			return;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogWarning(
				$"{nameof(_cachedSpawnPoints)} is unexpectedly null or empty. Initializing new cache...");
		}
		
		InitializeSpawnPoints();
	}
}