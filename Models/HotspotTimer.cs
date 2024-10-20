using UnityEngine;

namespace Donuts.Models
{
	public class HotspotTimer(Entry hotspot)
	{
		private float timer = 0f;
		private float cooldownTimer = 0f;

		public Entry Hotspot => hotspot;
		public bool OnCooldown { get; private set; } = false;
		public int TimesSpawned { get; private set; } = 0;

		public void UpdateTimer()
		{
			timer += Time.deltaTime;
			if (OnCooldown)
			{
				cooldownTimer += Time.deltaTime;
				if (cooldownTimer >= DefaultPluginVars.coolDownTimer.Value)
				{
					OnCooldown = false;
					cooldownTimer = 0f;
					TimesSpawned = 0;
				}
			}
		}

		public float GetTimer() => timer;

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

			return timer >= hotspot.BotTimerTrigger;
		}

		public void ResetTimer() => timer = 0f;

		public void TriggerCooldown()
		{
			OnCooldown = true;
			cooldownTimer = 0f;
		}
	}
}
