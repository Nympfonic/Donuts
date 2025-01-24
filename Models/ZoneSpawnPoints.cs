using Donuts.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Models;

[Serializable]
public class ZoneSpawnPoints : Dictionary<string, List<Vector3>>
{
	private List<Vector3> _unusedStartingSpawnPoints;
	
	/// <summary>
	/// The original JSON deserialized into a Dictionary&lt;string, List&lt;Vector3&gt;&gt; without filtering into Donuts' keyword zones (all/start/hotspot).
	/// <p>Used for serializing back into JSON while maintaining the original zones from deserialization.</p>
	/// </summary>
	[CanBeNull] public Dictionary<string, List<Vector3>> OriginalSerialization { get; set; }
	
	public new void Clear()
	{
		OriginalSerialization = null;
		_unusedStartingSpawnPoints = null;
		base.Clear();
	}
	
	/// <summary>
	/// Removes the spawn point from the <see cref="_unusedStartingSpawnPoints"/> list at the specified index.
	/// </summary>
	public bool SetStartingSpawnPointAsUsed(int index)
	{
		if (_unusedStartingSpawnPoints == null)
		{
			return false;
		}
		_unusedStartingSpawnPoints.RemoveAt(index);
		return true;
	}
	
	/// <summary>
	/// Gets a random unused starting spawn point, along with its index number from the list.
	/// </summary>
	/// <returns>A random unused starting spawn point or otherwise null.</returns>
	[CanBeNull]
	public Vector3? GetUnusedStartingSpawnPoint(out int index)
	{
		if (Count == 0 || !TryGetValue("start", out List<Vector3> startingSpawnPoints) || startingSpawnPoints.Count == 0)
		{
			index = -1;
			return null;
		}

		// If used up all zones, reset the _usedStartingSpawnPoints list
		if (_unusedStartingSpawnPoints == null || _unusedStartingSpawnPoints.Count == 0)
		{
			_unusedStartingSpawnPoints = [..startingSpawnPoints];
		}
		
		return _unusedStartingSpawnPoints.PickRandomElement(out index);
	}

	public static bool IsKeywordZone([NotNull] string zoneName, [CanBeNull] out string keyword)
	{
		if (zoneName.ToLower() == "all")
		{
			keyword = "all";
			return true;
		}

		return IsStartOrHotspotZone(zoneName, out keyword);
	}
	
	public static bool IsStartOrHotspotZone([NotNull] string zoneName, [CanBeNull] out string keyword)
	{
		if (zoneName.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			keyword = "start";
			return true;
		}
		
		return IsHotspotZone(zoneName, out keyword);
	}

	public static bool IsHotspotZone([NotNull] string zoneName, [CanBeNull] out string keyword)
	{
		if (zoneName.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			keyword = "hotspot";
			return true;
		}
		
		keyword = null;
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
		
		Dictionary<string, List<Vector3>> zoneSpawnPoints = value.OriginalSerialization ?? value;
		
		foreach (KeyValuePair<string, List<Vector3>> zoneSpawnPoint in zoneSpawnPoints)
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
		
		var originalZoneSpawnPoints = new Dictionary<string, List<Vector3>>();
		JObject obj = JObject.Load(reader);
		
		foreach ((string zoneName, JToken value) in obj)
		{
			var spawnPoints = value.ToObject<List<Vector3>>();
			// Save the original for serialization
			originalZoneSpawnPoints.AddRangeToKey(zoneName, spawnPoints);
			// Add zone to "all" key
			zoneSpawnPoints.AddRangeToKey("all", spawnPoints);
			// If someone accidentally specified a zone name as simply "All" or "all", continue to next iteration
			if (zoneName.ToLower() == "all")
			{
				continue;
			}
			// Add zone to start/hotspot key if it contains matching keywords, otherwise add to the specified zone name key
			zoneSpawnPoints.AddRangeToKey(
				ZoneSpawnPoints.IsStartOrHotspotZone(zoneName, out string keywordZoneName) ? keywordZoneName : zoneName,
				spawnPoints);
		}
		
		zoneSpawnPoints.OriginalSerialization = originalZoneSpawnPoints;
		
		return zoneSpawnPoints;
	}
}