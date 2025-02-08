using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Systems.Effects;
using UnityEngine;
using UnityToolkit.Extensions;

namespace Donuts.Bots;

public interface IBotDespawnService
{
	UniTask DespawnExcessBots(CancellationToken cancellationToken);
}

public abstract class BotDespawnService(
	BotConfigService configService,
	IBotDataService dataService) : IBotDespawnService
{
	private readonly FurthestBotComparer _furthestBotComparer = new();
	
	private const int FRAME_DELAY_BETWEEN_DESPAWNS = 20;
	
	private readonly List<Player> _botsToDespawn = new(20);
	private float _despawnCooldownTime;

	protected abstract bool IsDespawnBotEnabled();
	
	public async UniTask DespawnExcessBots(CancellationToken cancellationToken)
	{
		if (!IsDespawnBotEnabled()) return;
		
		float timeSinceLastDespawn = Time.time - _despawnCooldownTime;
		bool hasReachedTimeToDespawn = timeSinceLastDespawn >= DefaultPluginVars.despawnInterval.Value;
		if (!IsOverBotLimit(out int excessBots) || excessBots <= 0 || !hasReachedTimeToDespawn)
		{
			return;
		}
		
		IReadOnlyList<Player> furthestBots = FindFurthestBots();
		for (var i = 0; i < excessBots; i++)
		{
			Player furthestBot = furthestBots[i];
			if (furthestBot == null || furthestBot.HealthController == null || !furthestBot.HealthController.IsAlive)
			{
				continue;
			}
			
			if (TryDespawnBot(furthestBot) && i < excessBots - 1)
			{
				await UniTask.DelayFrame(FRAME_DELAY_BETWEEN_DESPAWNS, cancellationToken: cancellationToken);
			}
		}
	}
	
	private bool IsOverBotLimit(out int excess)
	{
		int aliveBots = dataService.GetAliveBotsCount();
		int botLimit = dataService.MaxBotLimit;
		
		if (aliveBots <= botLimit)
		{
			excess = 0;
			return false;
		}
		
		excess = aliveBots - botLimit;
		return true;
	}
	
	/// <summary>
	/// Finds the furthest bots away from all human players
	/// </summary>
	[NotNull]
	private IReadOnlyList<Player> FindFurthestBots()
	{
		_botsToDespawn.Clear();
		
		ReadOnlyCollection<Player> allAlivePlayers = dataService.AllAlivePlayers;
		for (int i = allAlivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = allAlivePlayers[i];
			// Ignore players that aren't the correct bot spawn type
			if (player.OrNull()?.IsAI == true &&
				player.HealthController?.IsAlive == true &&
				IsCorrectSpawnType(player.Profile.Info.Settings.Role))
			{
				_botsToDespawn.Add(player);
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
				sb.Append("No bots to despawn.");
			}
			else
			{
				sb.AppendFormat("{0} bots to despawn: \n", _botsToDespawn.Count);
				for (var i = 0; i < _botsToDespawn.Count; i++)
				{
					Player bot = _botsToDespawn[i];
					sb.AppendFormat("Furthest bot #{0}: {1}", i, bot.Profile.Info.Nickname);
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
		ReadOnlyCollection<Player> humanPlayers = configService.GetHumanPlayerList();
		int humanCount = humanPlayers.Count;
		Vector3 centroid = Vector3.zero;
		
		for (int i = humanCount - 1; i >= 0; i--)
		{
			Player humanPlayer = humanPlayers[i];
			if (humanPlayer.OrNull()?.HealthController?.IsAlive == true)
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
		if (botOwner == null) return false;
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Despawning bot: {0} ({1})", furthestBot.Profile.Info.Nickname, furthestBot.name);
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), GetType().Name, nameof(TryDespawnBot));
		}
		
		Player botPlayer = botOwner.GetPlayer;
		// _gameWorld.RegisteredPlayers.Remove(botOwner);
		// _gameWorld.AllAlivePlayersList.Remove(botPlayer);
		Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botPlayer);
		
		// TODO: Call Fika's despawn method instead
		if (DonutsPlugin.FikaEnabled)
		{
			botOwner.LeaveData.RemoveFromMap();
		}
		else
		{
			// BSG calls this to despawn; this calls the BotOwner::Deactivate(), BotOwner::Dispose() and IBotGame::BotDespawn() methods
			botOwner.LeaveData.RemoveFromMap();
		}
        
		// Update the cooldown
		_despawnCooldownTime = Time.time;
		return true;
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