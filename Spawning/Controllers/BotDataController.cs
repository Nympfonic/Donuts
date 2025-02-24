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

public class BotDataController(DiContainer container, TimeoutController timeoutController) : IBotDataController
{
	[NotNull] private readonly List<IBotDataService> _dataServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private bool _initialized;
	
	private bool _replenishBotCacheOngoing;
	
	private static readonly TimeSpan _startingBotsTimeoutSeconds = TimeSpan.FromSeconds(60);
	
	public void Dispose()
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
			if (cancellationToken.IsCancellationRequested) return;
		}
		
		if (forceAllBotType is "SCAV" or "Disabled")
		{
			var scavService = container.Resolve<IBotDataService>(DonutsRaidManager.SCAV_SERVICE_KEY);
			await SetupDataService(scavService, cancellationToken);
			if (cancellationToken.IsCancellationRequested) return;
		}
	}
	
	private async UniTask SetupDataService(IBotDataService dataService, CancellationToken cancellationToken)
	{
		if (_dataServices.Contains(dataService))
		{
			return;
		}
		
		_dataServices.Add(dataService);
		
		string spawnTypeString = dataService.SpawnType.LocalizedPlural();
		var message = $"Donuts: Generating {spawnTypeString}...";
		
		try
		{
			CancellationToken timeoutToken = timeoutController.Timeout(_startingBotsTimeoutSeconds);
			
			IUniTaskAsyncEnumerable<BotGenerationProgress> stream = dataService.SetupStartingBotCache(timeoutToken);
			if (stream == null)
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat("Error creating the starting bot generation stream in {0}!", dataService.GetType().Name);
				DonutsRaidManager.Logger.LogError(sb.ToString());
				return;
			}
			
			EventBus.Raise(BotGenStatusChangeEvent.Create(message, 0));
			
			IUniTaskAsyncEnumerator<BotGenerationProgress> enumerator = stream.GetAsyncEnumerator(timeoutToken);
			while (await enumerator.MoveNextAsync())
			{
				BotGenerationProgress generationProgress = enumerator.Current;
				EventBus.Raise(BotGenStatusChangeEvent.Create(message, generationProgress.Progress));
			}
			await enumerator.DisposeAsync();
			timeoutController.Reset();
			
			EventBus.Raise(BotGenStatusChangeEvent.Create(message, 1));
			
			await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: cancellationToken);
		}
		catch (OperationCanceledException)
		{
			if (timeoutController.IsTimeout())
			{
				using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
				sb.AppendFormat(
					"{0} timed out while generating starting bot profiles! Check your server logs for bot generation errors!",
					dataService.GetType().Name);
				DonutsRaidManager.Logger.LogError(sb.ToString());
				
				TimeSpan oneSecond = TimeSpan.FromSeconds(1);
				
				for (var i = 3; i > 0; i--)
				{
					sb.Clear();
					sb.AppendFormat("Donuts: Generating {0} timed out! Skipping in {1}...", spawnTypeString, i);
					EventBus.Raise(BotGenStatusChangeEvent.Create(sb.ToString()));
					await UniTask.Delay(oneSecond, cancellationToken: cancellationToken);
				}
			}
		}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(DonutsRaidManager), nameof(SetupDataService), ex);
		}
		finally
		{
			timeoutController.Reset();
		}
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