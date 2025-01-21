using Donuts.Bots;
using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityToolkit.Extensions;

namespace Donuts.Models;

public class ActivateBotCallbackWrapper([NotNull] BotSpawner botSpawner, [NotNull] BotCreationDataClass botData)
{
	private static readonly Stopwatch _stopwatch = new();
	// private static readonly WaitForSeconds _checkInterval = new(0.01f);
	
	private BotsGroup _group;
	// private int _groupBotCounter = 0;
	// private Coroutine _bossWaitForFollowerSpawnCoroutine;
	
	internal static ActivateBotCallbackDelegate ActivateBotDelegate { get; set; }

	internal delegate void ActivateBotCallbackDelegate([NotNull] BotOwner botOwner, [NotNull] BotCreationDataClass data,
		[CanBeNull] Action<BotOwner> callback, bool shallBeGroup, [NotNull] Stopwatch stopwatch);

	public void CreateBotCallback([NotNull] BotOwner bot)
	{
		// Create method delegate and cache it
		if (ActivateBotDelegate == null || ActivateBotDelegate.Target != botSpawner)
		{
			ActivateBotDelegate = AccessTools.MethodDelegate<ActivateBotCallbackDelegate>(
				ReflectionHelper.BotSpawner_method11_Method, botSpawner, false);
		}

		bool shallBeGroup = botData.SpawnParams?.ShallBeGroup != null;
		
		// BSG wants a stopwatch, we'll give em a stopwatch
		// TODO: transpile patch out the stopwatch
		ActivateBotDelegate(bot, botData, null, shallBeGroup, _stopwatch);
		
		// BossWithFollowersCheck(bot);
	}
	
	public BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
	{
		// If we haven't found/created our BotsGroup yet, do so, and then lock it so nobody else can use it
		if (_group == null)
		{
			_group = botSpawner.GetGroupAndSetEnemies(bot, zone);
			_group.Lock();
		}
		// For the rest of the bots in the same group, check if the bot should be added to other bot groups' allies/enemies list
		// This is normally performed in BotSpawner::GetGroupAndSetEnemies(BotOwner, BotZone)
		else
		{
			botSpawner.method_5(bot);
		}
		
		return _group;
	}

	// private void BossWithFollowersCheck(BotOwner bot)
	// {
	// 	if (!IsBossWithFollowers())
	// 	{
	// 		return;
	// 	}
	//
	// 	// If it's a boss, wait for followers to spawn before setting followers. Thanks to DanW for this!
	// 	if (_bossWaitForFollowerSpawnCoroutine == null)
	// 	{
	// 		_bossWaitForFollowerSpawnCoroutine = MonoBehaviourSingleton<DonutsRaidManager>.Instance
	// 			.OrNull()?
	// 			.StartCoroutine(WaitForFollowersAndSetBoss(bot, botData.Profiles.Count - 1));
	// 		return;
	// 	}
	// }

	// private bool IsBossWithFollowers() =>
	// 	botData._profileData.TryGetRole(out WildSpawnType role, out _) &&
	// 	role.IsBoss() &&
	// 	botData.Profiles.Count > 1;
	//
	// private IEnumerator WaitForFollowersAndSetBoss(BotOwner botOwner, int followerCount)
	// {
	// 	while (!allAliveBotsActive)
	// 	{
	// 		yield return _checkInterval;
	// 	}
	// 	
	// 	botOwner.Boss.SetBoss(followerCount);
	// }
}