using Cysharp.Threading.Tasks;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Donuts.Tools;

internal class DonutsGizmos
{
	internal static ConcurrentDictionary<Vector3, GameObject> GizmoMarkers { get; } = new();
	private static readonly IEnumerable<Entry> _emptyEntries = [];
	
	private readonly CancellationToken _onDestroyToken;
	private readonly CancellationTokenSource _cts = new();
	private CancellationTokenSource _updateMarkerCts;
	private CancellationTokenSource _resetMarkerCts;
	
	private readonly StringBuilder _displayedMarkerInfo = new();
	private readonly StringBuilder _previousMarkerInfo = new();
	private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(3);
	private readonly TimeSpan _markerResetTime = TimeSpan.FromSeconds(5);

	private bool _isGizmoEnabled;
	private bool _gizmoUpdateTaskStarted;
	private bool _willResetMarkerInfo;

	internal DonutsGizmos(CancellationToken onDestroyToken)
	{
		_onDestroyToken = onDestroyToken;
		_updateMarkerCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _onDestroyToken);
		_resetMarkerCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _onDestroyToken);
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
		else if (!_isGizmoEnabled && _gizmoUpdateTaskStarted)
		{
			ResetCts(ref _updateMarkerCts);
		}
	}

	private void ResetCts(ref CancellationTokenSource cts)
	{
		cts.Cancel();
		cts.Dispose();
		cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _onDestroyToken);
	}

	internal void DisplayMarkerInformation(BifacialTransform playerTransform)
	{
		if (GizmoMarkers.Count == 0) return;

		(var closestSqrMagnitude, GameObject closestMarker) = (float.MaxValue, null);

		foreach (GameObject marker in GizmoMarkers.Values)
		{
			float sqrMagnitude = (marker.transform.position - playerTransform.position).sqrMagnitude;
			if (sqrMagnitude < closestSqrMagnitude)
			{
				(closestSqrMagnitude, closestMarker) = (sqrMagnitude, marker);
			}
		}

		if (closestMarker == null) return;
		
		Vector3 markerPosition = closestMarker.transform.position;
		if (IsShapeVisible(playerTransform, markerPosition))
		{
			UpdateDisplayedMarkerInfo(markerPosition);
		}
	}

	internal void Dispose()
	{
		_cts.Cancel();
		_updateMarkerCts.Dispose();
		_resetMarkerCts.Dispose();
		_cts.Dispose();
	}

	private async UniTaskVoid UpdateGizmoSpheres()
	{
		_gizmoUpdateTaskStarted = true;
		while (_isGizmoEnabled)
		{
			RefreshGizmoDisplay();
			await UniTask.Delay(_updateInterval, cancellationToken: _updateMarkerCts.Token, cancelImmediately: true);
		}
		ClearGizmoMarkers();
		_gizmoUpdateTaskStarted = false;
	}

	private void RefreshGizmoDisplay()
	{
		ClearGizmoMarkers();
		if (!DefaultPluginVars.DebugGizmos.Value) return;

		DrawMarkers(EditorFunctions.FightLocations?.Locations ?? _emptyEntries, Color.green, PrimitiveType.Sphere);
		DrawMarkers(EditorFunctions.SessionLocations?.Locations ?? _emptyEntries, Color.red, PrimitiveType.Cube);
	}

	private static void ClearGizmoMarkers()
	{
		foreach (GameObject marker in GizmoMarkers.Values)
		{
			Object.Destroy(marker);
		}
		GizmoMarkers.Clear();
	}

	private void DrawMarkers(IEnumerable<Entry> locations, Color color, PrimitiveType primitiveType)
	{
		foreach (Entry hotspot in locations)
		{
			Position newPosition = hotspot.Position;
			if (MonoBehaviourSingleton<DonutsRaidManager>.Instance.BotConfigService.GetMapLocation() != hotspot.MapName ||
				GizmoMarkers.ContainsKey(newPosition))
			{
				continue;
			}

			GameObject marker = CreateMarker(newPosition, color, primitiveType, hotspot.MaxDistance);
			GizmoMarkers[newPosition] = marker;
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
			.AppendLine($"GroupNum: {closestEntry.GroupNum.ToString()}")
			.AppendLine($"Name: {closestEntry.Name}")
			.AppendLine($"SpawnType: {closestEntry.WildSpawnType}")
			.AppendLine(string.Format("Position: {0}, {1}, {2}",
				closestEntry.Position.x.ToString(CultureInfo.InvariantCulture),
				closestEntry.Position.y.ToString(CultureInfo.InvariantCulture),
				closestEntry.Position.z.ToString(CultureInfo.InvariantCulture)))
			.AppendLine($"Bot Timer Trigger: {closestEntry.BotTimerTrigger.ToString(CultureInfo.InvariantCulture)}")
			.AppendLine($"Spawn Chance: {closestEntry.SpawnChance.ToString()}")
			.AppendLine($"Max Random Number of Bots: {closestEntry.MaxRandomNumBots.ToString()}")
			.AppendLine($"Max Spawns Before Cooldown: {closestEntry.MaxSpawnsBeforeCoolDown.ToString()}")
			.AppendLine($"Ignore Timer for First Spawn: {closestEntry.IgnoreTimerFirstSpawn.ToString()}")
			.AppendLine($"Min Spawn Distance From Player: {closestEntry.MinSpawnDistanceFromPlayer.ToString(CultureInfo.InvariantCulture)}");

		var displayedMarkerString = _displayedMarkerInfo.ToString();
		if (displayedMarkerString == _previousMarkerInfo.ToString()) return;
		
		DonutsHelper.DisplayNotification(displayedMarkerString, Color.yellow);
		
		if (_willResetMarkerInfo)
		{
			ResetCts(ref _resetMarkerCts);
		}
		_willResetMarkerInfo = true;
		ResetMarkerInfo().Forget();
	}

	private async UniTaskVoid ResetMarkerInfo()
	{
		await UniTask.Delay(_markerResetTime, cancellationToken: _resetMarkerCts.Token, cancelImmediately: true);
		_displayedMarkerInfo.Clear();
		_willResetMarkerInfo = false;
	}

	[CanBeNull]
	private static Entry GetClosestEntry(Vector3 position)
	{
		(var closestSqrMagnitude, Entry closestEntry) = (float.MaxValue, null);

		foreach (Entry entry in EditorFunctions.FightLocations.Locations.Concat(EditorFunctions.SessionLocations.Locations))
		{
			float sqrMagnitude = (entry.Position - position).sqrMagnitude;
			if (sqrMagnitude < closestSqrMagnitude)
			{
				(closestSqrMagnitude, closestEntry) = (sqrMagnitude, entry);
			}
		}
		return closestEntry;
	}
}