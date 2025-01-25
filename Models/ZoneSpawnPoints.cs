using Donuts.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Models;

/// <summary>
/// ZoneSpawnPoints is a dictionary that stores all the zone spawn points, with an internal keyword-to-zone mapping dictionary.
/// </summary>
[Serializable]
public class ZoneSpawnPoints : Dictionary<string, List<Vector3>>
{
	public enum KeywordZoneType
	{
		None,
		All,
		Start,
		Hotspot
	}
	
	private Dictionary<string, HashSet<Vector3>> _unusedStartingSpawnPoints = [];
	private Dictionary<KeywordZoneType, HashSet<string>> _keywordZoneMappings = [];

	/// <summary>
	/// This is to keep track of all the starting spawn points so we can reset the <see cref="_unusedStartingSpawnPoints"/> dictionary later.
	/// <p>Do not modify!</p>
	/// </summary>
	public Dictionary<string, HashSet<Vector3>> AllStartingSpawnPoints { get; } = [];
	
	/// <summary>
	/// Clear all data within ZoneSpawnPoints. Mainly for deserializing into an existing ZoneSpawnPoints object.
	/// </summary>
	public new void Clear()
	{
		AllStartingSpawnPoints.Clear();
		_unusedStartingSpawnPoints.Clear();
		_keywordZoneMappings.Clear();
		base.Clear();
	}
	
	/// <summary>
	/// Maps the <see cref="zoneName"/> to the <see cref="keyword"/> in the <see cref="_keywordZoneMappings"/> dictionary.
	/// </summary>
	/// <param name="zoneName">The zone to map to the target keyword.</param>
	/// <param name="keyword">The target keyword.</param>
	public void MapZoneToKeyword([NotNull] string zoneName, KeywordZoneType keyword)
	{
		if (keyword == KeywordZoneType.None)
		{
			return;
		}
		
		if (!_keywordZoneMappings.ContainsKey(keyword))
		{
			_keywordZoneMappings.Add(keyword, [zoneName]);
			return;
		}
		
		_keywordZoneMappings[keyword].Add(zoneName);
	}
	
	/// <summary>
	/// Gets a list of spawn points for all zones that are mapped to the specified keyword.
	/// </summary>
	/// <param name="keyword">The keyword to filter for zones.</param>
	[NotNull]
	public List<KeyValuePair<string, List<Vector3>>> GetSpawnPointsFromKeyword(KeywordZoneType keyword)
	{
		var list = new List<KeyValuePair<string, List<Vector3>>>();
		
		if (keyword == KeywordZoneType.None || !_keywordZoneMappings.TryGetValue(keyword, out HashSet<string> zoneNames))
		{
			return list;
		}
		
		foreach (string zoneName in zoneNames)
		{
			var pair = new KeyValuePair<string, List<Vector3>>(zoneName, this[zoneName]);
			list.Add(pair);
		}
		
		return list;
	}
	
