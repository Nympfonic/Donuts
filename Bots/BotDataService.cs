using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using BotProfileData = GClass652;
using Random = UnityEngine.Random; // Implements IGetProfileData

namespace Donuts.Bots;

public interface IBotDataService
{
	public List<BotSpawnInfo> BotSpawnInfos { get; }
	public Dictionary<string, List<Vector3>> ZoneSpawnPoints { get; }
	public DonutsSpawnType SpawnType { get; }
	public int MaxBotLimit { get; }
	
	UniTask SetupInitialBotCache(CancellationToken cancellationToken);
	UniTask<(bool, BotCreationDataClass)> TryCreateBotData([NotNull] PrepBotInfo botInfo);
	UniTask ReplenishBotData(CancellationToken cancellationToken);
	[CanBeNull] BotCreationDataClass FindCachedBotData(BotDifficulty difficulty, int targetCount);
	void ClearBotCache([NotNull] BotCreationDataClass botData);
	BotDifficulty GetBotDifficulty();
}

public abstract class BotDataService : IBotDataService
{
	private CancellationToken _cancellationToken;
	private IBotCreator _botCreator;
	private BotSpawner _eftBotSpawner;

	private const int INITIAL_BOT_CACHE_SIZE = 100;
	private readonly List<PrepBotInfo> _botInfoList = new(INITIAL_BOT_CACHE_SIZE);
	
	protected BotConfigService ConfigService { get; private set; }
	protected ManualLogSource Logger { get; private set; }
	
	public List<BotSpawnInfo> BotSpawnInfos { get; } = new(INITIAL_BOT_CACHE_SIZE);
	public Dictionary<string, List<Vector3>> ZoneSpawnPoints { get; } = [];
	public abstract DonutsSpawnType SpawnType { get; }
	public int MaxBotLimit => ConfigService.GetMaxBotLimit(SpawnType);
	protected abstract string GroupChance { get; }
	protected HashSet<string> UsedSpawnZones { get; } = [];
	
	protected abstract BotConfig BotConfig { get; }
	protected abstract List<BotDifficulty> BotDifficulties { get; }

