using System.Reflection;
using Donuts.Bots;
using EFT;
using JetBrains.Annotations;
using SPT.Reflection.Patching;

namespace Donuts.Patches;

[UsedImplicitly]
internal class NewGameDonutsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

    [PatchPrefix]
    public static void PatchPrefix()
    {
        // This is only needed for Fika
        if (DonutsPlugin.IsFikaPresent)
        {
            DonutsRaidManager.Enable();
            _ = DonutsRaidManager.Initialize();
        }
    }
}