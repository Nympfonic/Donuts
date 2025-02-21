using Cysharp.Threading.Tasks;
using Donuts.Spawning.Services;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Threading;
using UnityToolkit.Structures.DependencyInjection;

namespace Donuts.Spawning.Controllers;

public interface IBotDespawnController
{
	void Initialize();
	UniTask DespawnExcessBots(CancellationToken cancellationToken);
}

public class BotDespawnController(DiContainer container) : IBotDespawnController
{
	[NotNull] private readonly List<IBotDespawnService> _despawnServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private bool _despawnProcessOngoing;
	
	public void Initialize()
	{
		var pmcService = container.Resolve<IBotDespawnService>(DonutsRaidManager.PMC_SERVICE_KEY);
		_despawnServices.Add(pmcService);
		
		var scavService = container.Resolve<IBotDespawnService>(DonutsRaidManager.SCAV_SERVICE_KEY);
		_despawnServices.Add(scavService);
	}
	
	public async UniTask DespawnExcessBots(CancellationToken cancellationToken)
	{
		if (_despawnProcessOngoing)
		{
			return;
		}
		
		_despawnProcessOngoing = true;
		
		for (int i = _despawnServices.Count - 1; i >= 0; i--)
		{
			IBotDespawnService service = _despawnServices[i];
			
			await service.DespawnExcessBots(cancellationToken);
			if (cancellationToken.IsCancellationRequested) return;
		}
		
		_despawnProcessOngoing = false;
	}
}