using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Bots;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using static Donuts.DefaultPluginVars;

namespace Donuts.Tools;

internal static class EditorFunctions
{
	private static readonly ManualLogSource _logger;
	
	internal static FightLocations FightLocations { get; } = new()
	{
		Locations = []
	};
	internal static FightLocations SessionLocations { get; } = new()
	{
		Locations = []
	};
	
	private static readonly Dictionary<int, List<HotspotTimer>> _groupedHotspotTimers = [];

	static EditorFunctions()
	{
		_logger = BepInEx.Logging.Logger.CreateLogSource(nameof(EditorFunctions));
	}

	internal static void Dispose()
	{
		_groupedHotspotTimers.Clear();
	}

	internal static void DeleteSpawnMarker()
	{
		if (!Singleton<GameWorld>.Instantiated) return;
			
		GameWorld gameWorld = Singleton<GameWorld>.Instance;

		// Need to be able to see it to delete it
		if (!DebugGizmos.Value) return;
		// Temporarily combine fightLocations and sessionLocations to find the closest entry
		List<Entry> combinedLocations = [
			..FightLocations.Locations,
			..SessionLocations.Locations
		];

		// If for some reason its empty already then return
		if (combinedLocations.Count == 0) return;

		(var closestSqrMagnitude, Entry closestEntry) = (float.MaxValue, null);
		foreach (Entry entry in combinedLocations)
		{
			float sqrMagnitude = (entry.Position - ((IPlayer)gameWorld.MainPlayer).Position).sqrMagnitude;
			if (sqrMagnitude < closestSqrMagnitude)
			{
				closestSqrMagnitude = sqrMagnitude;
				closestEntry = entry;
			}
		}

		// Check if the closest entry is null
		if (closestEntry == null)
		{
			const string closestEntryNullMsg = "Donuts: The Spawn Marker could not be deleted because closest entry could not be found";
			DonutsHelper.DisplayNotification(closestEntryNullMsg, Color.gray);
			return;
		}

		if ((closestEntry.Position - ((IPlayer)gameWorld.MainPlayer).Position).sqrMagnitude > 25f)
			return;
			
		// Remove the entry from the list if the distance from the player is less than 5m
		// Check which list the entry is in and remove it from that list
		if (FightLocations.Locations.Count > 0 &&
		    FightLocations.Locations.Contains(closestEntry))
		{
			FightLocations.Locations.Remove(closestEntry);
		}
		else if (SessionLocations.Locations.Count > 0 &&
		    SessionLocations.Locations.Contains(closestEntry))
		{
			SessionLocations.Locations.Remove(closestEntry);
		}

		// Remove the timer if it exists from the list of hotspotTimer in
		// DonutComponent.groupedHotspotTimers[closestEntry.GroupNum]
		if (!_groupedHotspotTimers.TryGetValue(closestEntry.GroupNum, out List<HotspotTimer> timerList))
		{
			_logger.LogDebug("GroupNum does not exist in groupedHotspotTimers.");
		}
		else
		{
			HotspotTimer timer = null;
			foreach (HotspotTimer t in timerList)
			{
				if (t.Hotspot == closestEntry)
				{
					timer = t;
					break;
				}
			}

			if (timer != null)
			{
				timerList.Remove(timer);
			}
			else
			{
				// Handle the case where no timer was found
				_logger.LogDebug("No matching timer found to delete.");
			}
		}

		// Display a message to the player
		using (var sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("Spawn Marker Deleted for {0}\nSpawnType: {1}\nPosition: {2}, {3}, {4}", closestEntry.Name,
				closestEntry.WildSpawnType, closestEntry.Position.x.ToString(CultureInfo.InvariantCulture),
				closestEntry.Position.y.ToString(CultureInfo.InvariantCulture),
				closestEntry.Position.z.ToString(CultureInfo.InvariantCulture));
			DonutsHelper.DisplayNotification(sb.ToString(), Color.yellow);
		}

		// Edit the DonutComponent.drawnCoordinates and gizmoSpheres list to remove the objects
		if (DonutsGizmos.GizmoMarkers.TryRemove(closestEntry.Position, out GameObject sphere))
		{
			Object.Destroy(sphere);
		}
	}