	/// <summary>
	/// Removes the spawn point from the <see cref="_unusedStartingSpawnPoints"/> dictionary using the zone name as the key.
	/// </summary>
	/// <returns>True if the spawn point was removed from the hashset at the specified key, false if the dictionary is not initialized.</returns>
	public bool SetStartingSpawnPointAsUsed(string zoneName, Vector3 spawnPoint)
	{
		if (_unusedStartingSpawnPoints == null)
		{
			return false;
		}
		
		if (_unusedStartingSpawnPoints.TryGetValue(zoneName, out HashSet<Vector3> usedSpawnPoints))
		{
			usedSpawnPoints.Remove(spawnPoint);
			return true;
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets a random unused starting spawn point, along with its index number from the list.
	/// Will reset the zone's list of spawn points if all are used.
	/// </summary>
	/// <returns>A random unused starting spawn point or otherwise null.</returns>
	[CanBeNull]
	public Vector3? GetUnusedStartingSpawnPoint([CanBeNull] IList<string> startingZoneNames, [CanBeNull] out string zoneName)
	{
		if (Count == 0)
		{
			zoneName = null;
			return null;
		}
		
		// No list of starting zone names provided: look at the 'start' keyword zones
		if (startingZoneNames == null)
		{
			// No 'start' keyword zones
			if (!_keywordZoneMappings.TryGetValue(KeywordZoneType.Start, out HashSet<string> zoneNames) ||
				zoneNames.Count == 0)
			{
				zoneName = null;
				return null;
			}
			
			return SelectRandomStartingSpawnPoint(zoneNames, out zoneName);
		}

		// List of starting zone names provided: pick a random zone and spawn point from them
		if (startingZoneNames.Count > 0)
		{
			return SelectRandomStartingSpawnPoint(startingZoneNames, out zoneName);
		}
		
		zoneName = null;
		return null;
	}
	
	private Vector3 SelectRandomStartingSpawnPoint([NotNull] IEnumerable<string> zoneNames, [CanBeNull] out string zoneName)
	{
		string randomZone = zoneNames.PickRandomElement()!;
		HashSet<Vector3> spawnPoints = _unusedStartingSpawnPoints[randomZone];
			
		// Reset if all starting spawns for this zone are used
		if (spawnPoints.Count == 0)
		{
			spawnPoints.UnionWith(AllStartingSpawnPoints[randomZone]);
		}

		zoneName = randomZone;
		return spawnPoints.PickRandomElement();
	}
	
	public static bool IsKeywordZone([NotNull] string zoneName, out KeywordZoneType keyword)
	{
		if (zoneName.ToLower() == "all")
		{
			keyword = KeywordZoneType.All;
			return true;
		}

		return IsStartOrHotspotZone(zoneName, out keyword);
	}
	
	public static bool IsStartOrHotspotZone([NotNull] string zoneName, out KeywordZoneType keyword)
	{
		return IsStartZone(zoneName, out keyword) || IsHotspotZone(zoneName, out keyword);
	}
	
	public static bool IsStartZone([NotNull] string zoneName, out KeywordZoneType keyword)
	{
		if (zoneName.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			keyword = KeywordZoneType.Start;
			return true;
		}
		
		keyword = KeywordZoneType.None;
		return false;
	}
	
	public static bool IsHotspotZone([NotNull] string zoneName, out KeywordZoneType keyword)
	{
		if (zoneName.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			keyword = KeywordZoneType.Hotspot;
			return true;
		}
		
		keyword = KeywordZoneType.None;
		return false;
	}
}

/// <summary>
/// JsonConverter to convert <see cref="ZoneSpawnPoints"/> to and from JSON.
/// </summary>
public class ZoneSpawnPointsConverter : JsonConverter<ZoneSpawnPoints>
{
	public override void WriteJson(JsonWriter writer, ZoneSpawnPoints value, JsonSerializer serializer)
	{
		if (value == null)
		{
			throw new JsonSerializationException("ZoneSpawnPoints cannot be null!");
		}
		
		writer.WriteStartObject();
		
		foreach (KeyValuePair<string, List<Vector3>> zoneSpawnPoint in value)
		{
			writer.WritePropertyName(zoneSpawnPoint.Key);
			serializer.Serialize(writer, zoneSpawnPoint.Value);
		}
		
		writer.WriteEndObject();
	}
	
	public override ZoneSpawnPoints ReadJson(
		JsonReader reader,
		Type objectType,
		ZoneSpawnPoints existingValue,
		bool hasExistingValue,
		JsonSerializer serializer)
	{
		ZoneSpawnPoints zoneSpawnPoints;
		if (hasExistingValue)
		{
			zoneSpawnPoints = existingValue;
			zoneSpawnPoints.Clear();
		}
		else
		{
			zoneSpawnPoints = new ZoneSpawnPoints();
		}
		
		JObject obj = JObject.Load(reader);
		
		foreach ((string zoneName, JToken value) in obj)
		{
			var spawnPoints = value.ToObject<List<Vector3>>();
			zoneSpawnPoints.AddRangeToKey(zoneName, spawnPoints);
			
			// Add zone to "all" key
			zoneSpawnPoints.MapZoneToKeyword(zoneName, ZoneSpawnPoints.KeywordZoneType.All);
			
			// Add zone to start/hotspot key if it contains matching keywords
			if (!ZoneSpawnPoints.IsStartOrHotspotZone(zoneName, out ZoneSpawnPoints.KeywordZoneType keyword))
			{
				continue;
			}
			
			zoneSpawnPoints.MapZoneToKeyword(zoneName, keyword);
			
			// If zone is a start zone, add to starting spawn dictionary
			if (!ZoneSpawnPoints.IsStartZone(zoneName, out _)) continue;
			
			Dictionary<string, HashSet<Vector3>> allStartingSpawns = zoneSpawnPoints.AllStartingSpawnPoints;
			if (allStartingSpawns.ContainsKey(zoneName))
			{
				allStartingSpawns[zoneName].UnionWith(spawnPoints);
			}
			else
			{
				allStartingSpawns.Add(zoneName, [..spawnPoints]);
			}
		}
		
		return zoneSpawnPoints;
	}
}