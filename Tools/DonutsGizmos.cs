using Cysharp.Threading.Tasks;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Donuts.Tools;

internal class DonutsGizmos
{
	internal static ConcurrentDictionary<Vector3, GameObject> gizmoMarkers { get; } = new();
	private static readonly IEnumerable<Entry> _emptyEntries = [];
	
	private readonly CancellationToken _cancellationToken;
	
	private readonly StringBuilder _displayedMarkerInfo = new();
	private readonly StringBuilder _previousMarkerInfo = new();
	private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(3);
	private readonly TimeSpan _markerResetTime = TimeSpan.FromSeconds(5);

	private bool _isGizmoEnabled;
	private bool _gizmoUpdateTaskStarted;

	internal DonutsGizmos(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;
	}

	internal void ToggleGizmoDisplay(bool enableGizmos)
	{
		_isGizmoEnabled = enableGizmos;

		if (_isGizmoEnabled && !_gizmoUpdateTaskStarted)
		{
			//RefreshGizmoDisplay();
			//_gizmoUpdateCoroutine = _monoBehaviourRef.StartCoroutine(UpdateGizmoSpheresCoroutine());
			UpdateGizmoSpheres().Forget();
		}
	}

	internal void DisplayMarkerInformation(BifacialTransform playerTransform)
	{
		if (gizmoMarkers.Count == 0) return;

		(var closestSqrMagnitude, GameObject closestMarker) = (float.MaxValue, null);

		foreach (GameObject marker in gizmoMarkers.Values)
		{
			float sqrMagnitude = (marker.transform.position - playerTransform.position).sqrMagnitude;
			if (sqrMagnitude < closestSqrMagnitude)
				(closestSqrMagnitude, closestMarker) = (sqrMagnitude, marker);
		}

		if (closestMarker == null) return;
		
		Vector3 markerPosition = closestMarker.transform.position;
		if (IsShapeVisible(playerTransform, markerPosition))
		{
			UpdateDisplayedMarkerInfo(markerPosition);
		}
	}

	private async UniTaskVoid UpdateGizmoSpheres()
	{
		_gizmoUpdateTaskStarted = true;
		while (_isGizmoEnabled)
		{
			RefreshGizmoDisplay();
			await UniTask.Delay(_updateInterval, cancellationToken: _cancellationToken);
		}
		ClearGizmoMarkers();
		_gizmoUpdateTaskStarted = false;
	}

	private void RefreshGizmoDisplay()
	{
		ClearGizmoMarkers();
		if (!DefaultPluginVars.DebugGizmos.Value)
		{
			return;
		}

		DrawMarkers(fightLocations?.Locations ?? _emptyEntries, Color.green, PrimitiveType.Sphere);
		DrawMarkers(sessionLocations?.Locations ?? _emptyEntries, Color.red, PrimitiveType.Cube);
	}

	private static void ClearGizmoMarkers()
	{
		foreach (GameObject marker in gizmoMarkers.Values)
		{
			Object.Destroy(marker);
		}
		gizmoMarkers.Clear();
	}

	private void DrawMarkers(IEnumerable<Entry> locations, Color color, PrimitiveType primitiveType)
	{
		foreach (Entry hotspot in locations)
		{
			var newPosition = hotspot.Position.ToVector3();

			if (_mapLocation != hotspot.MapName || gizmoMarkers.ContainsKey(newPosition)) continue;
			
			GameObject marker = CreateMarker(newPosition, color, primitiveType, hotspot.MaxDistance);
			gizmoMarkers[newPosition] = marker;
		}
	}

	private static GameObject CreateMarker(Vector3 position, Color color, PrimitiveType primitiveType, float size)
	{
		var marker = GameObject.CreatePrimitive(primitiveType);
		Material material = marker.GetComponent<Renderer>().material;
		material.color = color;
		marker.GetComponent<Collider>().enabled = false;
		marker.transform.position = position;
		marker.transform.localScale = DefaultPluginVars.gizmoRealSize.Value ? new Vector3(size, 3f, size) : Vector3.one;
		return marker;
	}

	private static bool IsShapeVisible(BifacialTransform playerTransform, Vector3 shapePosition)
	{
		Vector3 direction = shapePosition - playerTransform.position;
		return direction.sqrMagnitude <= 10f * 10f && Vector3.Angle(playerTransform.forward, direction) < 20f;
	}

	private void UpdateDisplayedMarkerInfo(Vector3 closestShapePosition)
	{
		Entry closestEntry = GetClosestEntry(closestShapePosition);
		if (closestEntry == null) return;

		_previousMarkerInfo.Clear().Append(_displayedMarkerInfo);

		_displayedMarkerInfo.Clear()
			.AppendLine("Donuts: Marker Info")
			.AppendLine($"GroupNum: {closestEntry.GroupNum}")
			.AppendLine($"Name: {closestEntry.Name}")
			.AppendLine($"SpawnType: {closestEntry.WildSpawnType}")
			.AppendLine($"Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}")
			.AppendLine($"Bot Timer Trigger: {closestEntry.BotTimerTrigger}")
			.AppendLine($"Spawn Chance: {closestEntry.SpawnChance}")
			.AppendLine($"Max Random Number of Bots: {closestEntry.MaxRandomNumBots}")
			.AppendLine($"Max Spawns Before Cooldown: {closestEntry.MaxSpawnsBeforeCoolDown}")
			.AppendLine($"Ignore Timer for First Spawn: {closestEntry.IgnoreTimerFirstSpawn}")
			.AppendLine($"Min Spawn Distance From Player: {closestEntry.MinSpawnDistanceFromPlayer}");

		if (_displayedMarkerInfo.ToString() == _previousMarkerInfo.ToString()) return;
		
		DonutsHelper.DisplayNotification(_displayedMarkerInfo.ToString(), Color.yellow);
		StartResetMarkerInfoCoroutine();
	}

	private void StartResetMarkerInfoCoroutine()
	{
		if (_resetMarkerInfoCoroutine != null)
		{
			_monoBehaviourRef.StopCoroutine(_resetMarkerInfoCoroutine);
		}

		_resetMarkerInfoCoroutine = _monoBehaviourRef.StartCoroutine(ResetMarkerInfoAfterDelay());
	}

	private IEnumerator ResetMarkerInfoAfterDelay()
	{
		yield return _markerResetTime;
		_displayedMarkerInfo.Clear();
		_resetMarkerInfoCoroutine = null;
	}

	[CanBeNull]
	private static Entry GetClosestEntry(Vector3 position)
	{
		(var closestSqrMagnitude, Entry closestEntry) = (float.MaxValue, null);

		foreach (Entry entry in fightLocations.Locations.Concat(sessionLocations.Locations))
		{
			var entryPosition = entry.Position.ToVector3();
			float sqrMagnitude = (entryPosition - position).sqrMagnitude;
			if (sqrMagnitude < closestSqrMagnitude)
			{
				(closestSqrMagnitude, closestEntry) = (sqrMagnitude, entry);
			}
		}
		return closestEntry;
	}
}