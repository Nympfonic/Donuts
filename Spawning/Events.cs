using EFT;
using JetBrains.Annotations;
using System;
using UnityToolkit.Structures.EventBus;

namespace Donuts.Spawning;

public readonly struct RegisterBotEvent : IEvent
{
	public readonly BotOwner bot;
	
	[Obsolete("Use the static Create method instead!", true)]
	public RegisterBotEvent() : this(null)
	{
		throw new InvalidOperationException("Use the static Create method instead!");
	}
	
	private RegisterBotEvent(BotOwner bot)
	{
		this.bot = bot;
	}
	
	public static RegisterBotEvent Create([NotNull] BotOwner bot) => new(bot);
}

public readonly struct BotSpawnedEvent : IEvent
{
	public static BotSpawnedEvent Create() => new();
}

public readonly struct BotGenStatusChangeEvent : IEvent
{
	[NotNull] public readonly string message;
	public readonly float? progress;
	
	[Obsolete("Use the static Create method instead!", true)]
	public BotGenStatusChangeEvent() : this(null, null)
	{
		throw new InvalidOperationException("Use the static Create method instead!");
	}
	
	private BotGenStatusChangeEvent(string message, float? progress)
	{
		this.message = message;
		this.progress = progress;
	}
	
	public static BotGenStatusChangeEvent Create([NotNull] string message, float? progress = null) => new(message, progress);
}

public readonly struct PlayerEnteredCombatEvent : IEvent
{
	public static PlayerEnteredCombatEvent Create() => new();
}

public readonly struct PlayerTargetedByBotEvent : IEvent
{
	public static PlayerTargetedByBotEvent Create() => new();
}

public readonly struct EveryUpdateSecondEvent : IEvent
{
	public readonly float deltaTime;
	
	[Obsolete("Use the static Create method instead!", true)]
	public EveryUpdateSecondEvent() : this(0)
	{
		throw new InvalidOperationException("Use the static Create method instead!");
	}
	
	private EveryUpdateSecondEvent(float deltaTime)
	{
		this.deltaTime = deltaTime;
	}
	
	public static EveryUpdateSecondEvent Create(float deltaTime) => new(deltaTime);
}