using Cysharp.Text;
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
	
	/// <summary>
	/// This is to keep track of all the starting spawn points so we can reset the <see cref="_unusedStartingSpawnPoints"/> dictionary later.
	/// <p>Do not modify!</p>
	/// </summary>
	private Dictionary<string, HashSet<Vector3>> _allStartingSpawnPoints = [];
	private Dictionary<string, HashSet<Vector3>> _unusedStartingSpawnPoints = [];
	private Dictionary<KeywordZoneType, HashSet<string>> _keywordZoneMappings = [];
	
	/// <summary>
	/// Clear all data within ZoneSpawnPoints. Mainly for deserializing into an existing ZoneSpawnPoints object.
	/// </summary>
	public new void Clear()
	{
		_allStartingSpawnPoints.Clear();
		_unusedStartingSpawnPoints.Clear();
		_keywordZoneMappings.Clear();
		base.Clear();
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
			if (usedSpawnPoints == null)
			{
				return false;
			}
			
			_unusedStartingSpawnPoints[zoneName].Remove(spawnPoint);
			return true;
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets a random unused starting spawn point, along with its zone name from the list.
	/// </summary>
	/// <returns>A random unused starting spawn point or otherwise null.</returns>
	[CanBeNull]
	public Vector3? GetUnusedStartingSpawnPoint([NotNull] IList<string> startingZoneNames, [CanBeNull] out string zoneName)
	{
		if (Count == 0)
		{
			zoneName = null;
			return null;
		}

#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		sb.Clear();
		sb.AppendLine("_keywordZoneMappings:");
		foreach (KeyValuePair<KeywordZoneType, HashSet<string>> kvp in _keywordZoneMappings)
		{
			sb.AppendFormat("Keyword: {0}, Zone Names: ", kvp.Key);
			if (kvp.Value == null || kvp.Value.Count == 0)
			{
				sb.Append("N/A\n");
				continue;
			}
			foreach (string z in kvp.Value)
			{
				sb.Append(z);
				sb.Append(", ");
			}
			sb.AppendLine();
		}
		DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(ZoneSpawnPoints), nameof(GetUnusedStartingSpawnPoint));
#endif
		
		// If it's a keyword zone (all/hotspot/start), get the correct list of zone names instead
		if (startingZoneNames.Count == 1 &&
			IsKeywordZone(startingZoneNames[0], out KeywordZoneType keyword, exactMatch: true))
		{
			if (_keywordZoneMappings.TryGetValue(keyword, out HashSet<string> keywordZoneNames))
			{
				return SelectRandomStartingSpawnPoint(keywordZoneNames, out zoneName);
			}
			
			// Failsafe: Is a keyword zone but could not find it in the keyword mapping dictionary
#if DEBUG
			sb.Clear();
			sb.AppendFormat("Donuts: Keyword {0} not found in the {1} dictionary", keyword.ToString(), nameof(_keywordZoneMappings));
			DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(ZoneSpawnPoints), nameof(GetUnusedStartingSpawnPoint));
#endif
			zoneName = null;
			return null;
		}
		
		// List of starting zone names provided: pick a random zone and spawn point from them
		if (startingZoneNames.Count >= 1)
		{
			return SelectRandomStartingSpawnPoint(startingZoneNames, out zoneName);
		}
		
		// Empty list of starting zone names provided: look at the 'all' keyword zones
		if (!_keywordZoneMappings.TryGetValue(KeywordZoneType.All, out HashSet<string> allZoneNames) ||
			allZoneNames.Count == 0)
		{
			// No 'all' keyword zones
			zoneName = null;
			return null;
		}
		
		return SelectRandomStartingSpawnPoint(allZoneNames, out zoneName);
	}
	
	private Vector3? SelectRandomStartingSpawnPoint([NotNull] IEnumerable<string> zoneNames, [CanBeNull] out string zoneName)
	{
		string randomZone = zoneNames.PickRandomElement()!;
#if DEBUG
		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
		sb.AppendFormat("Randomly selected zone: {0}", randomZone);
		DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(ZoneSpawnPoints), nameof(SelectRandomStartingSpawnPoint));
