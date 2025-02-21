using Cysharp.Text;
using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Donuts.Spawning.Models;

public class ActivateBotCallbackWrapper([NotNull] BotSpawner botSpawner, [NotNull] BotCreationDataClass botData)
{
	private static readonly FieldInfo _deadBodiesControllerField = AccessTools.Field(typeof(BotSpawner), "_deadBodiesController");
	private static readonly FieldInfo _botsField = AccessTools.Field(typeof(BotSpawner), "_bots");
	private static readonly FieldInfo _allPlayersField = AccessTools.Field(typeof(BotSpawner), "_allPlayers");
	private static readonly FieldInfo _freeForAllField = AccessTools.Field(typeof(BotSpawner), "_freeForAll");
	// (BotOwner bot, BotCreationDataClass data, Action<BotOwner> callback, bool shallBeGroup, Stopwatch stopWatch)
	private static readonly MethodInfo _spawnBotMethod = AccessTools.Method(typeof(BotSpawner), "method_11");
	
	private static readonly Stopwatch _stopwatch = new();
	
	private BotsGroup _group;
	private int _membersCount;
	private DeadBodiesController _deadBodiesController = (DeadBodiesController)_deadBodiesControllerField.GetValue(botSpawner);
	private BotsClass _bots = (BotsClass)_botsField.GetValue(botSpawner);
	private bool _freeForAll = (bool)_freeForAllField.GetValue(botSpawner);
	
	internal static ActivateBotCallbackDelegate ActivateBotDelegate { get; set; }
	
	internal delegate void ActivateBotCallbackDelegate([NotNull] BotOwner botOwner, [NotNull] BotCreationDataClass data,
		[CanBeNull] Action<BotOwner> callback, bool shallBeGroup, [NotNull] Stopwatch stopwatch);
	
	/// <summary>
	/// Invoked when the bot is created. Ensures the bot has its group set.
	/// </summary>
	public void CreateBotCallback([NotNull] BotOwner bot)
	{
		// Create method delegate and cache it
		if (ActivateBotDelegate == null || ActivateBotDelegate.Target != botSpawner)
		{
			ActivateBotDelegate = AccessTools.MethodDelegate<ActivateBotCallbackDelegate>(
				_spawnBotMethod, botSpawner, false);
		}
		
		bool shallBeGroup = botData.SpawnParams?.ShallBeGroup != null;
		
		// BSG wants a stopwatch, we'll give em a stopwatch
		// TODO: transpile patch out the stopwatch
		ActivateBotDelegate(bot, botData, null, shallBeGroup, _stopwatch);
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Successfully spawned {0} \"{1}\" ({2})", bot.name, bot.Profile.Nickname, bot.ProfileId);
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(ActivateBotCallbackWrapper), nameof(CreateBotCallback));
		}
	}
	
	/// <summary>
	/// Invoked when the bot is about to be activated. Sets the bot's group and its list of enemies.
	/// </summary>
	public BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
	{
		// If we haven't created our BotsGroup yet, do so, and then lock it so nobody else can use it
		if (_group == null)
		{
			_group = GetGroupAndSetEnemies_Internal(bot, zone);
		}
		// For the rest of the bots in the same group, check if the bot should be added to other bot groups' allies/enemies list
		// This is normally performed in BotSpawner::GetGroupAndSetEnemies(BotOwner, BotZone)
		else
		{
			botSpawner.method_5(bot);
		}
		
		_membersCount++;
		
		if (DefaultPluginVars.debugLogging.Value)
		{
			using Utf8ValueStringBuilder sb = ZString.CreateUtf8StringBuilder();
			sb.AppendFormat("Group {0} (ID: {1}) - Group size: {2}/{3} - Bot being added: {4} (ID: {5})", _group.Name,
				_group.Id, _membersCount, _group.TargetMembersCount, bot.Profile.Nickname, bot.Id);
			DonutsRaidManager.Logger.LogDebugDetailed(sb.ToString(), nameof(ActivateBotCallbackWrapper),
				nameof(GetGroupAndSetEnemies));
		}
		
		return _group;
	}
	
	/// <summary>
	/// This is very similar to the original <see cref="BotSpawner.GetGroupAndSetEnemies(BotOwner, BotZone)"/> method
	/// but removes checking for an existing bot group within BotSpawner's <see cref="BotZoneGroupsDictionary"/>.
	/// <p>This is because the way BSG finds a botgroup is incompatible with Donuts' spawning logic and only checks
	/// by zone, bot role and bot player side - what if there are multiple bot groups in the same zone sharing the same
	/// bot role and player side?</p>
	/// </summary>
	/// <param name="bot">The bot to add to the group.</param>
	/// <param name="zone">The zone where the bot group is tied to.</param>
	/// <returns>A new bot group</returns>
	private BotsGroup GetGroupAndSetEnemies_Internal(BotOwner bot, BotZone zone)
	{
		bool isBossOrFollower = bot.Profile.Info.Settings.IsBossOrFollower();
		EPlayerSide side = bot.Profile.Side;
		WildSpawnType role = bot.Profile.Info.Settings.Role;
		
		// if (isBossOrFollower &&
		// 	botSpawner.Groups.TryGetValue(zone, side, role, out BotsGroup bossBotsGroup, isBossOrFollower: true) &&
		// 	(bot.SpawnProfileData?.SpawnParams.ShallBeGroup == null || (!bot.Boss.IamBoss && !bossBotsGroup.IsFull)))
		// {
		// 	botSpawner.method_5(bot);
		// 	return bossBotsGroup;
		// }
		
		// Get a list of this bot's enemies
		List<BotOwner> enemies = GetEnemies(bot);
		// Check and add bot to other groups' allies or enemies list
		botSpawner.method_5(bot);
		
		var allPlayers = (List<Player>)_allPlayersField.GetValue(botSpawner);
		var botsGroup = new BotsGroup(zone, botSpawner.BotGame, bot, enemies, _deadBodiesController, allPlayers,
			forBoss: isBossOrFollower);
		
		if (bot.SpawnProfileData.SpawnParams?.ShallBeGroup != null)
		{
			botsGroup.TargetMembersCount = bot.SpawnProfileData.SpawnParams.ShallBeGroup.StartCount;
		}
		
		if (isBossOrFollower)
		{
			botSpawner.Groups.Add(zone, side, botsGroup, isBossOrFollower: true);
		}
		else
		{
			botSpawner.Groups.AddNoKey(botsGroup, zone);
			// if (_freeForAll.HasValue && _freeForAll.Value)
			// {
			// 	botSpawner.Groups.AddNoKey(botsGroup, zone);
			// }
			// else
			// {
			// 	botSpawner.Groups.Add(zone, side, botsGroup, isBossOrFollower: false);
			// }
		}
		
		botsGroup.Lock();
		return botsGroup;
	}
	
	private List<BotOwner> GetEnemies(BotOwner owner)
	{
		if (_freeForAll)
		{
			return _bots.BotOwners.ToList();
		}
		
		var enemies = new List<BotOwner>(20);
		foreach (BotOwner botToCheck in _bots.BotOwners)
		{
			WildSpawnType role = botToCheck.Profile.Info.Settings.Role;
			if (owner.Settings.IsEnemyByChance(botToCheck))
			{
				enemies.Add(botToCheck);
				continue;
			}
			
			if (owner.Settings.GetFriendlyBotTypes().Contains(role) ||
				owner.Settings.GetWarnBotTypes().Contains(role))
			{
				continue;
			}
			
			if (owner.Settings.GetEnemyBotTypes().Contains(role))
			{
				enemies.Add(botToCheck);
			}
		}
		
		return enemies;
	}
}