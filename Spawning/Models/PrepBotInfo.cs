using JetBrains.Annotations;
using System;

namespace Donuts.Spawning.Models;

public class PrepBotInfo(
	[NotNull] BotCreationDataClass botCreationData,
	BotDifficulty difficulty,
	int groupSize = 1)
{
	public readonly BotCreationDataClass botCreationData = botCreationData;
	public readonly BotDifficulty difficulty = difficulty;
	public readonly int groupSize = groupSize;
	public readonly Key key = new(difficulty, groupSize);
	
	public readonly struct Key(BotDifficulty difficulty, int groupSize) : IEquatable<Key>
	{
		private readonly BotDifficulty _difficulty = difficulty;
		private readonly int _groupSize = groupSize;
		
		public bool Equals(Key other)
		{
			return _difficulty == other._difficulty && _groupSize == other._groupSize;
		}
		
		public override bool Equals(object obj)
		{
			return obj is Key other && Equals(other);
		}
		
		public override int GetHashCode()
		{
			unchecked
			{
				return ((int)_difficulty * 397) ^ _groupSize;
			}
		}
	}
}