#endif
		if (!_unusedStartingSpawnPoints.TryGetValue(randomZone, out HashSet<Vector3> spawnPoints))
		{
			zoneName = null;
			return null;
		}
			
		// Reset if all starting spawns for this zone are used
		if (spawnPoints.Count == 0)
		{
			_unusedStartingSpawnPoints[randomZone] = [.._allStartingSpawnPoints[randomZone]];
			spawnPoints = _unusedStartingSpawnPoints[randomZone];
		}

		zoneName = randomZone;
		return spawnPoints.PickRandomElement();
	}
	
	/// <summary>
	/// Used to initialize the starting spawn points dictionaries.
	/// </summary>
	/// <param name="zoneName"></param>
	/// <param name="spawnPoints"></param>
	private void AddStartingSpawnPoints(string zoneName, [NotNull] IList<Vector3> spawnPoints)
	{
		if (_allStartingSpawnPoints.ContainsKey(zoneName))
		{
			_allStartingSpawnPoints[zoneName].UnionWith(spawnPoints);
			_unusedStartingSpawnPoints[zoneName].UnionWith(spawnPoints);
		}
		else
		{
			HashSet<Vector3> copyOfSpawnPoints = [..spawnPoints];
			_allStartingSpawnPoints.Add(zoneName, copyOfSpawnPoints);
			_unusedStartingSpawnPoints.Add(zoneName, copyOfSpawnPoints);
		}
		
// #if DEBUG
// 		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
// 		foreach (KeyValuePair<string, HashSet<Vector3>> kvp in _allStartingSpawnPoints)
// 		{
// 			sb.AppendFormat("{0} - Zone: {1}, Number of Spawn Points: {2}\n", nameof(_allStartingSpawnPoints), kvp.Key,
// 				kvp.Value?.Count);
// 		}
// 		foreach (KeyValuePair<string, HashSet<Vector3>> kvp in _unusedStartingSpawnPoints)
// 		{
// 			sb.AppendFormat("{0} - Zone: {1}, Number of Spawn Points: {2}\n", nameof(_unusedStartingSpawnPoints),
// 				kvp.Key, kvp.Value?.Count);
// 		}
// 		DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(ZoneSpawnPoints), nameof(AddStartingSpawnPoints));
// #endif
	}
	
	[CanBeNull]
	public new List<Vector3> this[[NotNull] string zoneName]
	{
		get => base[zoneName];
		set
		{
			base[zoneName] = value;
			UpdateInternalMappings(zoneName, value);
		}
	}
	
	public new void Add([NotNull] string zoneName, [CanBeNull] List<Vector3> spawnPoints)
	{
		base.Add(zoneName, spawnPoints);
		UpdateInternalMappings(zoneName, spawnPoints);
	}

	public void AppendSpawnPoints([NotNull] string zoneName, [NotNull] List<Vector3> spawnPoints)
	{
		if (!ContainsKey(zoneName))
		{
			Add(zoneName, spawnPoints);
			return;
		}

		if (this[zoneName] == null)
		{
			this[zoneName] = [..spawnPoints];
			return;
		}
		
		this[zoneName].AddRange(spawnPoints);
		UpdateInternalMappings(zoneName, spawnPoints);
	}
	
	/// <summary>
	/// Updates the internal mapping dictionaries.
	/// </summary>
	private void UpdateInternalMappings([NotNull] string zoneName, [CanBeNull] IList<Vector3> spawnPoints)
	{
		// Always map to 'all' keyword
		MapZoneToKeyword(zoneName, KeywordZoneType.All);
		
		// Map zones with 'hotspot' or 'start' in their name to their respective keywords
		if (!IsStartOrHotspotZone(zoneName, out KeywordZoneType keyword)) return;
		MapZoneToKeyword(zoneName, keyword);
		
		// Make sure to initialize/append to starting spawn point dictionaries
		if (keyword == KeywordZoneType.Start && spawnPoints != null && spawnPoints.Count > 0)
		{
			AddStartingSpawnPoints(zoneName, spawnPoints);
		}
	}
	
	/// <summary>
	/// Maps the <see cref="zoneName"/> to the <see cref="keyword"/> in the <see cref="_keywordZoneMappings"/> dictionary.
	/// </summary>
	/// <param name="zoneName">The zone to map to the target keyword.</param>
	/// <param name="keyword">The target keyword.</param>
	private void MapZoneToKeyword([NotNull] string zoneName, KeywordZoneType keyword)
	{
		if (keyword == KeywordZoneType.None)
		{
			return;
		}
		
		if (!_keywordZoneMappings.ContainsKey(keyword))
		{
			_keywordZoneMappings[keyword] = [zoneName];
		}
		else
		{
			_keywordZoneMappings[keyword].Add(zoneName);
		}

// #if DEBUG
// 		using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
// 		foreach (KeyValuePair<KeywordZoneType, HashSet<string>> kvp in _keywordZoneMappings)
// 		{
// 			sb.AppendFormat("Keyword: {0}, Zone Names: ", kvp.Key);
// 			if (kvp.Value == null || kvp.Value.Count == 0)
// 			{
// 				sb.Append("N/A\n");
// 				continue;
// 			}
// 			foreach (string z in kvp.Value)
// 			{
// 				sb.Append(z);
// 				sb.Append(", ");
// 			}
// 			sb.AppendLine();
// 		}
// 		DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(ZoneSpawnPoints), nameof(MapZoneToKeyword));
// #endif
	}
	
	/// <summary>
	/// Check if the zone name matches the keyword zones Donuts uses (all/start/hotspot).
	/// </summary>
	public static bool IsKeywordZone([NotNull] string zoneName, out KeywordZoneType keyword, bool exactMatch = false)
	{
		return IsAllZone(zoneName, out keyword) || IsStartOrHotspotZone(zoneName, out keyword, exactMatch);
	}
	
	public static bool IsAllZone([NotNull] string zoneName, out KeywordZoneType keyword)
	{
		if (zoneName.ToLower() == "all")
		{
			keyword = KeywordZoneType.All;
			return true;
		}

		keyword = KeywordZoneType.None;
		return false;
	}
	
	public static bool IsStartOrHotspotZone([NotNull] string zoneName, out KeywordZoneType keyword, bool exactMatch = false)
	{
		return IsStartZone(zoneName, out keyword, exactMatch) || IsHotspotZone(zoneName, out keyword, exactMatch);
	}
	
	public static bool IsStartZone([NotNull] string zoneName, out KeywordZoneType keyword, bool exactMatch = false)
	{
		bool exactStringMatch = zoneName.ToLower() == "start";
		if ((exactMatch && exactStringMatch) ||
			(!exactMatch && !exactStringMatch && zoneName.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			keyword = KeywordZoneType.Start;
			return true;
		}
		
		keyword = KeywordZoneType.None;
		return false;
	}
	
	public static bool IsHotspotZone([NotNull] string zoneName, out KeywordZoneType keyword, bool exactMatch = false)
	{
		bool exactStringMatch = zoneName.ToLower() == "hotspot";
		if ((exactMatch && exactStringMatch) ||
			(!exactMatch && !exactStringMatch && zoneName.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			keyword = KeywordZoneType.Hotspot;
			return true;
		}
		
		keyword = KeywordZoneType.None;
		return false;
	}
	
	/// <summary>
	/// Validate the zone name is not exact matching Donuts' reserved keywords.
	/// </summary>
	/// <param name="zoneName">The name of the zone to validate.</param>
	/// <param name="filePath">The path to the JSON.</param>
	/// <returns>True if the zone is valid, false otherwise.</returns>
	private static bool ValidateZoneName([NotNull] string zoneName, [NotNull] string filePath)
	{
		// 
		// E.g. You cannot have a zone called 'All' or 'start'
		if (IsKeywordZone(zoneName, out _, exactMatch: true))
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat(
				"Donuts: Invalid zone name \'{0}\'!\nYou cannot use Donuts keywords as zone names in a ZoneSpawnPoints .json!\nPath: \'{1}\'\n",
				zoneName, filePath);
			DonutsPlugin.Logger.NotifyLogError(sb.ToString());
			return false;
		}
		
		return true;
	}
	
	/// <summary>
	/// JsonConverter to convert <see cref="ZoneSpawnPoints"/> to and from JSON.
	/// </summary>
	public class JsonConverter : JsonConverter<ZoneSpawnPoints>
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
				if (ValidateZoneName(zoneName, reader.Path))
				{
					var spawnPoints = value.ToObject<List<Vector3>>();
					zoneSpawnPoints.Add(zoneName, spawnPoints);
				}
			}
			
			return zoneSpawnPoints;
		}
	}
}