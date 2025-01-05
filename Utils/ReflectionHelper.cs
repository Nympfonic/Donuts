using EFT;
using HarmonyLib;
using System.Reflection;

namespace Donuts.Utils;

internal static class ReflectionHelper
{
	internal static readonly FieldInfo BotsController_botEnabled_Field = AccessTools.Field(typeof(BotsController), "_botEnabled");
	internal static readonly FieldInfo BotSpawner_botCreator_Field = AccessTools.Field(typeof(BotSpawner), "_botCreator");
	//internal static readonly FieldInfo BotSpawner_cancellationTokenSource_Field = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource");

	//internal static readonly MethodInfo BotSpawner_method9_Method = AccessTools.Method(typeof(BotSpawner), "method_9");
	internal static readonly MethodInfo BotSpawner_method11_Method = AccessTools.Method(typeof(BotSpawner), "method_11");
}