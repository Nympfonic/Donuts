using UnityEngine;

namespace Donuts.Models;

// TODO: Currently unused - implement Hotspot timer into Donuts logic
public class HotspotTimer(Entry hotspot)
{
	private float _timer = 0f;
	private float _cooldownTimer = 0f;

	public Entry Hotspot => hotspot;
	public bool OnCooldown { get; private set; } = false;
	public int TimesSpawned { get; private set; } = 0;

	public void UpdateTimer()
	{
		_timer += Time.deltaTime;
		if (OnCooldown)
		{
			_cooldownTimer += Time.deltaTime;
			if (_cooldownTimer >= DefaultPluginVars.coolDownTimer.Value)
			{
				OnCooldown = false;
				_cooldownTimer = 0f;
				TimesSpawned = 0;
			}
		}
	}

	public float GetTimer() => _timer;

	public bool ShouldSpawn()
	{
		if (OnCooldown)
		{
			return false;
		}

		if (hotspot.IgnoreTimerFirstSpawn)
		{
			hotspot.IgnoreTimerFirstSpawn = false; // Ensure this is only true for the first spawn
			return true;
		}

		return _timer >= hotspot.BotTimerTrigger;
	}

	public void ResetTimer() => _timer = 0f;

	public void TriggerCooldown()
	{
		OnCooldown = true;
		_cooldownTimer = 0f;
	}
}