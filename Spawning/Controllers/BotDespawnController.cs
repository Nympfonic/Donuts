using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityToolkit.Structures.DependencyInjection;

namespace Donuts.Spawning.Controllers;

public interface IBotDespawnController
{
	void Initialize();
	void DespawnExcessBots();
}

public class BotDespawnController(DiContainer container) : IBotDespawnController
{
	[NotNull] private readonly List<IBotDespawnService> _despawnServices = new(DonutsRaidManager.INITIAL_SERVICES_COUNT);
	
	private bool _initialized;
	
	public void Initialize()
	{
		if (_initialized)
		{
			return;
		}
		
		try
		{
			ResolveServices();
		}
		catch (OperationCanceledException) {}
		catch (Exception ex)
		{
			DonutsRaidManager.Logger.LogException(nameof(BotDespawnController), nameof(Initialize), ex);
		}
		
		if (_despawnServices.Count == 0)
		{
			return;
		}
		
		_initialized = true;
	}
	
	private void ResolveServices()
	{
		string forceAllBotType = DefaultPluginVars.forceAllBotType.Value;

		if (forceAllBotType is "PMC" or "Disabled")
		{
			var pmcService = container.Resolve<IBotDespawnService>(DonutsRaidManager.PMC_SERVICE_KEY);
			if (!_despawnServices.Contains(pmcService))
			{
				_despawnServices.Add(pmcService);
			}
		}

		if (forceAllBotType is "SCAV" or "Disabled")
		{
			var scavService = container.Resolve<IBotDespawnService>(DonutsRaidManager.SCAV_SERVICE_KEY);
			if (!_despawnServices.Contains(scavService))
			{
				_despawnServices.Add(scavService);
			}
		}
	}
	
	public void DespawnExcessBots()
	{
		if (!_initialized)
		{
			return;
		}
		
		for (int i = _despawnServices.Count - 1; i >= 0; i--)
		{
			IBotDespawnService service = _despawnServices[i];
			service.DespawnExcessBots();
		}
	}
}