using System;
using UnityEngine;

namespace Donuts.Spawning.Models;

public struct BotGenerationProgress(int maxBotsToGenerate) : IProgress<int>
{
	public readonly int maxBotsToGenerate = maxBotsToGenerate;
	
	public float Progress { get; private set; }
	public int BotsGenerated { get; private set; }
	
	public void Report(int botsGenerated)
	{
		if (botsGenerated > maxBotsToGenerate || botsGenerated < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(botsGenerated));
		}
		
		BotsGenerated = botsGenerated;
		Progress = Mathf.Clamp(botsGenerated / (float)maxBotsToGenerate, 0, 1);
	}
}