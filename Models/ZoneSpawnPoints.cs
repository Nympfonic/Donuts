using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Donuts.Models;

public class ZoneSpawnPoints : Dictionary<string, List<Vector3>>
{
	private readonly ReadOnlyCollection<KeyValuePair<string, List<Vector3>>> _emptyZoneSpawnPoints =
		new(new List<KeyValuePair<string, List<Vector3>>>());
	private readonly HashSet<string> _usedZones = [];
	
	/// <summary>
	/// Initializes the zone spawn points based on the provided data.
	/// </summary>
	/// <param name="inputZoneSpawnPoints">The zone spawn points data that was deserialized from the JSON config.</param>
	/// <param name="zoneNames">The list of zone names used to determine how they are added to the dictionary.</param>
	public void SetZones(
		[NotNull] IDictionary<string, List<Position>> inputZoneSpawnPoints,
		[NotNull] IList<string> zoneNames)
	{
		foreach (string zoneName in zoneNames)
		{
			if (zoneName == "all")
				AddAllZones(inputZoneSpawnPoints);
			else if (zoneName is "start" or "hotspot")
				AddHotspotStartZones(inputZoneSpawnPoints, zoneName);
			else
				AddSingleZone(inputZoneSpawnPoints, zoneName);
		}
	}
	
	/// <summary>
	/// Adds a zone by name to the <see cref="_usedZones"/> hashset.
	/// </summary>
	/// <returns>True if it was successful at adding the zone to the hashset or otherwise false.</returns>
	public bool AddZoneToUsedZones(string zoneName) => _usedZones.Add(zoneName);
	
	/// <summary>
	/// A read-only list of unused zones and their spawn points.
	/// </summary>
	/// <returns>A read-only list of unused zones and their spawn points or an empty list.</returns>
	[NotNull]
	public ReadOnlyCollection<KeyValuePair<string, List<Vector3>>> GetUnusedZoneSpawnPoints()
	{
		if (Count == 0)
		{
			return _emptyZoneSpawnPoints;
		}
		
		if (_usedZones.Count == Count)
		{
			_usedZones.Clear();
			return this.ToList().AsReadOnly();
		}
		
		var list = new List<KeyValuePair<string, List<Vector3>>>(Count - _usedZones.Count);
		foreach (KeyValuePair<string, List<Vector3>> zoneSpawnPoint in this)
		{
			if (!_usedZones.Contains(zoneSpawnPoint.Key))
			{
				list.Add(zoneSpawnPoint);
			}
		}
		
		return list.AsReadOnly();
	}

	private void AddAllZones([NotNull] IDictionary<string, List<Position>> inputZoneSpawnPoints)
	{
		foreach (KeyValuePair<string, List<Position>> zoneSpawnPoint in inputZoneSpawnPoints)
		{
			AddZoneSpawnPoint(zoneSpawnPoint.Key, zoneSpawnPoint.Value);
		}
	}

	private void AddHotspotStartZones(
		[NotNull] IDictionary<string, List<Position>> inputZoneSpawnPoints,
		[NotNull] string zoneName)
	{
		foreach (KeyValuePair<string, List<Position>> zoneSpawnPoint in inputZoneSpawnPoints)
		{
			if (zoneSpawnPoint.Key.IndexOf(zoneName, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				AddZoneSpawnPoint(zoneSpawnPoint.Key, zoneSpawnPoint.Value);
			}
		}
	}

	private void AddSingleZone(
		[NotNull] IDictionary<string, List<Position>> inputZoneSpawnPoints,
		[NotNull] string zoneName)
	{
		if (inputZoneSpawnPoints.TryGetValue(zoneName, out List<Position> spawnPoints))
		{
			AddZoneSpawnPoint(zoneName, spawnPoints);
		}
	}

	private void AddZoneSpawnPoint([NotNull] string zoneName, [NotNull] IList<Position> spawnPoints)
	{
		if (!ContainsKey(zoneName))
		{
			this[zoneName] = [];
		}
		
		this[zoneName].AddRange(spawnPoints.Select(p => (Vector3)p));
	}
}