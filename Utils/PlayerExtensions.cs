using EFT;
using JetBrains.Annotations;

namespace Donuts.Utils;

public static class PlayerExtensions
{
	public static bool IsAlive([NotNull] this Player player)
	{
		return player.HealthController?.IsAlive == true;
	}
}