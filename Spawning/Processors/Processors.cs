using Donuts.Spawning.Models;
using UnityEngine;
using UnityToolkit.Structures;

namespace Donuts.Spawning.Processors;

public abstract class SpawnCheckProcessorBase : ProcessorBase<Vector3>;
public abstract class WaveSpawnProcessorBase : ProcessorBase<BotWave>;