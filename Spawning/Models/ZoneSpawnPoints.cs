using Cysharp.Text;
using Donuts.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Donuts.Spawning.Models;

/// <summary>
/// ZoneSpawnPoints is a dictionary that stores all the zone spawn points, with an internal keyword-to-zone mapping dictionary.
/// </summary>
[Serializable]
public class ZoneSpawnPoints : Dictionary<string, HashSet<Vector3>>
{
	public enum KeywordZoneType
	{
		None,
		All,
		Start,
		Hotspot
	}
	
	private Dictionary<KeywordZoneType, HashSet<string>> _keywordZoneMappings = [];
	
	/// <summary>
	/// Clear all data within ZoneSpawnPoints. Mainly for deserializing into an existing ZoneSpawnPoints object.
	/// </summary>
	public new void Clear()
	{
		_keywordZoneMappings.Clear();
		base.Clear();
	}
	
	/// <summary>
	/// Gets an array of spawn points for all zones that are mapped to the specified keyword.
	/// </summary>
	/// <param name="keyword">The keyword to filter for zones.</param>
	[CanBeNull]
	public KeyValuePair<string, HashSet<Vector3>>[] GetSpawnPointsFromKeyword(KeywordZoneType keyword)
	{
		if (keyword == KeywordZoneType.None || !_keywordZoneMappings.TryGetValue(keyword, out HashSet<string> zoneNames))
		{
			return null;
		}
		
		var index = 0;
		var array = new KeyValuePair<string, HashSet<Vector3>>[zoneNames.Count];
		foreach (string zoneName in zoneNames)
		{
			array[index++] = new KeyValuePair<string, HashSet<Vector3>>(zoneName, this[zoneName]);
		}
		
		return array;
	}
	
	public bool TryGetKeywordZoneMappings([NotNull] string zoneName, [CanBeNull] out HashSet<string> keywordZoneMappings)
	{
		if (!IsKeywordZone(zoneName, out KeywordZoneType keyword, exactMatch: true))
		{
			keywordZoneMappings = null;
			return false;
		}
		
		if (_keywordZoneMappings.TryGetValue(keyword, out keywordZoneMappings))
		{
			return true;
		}
		
		// Failsafe: Is a keyword zone but could not find it in the keyword mapping dictionary
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Donuts: Keyword {0} not found in the {1} dictionary", keyword.ToString(), nameof(_keywordZoneMappings));
			DonutsPlugin.Logger.LogDebugDetailed(sb.ToString(), nameof(ZoneSpawnPoints), nameof(TryGetKeywordZoneMappings));
		}
		
		return false;
	}
	
	[CanBeNull]
	public new HashSet<Vector3> this[[NotNull] string zoneName]
	{
		get => base[zoneName];
		set
		{
			base[zoneName] = value;
			UpdateInternalMappings(zoneName);
		}
	}
	
	public new void Add([NotNull] string zoneName, [CanBeNull] HashSet<Vector3> spawnPoints)
	{
		base.Add(zoneName, spawnPoints);
		UpdateInternalMappings(zoneName);
	}

	public void AppendSpawnPoints([NotNull] string zoneName, [NotNull] HashSet<Vector3> spawnPoints)
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
		
		this[zoneName].UnionWith(spawnPoints);
		UpdateInternalMappings(zoneName);
	}
	
	/// <summary>
	/// Updates the internal mapping dictionaries.
	/// </summary>
	private void UpdateInternalMappings([NotNull] string zoneName)
	{
		// Always map to 'all' keyword
		MapZoneToKeyword(zoneName, KeywordZoneType.All);
		
		// Map zones with 'hotspot' or 'start' in their name to their respective keywords
		if (IsStartOrHotspotZone(zoneName, out KeywordZoneType keyword))
		{
			MapZoneToKeyword(zoneName, keyword);
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
	/// <returns>True if the zone name is valid, false otherwise.</returns>
	private static bool ValidateZoneName([NotNull] string zoneName, [NotNull] string filePath)
	{
		// E.g. You cannot have a zone called 'all', 'start' or 'hotspot'
		if (IsKeywordZone(zoneName, out _, exactMatch: true))
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat(
				"Donuts: Invalid zone name \'{0}\'!\nYou cannot use Donuts keywords as zone names in a ZoneSpawnPoints .json!\nPath: \'{1}\'\n",
				zoneName, filePath);
			DonutsHelper.NotifyLogError(sb.ToString());
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
			
			foreach (KeyValuePair<string, HashSet<Vector3>> zoneSpawnPoint in value)
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
					var spawnPoints = value.ToObject<HashSet<Vector3>>();
					zoneSpawnPoints.Add(zoneName, spawnPoints);
				}
			}
			
			return zoneSpawnPoints;
		}
	}
}