	public static TBotDataService Create<TBotDataService>(
		[NotNull] BotConfigService configService,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
		where TBotDataService : BotDataService, new()
	{
		var service = new TBotDataService();
		service.Initialize(configService, logger, cancellationToken);
		return service;
	}

	public abstract WildSpawnType GetWildSpawnType();
	public abstract EPlayerSide GetPlayerSide(WildSpawnType spawnType);
	public abstract BotDifficulty GetBotDifficulty();
	protected abstract List<string> GetZoneNames(string location);

	public async UniTask SetupInitialBotCache(CancellationToken cancellationToken)
	{
		try
		{
			BotConfig botCfg = BotConfig;
			int maxBots = BotHelper.GetRandomBotCap(botCfg.MinCount, botCfg.MaxCount, MaxBotLimit);
#if DEBUG
			Logger.LogDebug($"{GetType()}::{nameof(SetupInitialBotCache)}: Max starting bots: {maxBots.ToString()}");
#endif
			var totalBots = 0;
			while (totalBots < maxBots)
			{
				if (cancellationToken.IsCancellationRequested) break;
				
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, botCfg.MinGroupSize, botCfg.MaxGroupSize,
					maxBots - totalBots);

				string selectedZone = SpawnPointHelper.SelectUnusedZone(UsedSpawnZones, ZoneSpawnPoints);
				List<Vector3> spawnPoints = ZoneSpawnPoints[selectedZone].ShuffleElements();
				
				var botInfo = new PrepBotInfo(BotDifficulties.PickRandomElement(), groupSize > 1, groupSize);
				(bool success, BotCreationDataClass _) = await TryCreateBotData(botInfo);
				if (cancellationToken.IsCancellationRequested) return;
				
				if (success)
				{
					BotSpawnInfos.Add(new BotSpawnInfo(botInfo, selectedZone, spawnPoints));
					totalBots += groupSize;
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError($"Exception thrown in {GetType()}::{nameof(SetupInitialBotCache)}: {ex.Message}\n{ex.StackTrace}");
		}
		catch (OperationCanceledException) {}
	}

	public async UniTask<(bool, BotCreationDataClass)> TryCreateBotData(PrepBotInfo botInfo)
	{
		try
		{
			if (_cancellationToken.IsCancellationRequested)
			{
				return (false, null);
			}
			
			WildSpawnType spawnType = GetWildSpawnType();
			EPlayerSide side = GetPlayerSide(spawnType);
#if DEBUG
			Logger.LogDebug(string.Format("{0}::{1}: Creating bot: Type={2}, Difficulty={3}, Side={4}, GroupSize={5}",
				nameof(BotDataService), nameof(TryCreateBotData), spawnType.ToString(),
				botInfo.Difficulty.ToString(), side.ToString(), botInfo.GroupSize.ToString()));
#endif
			var botProfileData = new BotProfileData(side, spawnType, botInfo.Difficulty, 0f);
			BotCreationDataClass botCreationData = await BotCreationDataClass
				.Create(botProfileData, _botCreator, botInfo.GroupSize, _eftBotSpawner)
				.AsUniTask()
				.AttachExternalCancellation(_cancellationToken);

			if (botCreationData?.Profiles == null || botCreationData.Profiles.Count == 0)
			{
				Logger.LogError(string.Format(
					"{0}::{1}: Failed to create or properly initialize bot for {2}; Profiles is null or empty.",
					GetType(), nameof(TryCreateBotData), spawnType.ToString()));
				return (false, null);
			}

			botInfo.Bots = botCreationData;
			_botInfoList.Add(botInfo);
#if DEBUG
			Logger.LogDebug(string.Format("{0}::{1}: Bot created and assigned successfully; {2} profiles loaded.",
				GetType(), nameof(TryCreateBotData), botCreationData.Profiles.Count.ToString()));
#endif
			return (true, botCreationData);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError(string.Format("Exception thrown in {0}::{1}: {2}\n{3}", GetType(), nameof(TryCreateBotData),
				ex.Message, ex.StackTrace));
		}
		catch (OperationCanceledException) {}
		
		return (false, null);
	}

	public async UniTask ReplenishBotData(CancellationToken cancellationToken)
	{
		var singleBotsCount = 0;
		var groupBotsCount = 0;
		foreach (PrepBotInfo botInfo in _botInfoList)
		{
			if (cancellationToken.IsCancellationRequested) return;
			if (botInfo.Bots != null && botInfo.Bots.Profiles.Count > 0) continue;
				
			(bool success, BotCreationDataClass botData) = await TryCreateBotData(botInfo);
			if (cancellationToken.IsCancellationRequested) return;
			if (!success) continue;
				
			if (botInfo.IsGroup && groupBotsCount < 1)
			{
				groupBotsCount++;
#if DEBUG
				Logger.LogDebug(string.Format("Replenishing group bot: {0} {1} {2} Count: {3}.",
					SpawnType.ToString(), botInfo.Difficulty.ToString(), botData.Side.ToString(), botInfo.GroupSize.ToString()));
#endif
			}
			else if (!botInfo.IsGroup && singleBotsCount < 3)
			{
				singleBotsCount++;
#if DEBUG
				Logger.LogDebug(string.Format("Replenishing single bot: {0} {1} {2} Count: 1.",
					SpawnType.ToString(), botInfo.Difficulty.ToString(), botData.Side.ToString()));
#endif
			}

			if (singleBotsCount >= 3 && groupBotsCount >= 1) break;
		}
	}
	
	public BotCreationDataClass FindCachedBotData(BotDifficulty difficulty, int groupSize)
	{
		// Find the bot info that matches the difficulty and group size
		foreach (PrepBotInfo botInfo in _botInfoList)
		{
			if (botInfo.Difficulty == difficulty &&
				botInfo.Bots != null &&
				botInfo.Bots.Profiles.Count == groupSize)
			{
				return botInfo.Bots;
			}
		}
#if DEBUG
		Logger.LogWarning(string.Format(
			"{0}: No cached bots found for difficulty {1}, and target count {2}.",
			nameof(FindCachedBotData), difficulty.ToString(), groupSize.ToString()));
#endif
		return null;
	}
	
	public void ClearBotCache(BotCreationDataClass botData)
	{
		foreach (PrepBotInfo botInfo in _botInfoList)
		{
			if (botInfo.Bots == botData)
			{
				botInfo.Bots = null;
#if DEBUG
				Logger.LogDebug($"Cleared cached bot info for bot type: {SpawnType.ToString()}");
#endif
				Singleton<DonutsRaidManager>.Instance.RestartReplenishBotDataTimer();
				return;
			}
		}
	}
	
	protected static BotDifficulty GetBotDifficulty(string settingValue)
	{
		string difficultyLower = settingValue.ToLower();
		switch (difficultyLower)
		{
			case "asonline":
				return DefaultPluginVars.BotDifficulties[Random.Range(0, 3)];
			case "easy":
			case "normal":
			case "hard":
			case "impossible":
				if (!Enum.TryParse(difficultyLower, out BotDifficulty result))
				{
					goto default;
				}
				return result;
			default:
				return BotDifficulty.normal;
		}
	}

	private void Initialize(
		[NotNull] BotConfigService configService,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
	{
		ConfigService = configService;
		Logger = logger;
		_cancellationToken = cancellationToken;
		_eftBotSpawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
		_botCreator = (IBotCreator)ReflectionHelper.BotSpawner_botCreator_Field.GetValue(_eftBotSpawner);

		string location = ConfigService.GetMapLocation();
		Dictionary<string,List<Position>> zones = ConfigService.GetAllMapsZoneConfig()!.Maps[location].Zones;
		SpawnPointHelper.SetSpawnPointsForZones(ZoneSpawnPoints, zones, GetZoneNames(location));
	}
}