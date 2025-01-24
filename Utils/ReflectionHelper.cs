using EFT;
using HarmonyLib;
using System.Reflection;

namespace Donuts.Utils;

internal static class ReflectionHelper
{
	internal static readonly FieldInfo BotSpawner_botCreator_Field = AccessTools.Field(typeof(BotSpawner), "_botCreator");
	internal static readonly FieldInfo BotSpawner_inSpawnProcess_Field = AccessTools.Field(typeof(BotSpawner), "_inSpawnProcess");
}