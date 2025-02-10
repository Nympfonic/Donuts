using Donuts.Models;
using UnityEngine;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Bots.Processors;

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
		return !IsPlayerInCombat() && base.Process(data);
	}
	
	private bool IsPlayerInCombat()
	{
		return Time.time < _lastTimePlayerHit + DefaultPluginVars.battleStateCoolDown.Value;
	}
	
	private void ResetTimer()
	{
		_lastTimePlayerHit = Time.time;
	}
}