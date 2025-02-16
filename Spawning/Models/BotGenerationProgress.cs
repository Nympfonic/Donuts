using System;
using UnityEngine;

namespace Donuts.Spawning.Models;

public class BotGenerationProgress(int maxBotsToGenerate, string statusMessage) : IProgress<int>
{
	public readonly int maxBotsToGenerate = maxBotsToGenerate;
	public readonly string statusMessage = statusMessage;
	
	public float Progress { get; private set; }
	public int TotalBotsGenerated { get; private set; }
	
	public void Report(int botsGenerated)
	{
		if (botsGenerated < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(botsGenerated));
		}
		
		TotalBotsGenerated += botsGenerated;
		Progress = Mathf.Clamp(TotalBotsGenerated / (float)maxBotsToGenerate, 0f, 1f);
	}
}