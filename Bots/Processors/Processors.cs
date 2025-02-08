using Donuts.Models;
using System;
using UnityEngine;
using UnityToolkit.Structures;

namespace Donuts.Bots.Processors;

public abstract class SpawnCheckProcessorBase : ProcessorBase<Vector3>;
public abstract class WaveSpawnProcessorBase : ProcessorBase<WaveSpawnData>;

public class WaveSpawnData(Action<int> resetGroupTimerCallback)
{
	public readonly Action<int> resetGroupTimerCallback = resetGroupTimerCallback;
	
	public BotWave Wave { get; set; }
}