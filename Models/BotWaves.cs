using System.Collections.Generic;

namespace Donuts.Models
{
	public class BotWave
	{
		public int GroupNum { get; set; }
		public int TriggerTimer { get; set; }
		public int TriggerDistance { get; set; }
		public int SpawnChance { get; set; }
		public int MaxTriggersBeforeCooldown { get; set; }
		public bool IgnoreTimerFirstSpawn { get; set; }
		public int MinGroupSize { get; set; }
		public int MaxGroupSize { get; set; }
		public List<string> Zones { get; set; }

		// Timer properties
		private float timer;
		private float cooldownTimer;
		public bool InCooldown { get; set; }
		public int TimesSpawned { get; set; }

		public BotWave()
		{
			timer = 0f;
			cooldownTimer = 0f;
			InCooldown = false;
			TimesSpawned = 0;
		}

		public void UpdateTimer(float deltaTime, float coolDownDuration)
		{
			timer += deltaTime;
			if (InCooldown)
			{
				cooldownTimer += deltaTime;

				if (cooldownTimer >= coolDownDuration)
				{
					InCooldown = false;
					cooldownTimer = 0f;
					TimesSpawned = 0;
				}
			}
		}

		public bool ShouldSpawn()
		{
			if (InCooldown)
			{
				return false;
			}

			if (IgnoreTimerFirstSpawn)
			{
				IgnoreTimerFirstSpawn = false; // Ensure this is only true for the first spawn
				return true;
			}

			return timer >= TriggerTimer;
		}

		public void ResetTimer()
		{
			timer = 0f;
		}

		public void TriggerCooldown()
		{
			InCooldown = true;
			cooldownTimer = 0f;
		}
	}
}
