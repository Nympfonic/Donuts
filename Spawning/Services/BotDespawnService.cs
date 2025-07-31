using Comfort.Common;
using Cysharp.Text;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Systems.Effects;
using UnityEngine;
using UnityToolkit.Extensions;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Spawning.Services;

public interface IBotDespawnService : IServiceSpawnType
{
	void DespawnExcessBots();
}

public abstract class BotDespawnService : IBotDespawnService
{
	private readonly GameWorld _gameWorld = Singleton<GameWorld>.Instance;
	private readonly EffectsCommutator _effectsCommutator = Singleton<Effects>.Instance.EffectsCommutator;
	private readonly BotsController _botsController = Singleton<IBotGame>.Instance.BotsController;
	private readonly FurthestBotComparer _furthestBotComparer = new();
	private readonly List<BotOwner> _aliveBots = [];
	
	private const int MIN_BOT_ACTIVE_TIME_SECONDS = 3;
	
	private readonly List<Player> _botsToDespawn = new(20);
	private float _despawnCooldownTime;
	
	private readonly BotConfigService _configService;
	private readonly IBotDataService _dataService;
	
	protected BotDespawnService(BotConfigService configService, IBotDataService dataService)
	{
		_configService = configService;
		_dataService = dataService;
		
		var registerBotBinding = new EventBinding<RegisterBotEvent>(RegisterBot);
		EventBus.Register(registerBotBinding);
		
		var playerEnteredCombatBinding = new EventBinding<PlayerEnteredCombatEvent>(ResetDespawnTimer);
		EventBus.Register(playerEnteredCombatBinding);
	}
	
	public abstract DonutsSpawnType SpawnType { get; }

	protected abstract bool IsDespawnBotEnabled();
	
	public void DespawnExcessBots()
	{
		if (!IsDespawnBotEnabled()) return;
		
		float timeSinceLastDespawn = Time.time - _despawnCooldownTime;
		bool hasReachedTimeToDespawn = timeSinceLastDespawn >= DefaultPluginVars.despawnInterval.Value;
		if (!hasReachedTimeToDespawn || !IsOverBotLimit(out int excessBots) || excessBots <= 0)
		{
			return;
		}
		
		_despawnCooldownTime = Time.time;
		
		IReadOnlyList<Player> furthestBots = FindFurthestBots();
		int furthestBotsCount = furthestBots.Count;
		excessBots = Mathf.Min(furthestBotsCount, excessBots);
		
		var despawnedCount = 0;
		var pointer = 0;
		while (despawnedCount < excessBots && pointer < furthestBotsCount)
		{
			Player furthestBot = furthestBots[pointer];
			if (furthestBot.OrNull()?.IsAlive() == true && TryDespawnBot(furthestBot))
			{
				despawnedCount++;
			}
			
			pointer++;
		}
	}
	
	private bool IsOverBotLimit(out int excess)
	{
		int aliveBots = _dataService.GetAliveBotsCount();
		int botLimit = _dataService.MaxBotLimit;
		
		if (aliveBots <= botLimit)
		{
			excess = 0;
			return false;
		}
		
		excess = aliveBots - botLimit;
		return true;
	}
	
	/// <summary>
	/// Finds the furthest bots away from all human players that are ready to despawn.
	/// </summary>
	[NotNull]
	private IReadOnlyList<Player> FindFurthestBots()
	{
		_botsToDespawn.Clear();
		_aliveBots.RemoveAll(b => b == null || !b.GetPlayer.IsAlive());
		foreach (BotOwner bot in _aliveBots)
		{
			if (Time.time >= bot.ActivateTime + MIN_BOT_ACTIVE_TIME_SECONDS)
			{
				_botsToDespawn.Add(bot.GetPlayer);
			}
		}
		
		Vector3 centroid = GetCenterPointBetweenHumanPlayers();
		_furthestBotComparer.Position = centroid;
		_botsToDespawn.Sort(_furthestBotComparer);
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			if (_botsToDespawn.Count == 0)
			{
				sb.Append("No alive bots that can be despawned, aborting despawn process.");
			}
			else
			{
				sb.AppendFormat("{0} alive bots found that can be despawned: \n", _botsToDespawn.Count);
				for (var i = 0; i < _botsToDespawn.Count; i++)
				{
					Player bot = _botsToDespawn[i];
					sb.AppendFormat("Furthest bot #{0}: {1} ({2}), {3}m away from center position {4} between human players",
						i + 1, bot.Profile.Info.Nickname, bot.ProfileId, (centroid - bot.Transform.position).magnitude, centroid);
					if (i < _botsToDespawn.Count - 1)
					{
						sb.Append('\n');
					}
				}
			}
			
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(FindFurthestBots));
		}
		
		return _botsToDespawn;
	}
	
	protected abstract bool IsCorrectSpawnType(WildSpawnType role);
	
	private Vector3 GetCenterPointBetweenHumanPlayers()
	{
		ReadOnlyCollection<Player> humanPlayers = _configService.GetHumanPlayerList();
		int humanCount = humanPlayers.Count;
		Vector3 centroid = Vector3.zero;
		
		if (humanCount == 0)
		{
			return centroid;
		}
		
		for (int i = humanCount - 1; i >= 0; i--)
		{
			Player humanPlayer = humanPlayers[i];
			if (humanPlayer.OrNull()?.IsAlive() == true)
			{
				centroid += humanPlayer.Transform.position;
			}
		}
		
		centroid /= humanCount;
		return centroid;
	}
	
	private bool TryDespawnBot([NotNull] Player furthestBot)
	{
		BotOwner botOwner = furthestBot.AIData?.BotOwner;
		if (botOwner == null)
		{
			return false;
		}
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Despawning bot: {0} ({1})", furthestBot.Profile.Info.Nickname, furthestBot.name);
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryDespawnBot));
		}
		
		// Not sure what exactly fixed the error spam with SAIN but don't change this shit EVER
		Player botPlayer = botOwner.GetPlayer;
		_effectsCommutator.StopBleedingForPlayer(botPlayer);
		// _gameWorld.RegisteredPlayers.Remove(botOwner);
		// _gameWorld.AllAlivePlayersList.Remove(botPlayer);
		_gameWorld.UnregisterPlayer(botOwner);
		_gameWorld.UnregisterPlayer(botPlayer);
		
		botOwner.Deactivate();
		botOwner.Dispose();
		_botsController.BotDied(botOwner);
		_botsController.DestroyInfo(botPlayer);
		
		Object.DestroyImmediate(botOwner.gameObject);
		Object.Destroy(botOwner);
        
		return true;
	}
	
	private void RegisterBot(RegisterBotEvent data)
	{
		WildSpawnType role = data.bot.Profile.Info.Settings.Role;
		if (IsCorrectSpawnType(role) && !_aliveBots.Contains(data.bot))
		{
			_aliveBots.Add(data.bot);
		}
	}

	private void ResetDespawnTimer()
	{
		_despawnCooldownTime = Time.time;
	}
	
	private class FurthestBotComparer : Comparer<Player>
	{
		public Vector3 Position { get; set; }
		
		public override int Compare(Player x, Player y)
		{
			if (x == null || y == null)
			{
				return 0;
			}
			
			float xSqrMagnitude = (x.Transform.position - Position).sqrMagnitude;
			float ySqrMagnitude = (y.Transform.position - Position).sqrMagnitude;
			return xSqrMagnitude.CompareTo(ySqrMagnitude) * -1;
		}
	}
}