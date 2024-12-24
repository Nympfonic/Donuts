using Donuts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Donuts.Utils;

internal static class SpawnPointHelper
{
	internal static void SetSpawnPointsForZones(
		IDictionary<string, List<Vector3>> spawnPoints,
		IDictionary<string, List<Position>> zones,
		IList<string> zoneNames)
	{
		foreach (string zoneName in zoneNames)
		{
			if (zoneName == "all")
				AddAllZones(spawnPoints, zones);
			else if (zoneName is "start" or "hotspot")
				AddHotspotStartZones(spawnPoints, zones, zoneName);
			else
				AddZones(spawnPoints, zones, zoneName);
		}
	}
	
	internal static string SelectUnusedZone(HashSet<string> usedZones, IDictionary<string, List<Vector3>> spawnPointsDict)
	{
		List<string> zones = spawnPointsDict.Keys.ShuffleElements();
		string selectedZone = null;
		
		foreach (string zone in zones)
		{
			if (!usedZones.Contains(zone))
			{
				selectedZone = zone;
				break;
			}
		}
				
		if (selectedZone == null)
		{
			usedZones.Clear();
			selectedZone = zones[0];
		}
		
		usedZones.Add(selectedZone);
		return selectedZone;
	}

	private static void AddAllZones(IDictionary<string, List<Vector3>> spawnPoints, IDictionary<string, List<Position>> zones)
	{
		foreach (KeyValuePair<string, List<Position>> zone in zones)
		{
			AddCoordinatesToDictionary(spawnPoints, zone.Key, zone.Value);
		}
	}

	private static void AddHotspotStartZones(
		IDictionary<string, List<Vector3>> spawnPoints,
		IDictionary<string, List<Position>> zones,
		string zoneName)
	{
		foreach (KeyValuePair<string, List<Position>> zone in zones)
		{
			if (zone.Key.IndexOf(zoneName, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				AddCoordinatesToDictionary(spawnPoints, zone.Key, zone.Value);
			}
		}
	}

	private static void AddZones(
		IDictionary<string, List<Vector3>> spawnPoints,
		IDictionary<string, List<Position>> zones,
		string zoneName)
	{
		if (zones.TryGetValue(zoneName, out List<Position> coordinates))
		{
			AddCoordinatesToDictionary(spawnPoints, zoneName, coordinates);
		}
	}

	private static void AddCoordinatesToDictionary(
		IDictionary<string, List<Vector3>> spawnPoints,
		string zoneKey,
		IList<Position> coordinates)
	{
		if (!spawnPoints.ContainsKey(zoneKey))
		{
			spawnPoints[zoneKey] = [];
		}
		spawnPoints[zoneKey].AddRange(coordinates.Select(position => (Vector3)position));
	}
}