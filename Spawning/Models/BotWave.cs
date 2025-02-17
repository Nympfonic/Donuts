using Newtonsoft.Json;
using System;

namespace Donuts.Spawning.Models;

[JsonObject]
public class BotWave
{
	private float _waveTimer;
	private float _cooldownTimer;
	private bool _onCooldown;
	private int _timesSpawned;
	
	[JsonProperty("GroupNum")]
	public int GroupNum { get; private set; }
	
	[JsonProperty("TriggerTimer")]
	public int TriggerTimer { get; private set; }
	
	[JsonProperty("TriggerDistance")]
	public int TriggerDistance { get; private set; }
	
	[JsonProperty("SpawnChance")]
	public int SpawnChance { get; private set; }
	
	[JsonProperty("MaxTriggersBeforeCooldown")]
	public int MaxTriggersBeforeCooldown { get; private set; }
	
	[JsonProperty("IgnoreTimerFirstSpawn")]
	public bool IgnoreTimerFirstSpawn { get; private set; }
	
	[JsonProperty("MinGroupSize")]
	public int MinGroupSize { get; private set; }
	
	[JsonProperty("MaxGroupSize")]
	public int MaxGroupSize { get; private set; }
	
	[JsonProperty("Zones")]
	public string[] Zones { get; private set; }
	
	public void UpdateTimer(float deltaTime, float coolDownDuration)
	{
		_waveTimer += deltaTime;
		if (!_onCooldown) return;
		
		_cooldownTimer += deltaTime;
		
		if (_cooldownTimer >= coolDownDuration)
		{
			_onCooldown = false;
			_cooldownTimer = 0f;
			_timesSpawned = 0;
		}
	}
	
	public bool ShouldSpawn()
	{
		if (_onCooldown)
		{
			return false;
		}
		
		if (IgnoreTimerFirstSpawn)
		{
			IgnoreTimerFirstSpawn = false; // Ensure this is only true for the first spawn
			return true;
		}
		
		return _waveTimer >= TriggerTimer;
	}
	
	public void SpawnTriggered()
	{
		_timesSpawned++;
		if (_timesSpawned >= MaxTriggersBeforeCooldown)
		{
			TriggerCooldown();
		}
	}
	
	private void TriggerCooldown()
	{
		_onCooldown = true;
		_cooldownTimer = 0f;
	}
	
	public void ResetTimer()
	{
		_waveTimer = 0f;
	}
	
	public void SetSpawnChance(int newChance)
	{
		if (newChance is < 0 or > 100)
		{
			throw new ArgumentOutOfRangeException(nameof(newChance), newChance, "Must be between 0 and 100.");
		}
		
		SpawnChance = newChance;
	}
}