using System;

namespace Donuts.Spawning;

public enum DonutsSpawnType
{
    Pmc,
    Scav
}

public static class DonutsSpawnTypeExtensions
{
	public static string Localized(this DonutsSpawnType type)
	{
		return type switch
		{
			DonutsSpawnType.Pmc => "PMC",
			DonutsSpawnType.Scav => "Scav",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}

	public static string LocalizedPlural(this DonutsSpawnType type)
	{
		return type switch
		{
			DonutsSpawnType.Pmc => "PMCs",
			DonutsSpawnType.Scav => "Scavs",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}
}