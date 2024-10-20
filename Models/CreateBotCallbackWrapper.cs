using Comfort.Common;
using EFT;
using HarmonyLib;
using System;
using System.Diagnostics;

namespace Donuts.Models
{
	// Wrapper around method_10 called after bot creation, so we can pass it the BotCreationDataClass data
	public class CreateBotCallbackWrapper(BotCreationDataClass botData)
	{
		private static CreateBotCallbackDelegate createBotCallbackDelegate;
		private readonly Stopwatch stopwatch = new();

		private delegate void CreateBotCallbackDelegate(BotOwner botOwner, BotCreationDataClass data,
			Action<BotOwner> callback, bool shallBeGroup, Stopwatch stopwatch);

		public void CreateBotCallback(BotOwner bot)
		{
			var botSpawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
			if (botSpawner == null)
			{
				ClearDelegate();
				return;
			}

			if (createBotCallbackDelegate == null || createBotCallbackDelegate.Target != botSpawner)
			{
				SetDelegate(botSpawner);
			}

			bool shallBeGroup = botData.SpawnParams?.ShallBeGroup != null;

			// I have no idea why BSG passes a stopwatch into this call...
			// TODO: Looks like SPT 3.10 has a patch that will remove BSG's instantiation of a stopwatch
			// Remove for 3.10 update
			stopwatch.Start();
			//ReflectionCache.BotSpawner_method10_Method.Invoke(botSpawner, [bot, botData, null, shallBeGroup, stopWatch]);
			createBotCallbackDelegate(bot, botData, null, shallBeGroup, stopwatch);
		}

		private static void SetDelegate(BotSpawner botSpawner)
		{
			createBotCallbackDelegate = AccessTools.MethodDelegate<CreateBotCallbackDelegate>(
				ReflectionCache.BotSpawner_method10_Method,
				botSpawner,
				false
			);
		}

		private static void ClearDelegate()
		{
			createBotCallbackDelegate = null;
		}
	}
}
