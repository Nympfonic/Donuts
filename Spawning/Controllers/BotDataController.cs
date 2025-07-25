using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Spawning.Models;
using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityToolkit.Structures.DependencyInjection;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Spawning.Controllers;

public interface IBotDataController : IDisposable
{
	UniTask<bool> Initialize(CancellationToken cancellationToken);
	UniTask ReplenishBotCache(CancellationToken cancellationToken);
}

public class BotDataController(DiContainer container) : IBotDataController
{
	[NotNull] private readonly List<IBotDataService> _dataServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private bool _initialized;
	private bool _replenishBotCacheOngoing;
	
	void IDisposable.Dispose()
	{
		foreach (IBotDataService service in _dataServices)
		{
			service.Dispose();
		}
	}
	
	public async UniTask<bool> Initialize(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return true;
		}
		
		try
		{
			await ResolveServices(cancellationToken);
		}
		catch (OperationCanceledException) {}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(BotDataController), nameof(Initialize), ex);
		}
		
		if (_dataServices.Count == 0)
		{
			return false;
		}
		
		_initialized = true;
		return true;
	}
	
	private async UniTask ResolveServices(CancellationToken cancellationToken)
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;
		
		if (forceAllBotType is "PMC" or "Disabled")
		{
			var pmcService = container.Resolve<IBotDataService>(DonutsRaidManager.PMC_SERVICE_KEY);
			await SetupDataService(pmcService, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			var scavService = container.Resolve<IBotDataService>(DonutsRaidManager.SCAV_SERVICE_KEY);
			await SetupDataService(scavService, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
		}
	}
	
	private async UniTask SetupDataService(IBotDataService dataService, CancellationToken cancellationToken)
	{
		if (!_dataServices.Contains(dataService))
		{
			_dataServices.Add(dataService);
			await ProcessStartingBotGeneration(dataService, cancellationToken);
		}
	}
	
	private static async UniTask ProcessStartingBotGeneration(IBotDataService dataService, CancellationToken cancellationToken)
	{
		string spawnTypeString = dataService.SpawnType.LocalizedPlural();
		var message = $"Donuts: Generating {spawnTypeString}...";
		
		EventBus.Raise(BotGenStatusChangeEvent.Create(message, progress: 0));
		
		while (true)
		{
			bool finishedGeneration = await GenerateStartingBots(message, spawnTypeString, dataService, cancellationToken);
			if (finishedGeneration)
			{
				break;
			}
		}
		
		EventBus.Raise(BotGenStatusChangeEvent.Create(message, progress: 1));
		await UniTask.Delay(1500, cancellationToken: cancellationToken);
	}
	
	private static async UniTask<bool> GenerateStartingBots(
		string statusMessage,
		string spawnType,
		IBotDataService dataService,
		CancellationToken cancellationToken)
	{
		IUniTaskAsyncEnumerator<BotGenerationProgress> enumerator = dataService
			.CreateStartingBotGenerationStream(cancellationToken)
			.GetAsyncEnumerator(cancellationToken);
		
		try
		{
			while (await enumerator.MoveNextAsync())
			{
				BotGenerationProgress generationProgress = enumerator.Current;
				EventBus.Raise(BotGenStatusChangeEvent.Create(statusMessage, generationProgress.Progress));
			}
			
			return true;
		}
		catch (OperationCanceledException) {}
		catch (TimeoutException)
		{
			DonutsRaidManager.Logger.LogError(
				"Timed out while requesting bot profile data! Check your server log for bot generation errors!");
			
			for (var i = 3; i > 0; i--)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Donuts: Generating {0} timed out! Skipping in {1}...", spawnType, i);
				EventBus.Raise(BotGenStatusChangeEvent.Create(sb.ToString()));
				await UniTask.Delay(1000, cancellationToken: cancellationToken);
			}
		}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(SetupDataService), ex);
		}
		finally
		{
			await enumerator.DisposeAsync();
		}
		
		return false;
	}
	
	public async UniTask ReplenishBotCache(CancellationToken cancellationToken)
	{
		if (!_initialized || _replenishBotCacheOngoing)
		{
			return;
		}
		
		_replenishBotCacheOngoing = true;
			
		for (int i = _dataServices.Count - 1; i >= 0; i--)
		{
			IBotDataService service = _dataServices[i];
			try
			{
				await service.ReplenishBotCache(cancellationToken);
			}
			catch (OperationCanceledException) {}
			catch (Exception ex)
			{
				DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(ReplenishBotCache), ex);
			}
		}
		
		_replenishBotCacheOngoing = false;
		
	}
}