using Donuts.Spawning.Models;
using Donuts.Utils;
using UnityEngine;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Spawning.Processors;

public class PlayerCombatStateCheck : WaveSpawnProcessorBase
{
	public readonly struct ResetTimerEvent : IEvent;
	
	private float _lastTimePlayerHit;
	
	public PlayerCombatStateCheck()
	{
		var binding = new EventBinding<ResetTimerEvent>(ResetTimer);
		EventBus<ResetTimerEvent>.Register(binding);
	}
	
	public override bool Process(BotWave data)
	{
		if (!IsPlayerInCombat())
		{
			return base.Process(data);
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogDebugDetailed(
				$"Resetting timer for GroupNum {data.GroupNum.ToString()}, reason: A player is in combat.",
				nameof(PlayerCombatStateCheck), nameof(Process));
		}
		
		return false;
	}
	
	private bool IsPlayerInCombat()
	{
		return Time.time < _lastTimePlayerHit + DefaultPluginVars.battleStateCoolDown.Value;
	}
	
	private void ResetTimer()
	{
		if (DefaultPluginVars.debugLogging.Value)
		{
			DonutsRaidManager.Logger.LogDebugDetailed("A player was hit, resetting combat state timer",
				nameof(PlayerCombatStateCheck), nameof(ResetTimer));
		}
		
		_lastTimePlayerHit = Time.time;
	}
}