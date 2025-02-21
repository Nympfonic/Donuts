using Cysharp.Threading.Tasks;
using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityToolkit.Structures.DependencyInjection;

namespace Donuts.Spawning.Controllers;

public interface IBotSpawnController
{
	void Initialize();
	UniTask SpawnStartingBots(CancellationToken cancellationToken);
	UniTask SpawnBotWaves(CancellationToken cancellationToken);
}

public class BotSpawnController([NotNull] DiContainer container) : IBotSpawnController
{
	[NotNull] private readonly List<IBotSpawnService> _spawnServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private const int MS_DELAY_BETWEEN_SPAWNS = 500;
	private const float SPAWN_INTERVAL_SECONDS = 1f;
	
	private bool _initialized;
	
	private bool _hasSpawnedStartingBots;
	private bool _startingSpawnOngoing;
	private float _startingSpawnPrevTime;
	
	private bool _waveSpawnOngoing;
	private float _waveSpawnPrevTime;
	
	public void Initialize()
	{
		if (_initialized)
		{
			return;
		}
		
		var pmcService = container.Resolve<IBotSpawnService>(DonutsRaidManager.PMC_SERVICE_KEY);
		_spawnServices.Add(pmcService);
		
		var scavService = container.Resolve<IBotSpawnService>(DonutsRaidManager.SCAV_SERVICE_KEY);
		_spawnServices.Add(scavService);
		
		_initialized = true;
	}
	
	public async UniTask SpawnStartingBots(CancellationToken cancellationToken)
	{
		if (_hasSpawnedStartingBots || _startingSpawnOngoing || Time.time < _startingSpawnPrevTime + SPAWN_INTERVAL_SECONDS)
		{
			return;
		}
		
		_startingSpawnPrevTime = Time.time;
		_startingSpawnOngoing = true;
		_hasSpawnedStartingBots = true;
		
		for (int i = _spawnServices.Count - 1; i >= 0; i--)
		{
			IBotSpawnService service = _spawnServices[i];
			
			try
			{
				if (!await service.SpawnStartingBots(cancellationToken))
				{
					_hasSpawnedStartingBots = false;
				}
				
				if (cancellationToken.IsCancellationRequested) return;
			}
			catch (OperationCanceledException) {}
			catch (Exception ex)
			{
				DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(SpawnStartingBots), ex);
			}
		}
		
		_startingSpawnOngoing = false;
	}
	
	public async UniTask SpawnBotWaves(CancellationToken cancellationToken)
	{
		if (_waveSpawnOngoing || Time.time < _waveSpawnPrevTime + SPAWN_INTERVAL_SECONDS)
		{
			return;
		}
		
		_waveSpawnPrevTime = Time.time;
		_waveSpawnOngoing = true;
		
		// Spawn all queued bot waves
		var hasWavesToSpawn = true;
		while (hasWavesToSpawn && !cancellationToken.IsCancellationRequested)
		{
			hasWavesToSpawn = false;
			
			for (int i = _spawnServices.Count - 1; i >= 0; i--)
			{
				if (cancellationToken.IsCancellationRequested) return;
				IBotSpawnService service = _spawnServices[i];
				
				try
				{
					if (!await service.TrySpawnBotWave(cancellationToken))
					{
						continue;
					}
				}
				catch (OperationCanceledException) {}
				catch (Exception ex)
				{
					DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(SpawnBotWaves), ex);
				}
				
				hasWavesToSpawn = true;
				
				if (i > 0)
				{
					await UniTask.Delay(MS_DELAY_BETWEEN_SPAWNS, cancellationToken: cancellationToken);
					if (cancellationToken.IsCancellationRequested) return;
				}
			}
			
			if (hasWavesToSpawn)
			{
				await UniTask.Delay(MS_DELAY_BETWEEN_SPAWNS, cancellationToken: cancellationToken);
				if (cancellationToken.IsCancellationRequested) return;
			}
		}
		
		_waveSpawnOngoing = false;
	}
}