	internal static void CreateSpawnMarker()
	{
		// Check if any of the required objects are null
		DonutsRaidManager raidManager = MonoBehaviourSingleton<DonutsRaidManager>.Instance;
		if (!Singleton<GameWorld>.Instantiated ||
			raidManager == null ||
			raidManager.BotConfigService == null)
		{
			_logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
			return;
		}
			
		GameWorld gameWorld = Singleton<GameWorld>.Instance;
		IPlayer mainPlayer = gameWorld.MainPlayer;

		// Create new Donuts.Entry
		Entry newEntry = new()
		{
			Name = spawnName.Value,
			GroupNum = groupNum.Value,
			MapName = raidManager.BotConfigService.GetMapLocation(),
			WildSpawnType = wildSpawns.Value,
			MinDistance = minSpawnDist.Value,
			MaxDistance = maxSpawnDist.Value,
			MaxRandomNumBots = maxRandNumBots.Value,
			SpawnChance = spawnChance.Value,
			BotTimerTrigger = botTimerTrigger.Value,
			BotTriggerDistance = botTriggerDistance.Value,
			Position = new Position
			{
				x = mainPlayer.Position.x,
				y = mainPlayer.Position.y,
				z = mainPlayer.Position.z
			},
			MaxSpawnsBeforeCoolDown = maxSpawnsBeforeCooldown.Value,
			IgnoreTimerFirstSpawn = ignoreTimerFirstSpawn.Value,
			MinSpawnDistanceFromPlayer = minSpawnDistanceFromPlayer.Value
		};

		// Add new entry to sessionLocations.Locations list since we're adding new ones
		SessionLocations.Locations.Add(newEntry);

		// Make it testable immediately by adding the timer needed to the groupnum in DonutComponent.groupedHotspotTimers
		if (!_groupedHotspotTimers.ContainsKey(newEntry.GroupNum))
		{
			// Create a new list for the groupnum and add the timer to it
			_groupedHotspotTimers.Add(newEntry.GroupNum, []);
		}

		// Create a new timer for the entry and add it to the list
		var timer = new HotspotTimer(newEntry);
		_groupedHotspotTimers[newEntry.GroupNum].Add(timer);

		using var sb = ZString.CreateUtf8StringBuilder();
		sb.AppendFormat("Donuts: Wrote Entry for {0}\nSpawnType: {1}\nPosition: {2}, {3}, {4}", newEntry.Name,
			newEntry.WildSpawnType, newEntry.Position.x.ToString(CultureInfo.InvariantCulture),
			newEntry.Position.y.ToString(CultureInfo.InvariantCulture),
			newEntry.Position.z.ToString(CultureInfo.InvariantCulture));
		DonutsHelper.DisplayNotification(sb.ToString(), Color.yellow);
	}

	internal static async UniTask WriteToJsonFile(string directoryPath)
	{
		DonutsRaidManager raidManager = MonoBehaviourSingleton<DonutsRaidManager>.Instance;
		if (!Singleton<GameWorld>.Instantiated ||
			raidManager == null ||
			raidManager.BotConfigService == null)
		{
			return;
		}

		string json;
		string fileName;
		// Check if saveNewFileOnly is true then we use the sessionLocations object to serialize, otherwise we use combinedLocations
		if (saveNewFileOnly.Value)
		{
			// Take the sessionLocations object only and serialize it to json
			json = JsonConvert.SerializeObject(SessionLocations, Formatting.Indented);
			fileName = string.Format("{0}_{1}_NewLocOnly.json",
				raidManager.BotConfigService.GetMapLocation(),
				Random.Range(0, 1000).ToString());
		}
		else
		{
			// Combine the fightLocations and sessionLocations objects into one variable
			FightLocations combinedLocations = new()
			{
				Locations = [..FightLocations.Locations, ..SessionLocations.Locations]
			};

			json = JsonConvert.SerializeObject(combinedLocations, Formatting.Indented);
			fileName = string.Format("{0}_{1}_All.json",
				raidManager.BotConfigService.GetMapLocation(),
				Random.Range(0, 1000).ToString());
		}

		// Write json to file with filename == Donuts.DonutComponent.mapLocation + random number
		string jsonFilePath = Path.Combine(directoryPath, "patterns", fileName);

		await UniTask.SwitchToThreadPool();
		using (var writer = new StreamWriter(jsonFilePath, false))
		{
			await writer.WriteAsync(json);
		}
		await UniTask.SwitchToMainThread();

		DonutsHelper.DisplayNotification($"Donuts: Wrote Json File to: {jsonFilePath}", Color.yellow);
	}
}