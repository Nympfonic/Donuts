using Newtonsoft.Json;
using System.Collections.Generic;

namespace Donuts.Models;

[JsonObject]
public class BotWave
{
	private float _timer;
	private float _cooldownTimer;
	
	[JsonProperty("groupNum")]
	public int GroupNum { get; set; }
	
	[JsonProperty("triggerTimer")]
	public int TriggerTimer { get; set; }
	
	[JsonProperty("triggerDistance")]
	public int TriggerDistance { get; set; }
	
	[JsonProperty("spawnChance")]
	public int SpawnChance { get; set; }
	
	[JsonProperty("maxTriggersBeforeCooldown")]
	public int MaxTriggersBeforeCooldown { get; set; }
	
	[JsonProperty("ignoreTimerFirstSpawn")]
	public bool IgnoreTimerFirstSpawn { get; set; }
	
	[JsonProperty("minGroupSize")]
	public int MinGroupSize { get; set; }
	
	[JsonProperty("maxGroupSize")]
	public int MaxGroupSize { get; set; }
	
	[JsonProperty("zones")]
	public List<string> Zones { get; set; }
	
	[JsonIgnore] public bool OnCooldown { get; private set; }
	[JsonIgnore] public int TimesSpawned { get; private set; }

	public void UpdateTimer(float deltaTime, float coolDownDuration)
	{
		_timer += deltaTime;
		if (!OnCooldown) return;
		
		_cooldownTimer += deltaTime;
		
		if (_cooldownTimer >= coolDownDuration)
		{
			OnCooldown = false;
			_cooldownTimer = 0f;
			TimesSpawned = 0;
		}
	}
	
	public bool ShouldSpawn()
	{
		if (OnCooldown)
		{
			return false;
		}
		
		if (IgnoreTimerFirstSpawn)
		{
			IgnoreTimerFirstSpawn = false; // Ensure this is only true for the first spawn
			return true;
		}
		
		return _timer >= TriggerTimer;
	}
	
	public void SpawnTriggered()
	{
		TimesSpawned++;
		if (TimesSpawned >= MaxTriggersBeforeCooldown)
		{
			TriggerCooldown();
		}
	}
	
	public void ResetTimer()
	{
		_timer = 0f;
	}
	
	private void TriggerCooldown()
	{
		OnCooldown = true;
		_cooldownTimer = 0f;
	}
}