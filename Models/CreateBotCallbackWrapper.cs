using Comfort.Common;
using Donuts.Utils;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Diagnostics;

namespace Donuts.Models;

// Wrapper around method_11 called after bot creation, so we can pass it the BotCreationDataClass data
public class CreateBotCallbackWrapper([NotNull] BotCreationDataClass botData)
{
	private static CreateBotCallbackDelegate _createBotCallbackDelegate;
	private readonly Stopwatch _stopwatch = new();

	private delegate void CreateBotCallbackDelegate([NotNull] BotOwner botOwner, [NotNull] BotCreationDataClass data,
		[CanBeNull] Action<BotOwner> callback, bool shallBeGroup, [NotNull] Stopwatch stopwatch);

	public void CreateBotCallback([NotNull] BotOwner bot)
	{
		BotSpawner botSpawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
		if (botSpawner == null)
		{
			_createBotCallbackDelegate = null;
			return;
		}

		if (_createBotCallbackDelegate == null || _createBotCallbackDelegate.Target != botSpawner)
		{
			_createBotCallbackDelegate = AccessTools.MethodDelegate<CreateBotCallbackDelegate>(
				ReflectionHelper.BotSpawner_method11_Method, botSpawner, false);
		}

		bool shallBeGroup = botData.SpawnParams?.ShallBeGroup != null;
		
		//ReflectionCache.BotSpawner_method11_Method.Invoke(botSpawner, [bot, botData, null, shallBeGroup, stopWatch]);
		_createBotCallbackDelegate(bot, botData, null, shallBeGroup, _stopwatch);
	}
}