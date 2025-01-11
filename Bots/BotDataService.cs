using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using Donuts.Utils;
using EFT;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using UnityEngine;
using BotProfileData = GClass652; // Implements IGetProfileData
using Random = UnityEngine.Random;

namespace Donuts.Bots;

public interface IBotDataService
{
	public List<BotSpawnInfo> BotSpawnInfos { get; }
	public Dictionary<string, List<Vector3>> ZoneSpawnPoints { get; }
	public DonutsSpawnType SpawnType { get; }
	public int MaxBotLimit { get; }
	
	UniTask<(bool success, BotCreationDataClass botData)> TryCreateBotData([NotNull] PrepBotInfo botInfo);
	UniTask ReplenishBotData();
	[CanBeNull] BotCreationDataClass FindCachedBotData(BotDifficulty difficulty, int targetCount);
	void ClearBotCache([NotNull] BotCreationDataClass botData);
	BotDifficulty GetBotDifficulty();
}

public abstract class BotDataService : IBotDataService
{
	private CancellationToken _onDestroyToken;
	private IBotCreator _botCreator;
	private BotSpawner _eftBotSpawner;
	
	protected BotConfig botConfig;

	private const int INITIAL_BOT_CACHE_SIZE = 30;
	private readonly List<PrepBotInfo> _botInfoList = new(INITIAL_BOT_CACHE_SIZE);
	
	protected BotConfigService ConfigService { get; private set; }
	protected ManualLogSource Logger { get; private set; }
	
	public List<BotSpawnInfo> BotSpawnInfos { get; } = new(INITIAL_BOT_CACHE_SIZE);
	public Dictionary<string, List<Vector3>> ZoneSpawnPoints { get; } = [];
	public abstract DonutsSpawnType SpawnType { get; }
	public int MaxBotLimit => ConfigService.GetMaxBotLimit(SpawnType);
	
	protected abstract string GroupChance { get; }
	protected HashSet<string> UsedSpawnZones { get; } = [];
	protected abstract ReadOnlyCollection<BotDifficulty> BotDifficulties { get; }

	public static async UniTask<TBotDataService> Create<TBotDataService>(
		[NotNull] BotConfigService configService,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
	where TBotDataService : BotDataService, new()
	{
		var service = new TBotDataService();
		service.Initialize(configService, logger, cancellationToken);
		MonoBehaviourSingleton<DonutsRaidManager>.Instance.BotDataServices.Add(service.SpawnType, service);
		await service.SetupInitialBotCache();
		return service;
	}
	
	public abstract BotDifficulty GetBotDifficulty();
	
	protected abstract BotConfig GetBotConfig();

	private async UniTask SetupInitialBotCache()
	{
		try
		{
			BotConfig botCfg = GetBotConfig();
			int maxBots = BotHelper.GetRandomBotCap(botCfg.MinCount, botCfg.MaxCount, MaxBotLimit);
#if DEBUG
			using (var sb = ZString.CreateUtf8StringBuilder())
			{
				sb.AppendFormat("{0} {1}::{2}: Max starting bots set to {3}", DateTime.Now.ToLongTimeString(),
					GetType().Name, nameof(SetupInitialBotCache), maxBots.ToString());
				Logger.LogDebug(sb.ToString());
			}
#endif
			var totalBots = 0;
			while (totalBots < maxBots && !_onDestroyToken.IsCancellationRequested)
			{
				int groupSize = BotHelper.GetBotGroupSize(GroupChance, botCfg.MinGroupSize, botCfg.MaxGroupSize,
					maxBots - totalBots);

				string selectedZone = SpawnPointHelper.SelectUnusedZone(UsedSpawnZones, ZoneSpawnPoints);
				List<Vector3> spawnPoints = ZoneSpawnPoints[selectedZone].ShuffleElements();
				
				var botInfo = new PrepBotInfo(BotDifficulties.PickRandomElement(), groupSize > 1, groupSize);
				(bool success, BotCreationDataClass _) = await TryCreateBotData(botInfo);
				if (_onDestroyToken.IsCancellationRequested) return;
				
				if (success)
				{
					BotSpawnInfos.Add(new BotSpawnInfo(botInfo, selectedZone, spawnPoints));
					totalBots += groupSize;
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(GetType().Name, nameof(SetupInitialBotCache), ex);
		}
		catch (OperationCanceledException) {}
	}

	protected abstract WildSpawnType GetWildSpawnType();
	protected abstract EPlayerSide GetPlayerSide(WildSpawnType spawnType);

	public async UniTask<(bool success, BotCreationDataClass botData)> TryCreateBotData(PrepBotInfo botInfo)
	{
		try
		{
			if (_onDestroyToken.IsCancellationRequested)
			{
				return (false, null);
			}
			
			WildSpawnType spawnType = GetWildSpawnType();
			EPlayerSide side = GetPlayerSide(spawnType);
#if DEBUG
			using (var sb = ZString.CreateUtf8StringBuilder())
			{
				sb.AppendFormat("{0} {1}::{2}: Creating bot: Type={3}, Difficulty={4}, Side={5}, GroupSize={6}",
					DateTime.Now.ToLongTimeString(), GetType().Name, nameof(TryCreateBotData), spawnType.ToString(),
					botInfo.Difficulty.ToString(), side.ToString(), botInfo.GroupSize.ToString());
				Logger.LogDebug(sb.ToString());
			}
#endif
			var botProfileData = new BotProfileData(side, spawnType, botInfo.Difficulty, 0f);
			var botCreationData = await BotCreationDataClass
				.Create(botProfileData, _botCreator, botInfo.GroupSize, _eftBotSpawner);

			if (botCreationData?.Profiles == null || botCreationData.Profiles.Count == 0)
			{
				return (false, null);
			}

			botInfo.Bots = botCreationData;
			_botInfoList.Add(botInfo);
#if DEBUG
			using (var sb = ZString.CreateUtf8StringBuilder())
			{
				sb.AppendFormat("{0} {1}::{2}: Bot created and assigned successfully; {3} profiles loaded. IDs: {4}",
					DateTime.Now.ToLongTimeString(), GetType().Name, nameof(TryCreateBotData),
					botCreationData.Profiles.Count.ToString(),
					string.Join(", ", botCreationData.Profiles.Select(p => p.Id)));
				Logger.LogDebug(sb.ToString());
			}
#endif
			return (true, botCreationData);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(GetType().Name, nameof(TryCreateBotData), ex);
		}
		catch (OperationCanceledException) {}
		
		return (false, null);
	}

	public async UniTask ReplenishBotData()
	{
		try
		{
			var singleBotsCount = 0;
			var groupBotsCount = 0;
			for (int i = _botInfoList.Count - 1; i >= 0; i--)
			{
				PrepBotInfo botInfo = _botInfoList[i];
				if (_onDestroyToken.IsCancellationRequested) return;
				if (botInfo.Bots != null && botInfo.Bots.Profiles.Count > 0) continue;

				(bool success, BotCreationDataClass botData) = await TryCreateBotData(botInfo);
				if (_onDestroyToken.IsCancellationRequested) return;
				if (!success) continue;

				botData._profileData.TryGetRole(out WildSpawnType role, out _);
				if (botInfo.IsGroup && groupBotsCount < 1)
				{
					groupBotsCount++;
#if DEBUG
					using (var sb = ZString.CreateUtf8StringBuilder())
					{
						sb.AppendFormat("{0} {1}::{2}: Replenishing group bot: {3} {4} {5} Count: {6}.",
							DateTime.Now.ToLongTimeString(), GetType().Name, nameof(ReplenishBotData), role.ToString(),
							botInfo.Difficulty.ToString(), botData.Side.ToString(), botInfo.GroupSize.ToString());
						Logger.LogDebug(sb.ToString());
					}
#endif
				}
				else if (!botInfo.IsGroup && singleBotsCount < 3)
				{
					singleBotsCount++;
#if DEBUG
					using (var sb = ZString.CreateUtf8StringBuilder())
					{
						sb.AppendFormat("{0} {1}::{2}: Replenishing single bot: {3} {4} {5} Count: 1.",
							DateTime.Now.ToLongTimeString(), GetType().Name, nameof(ReplenishBotData), role.ToString(),
							botInfo.Difficulty.ToString(), botData.Side.ToString());
						Logger.LogDebug(sb.ToString());
					}
#endif
				}

				if (singleBotsCount >= 3 && groupBotsCount >= 1) break;
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogException(GetType().Name, nameof(ReplenishBotData), ex);
		}
		catch (OperationCanceledException) {}
	}

	public BotCreationDataClass FindCachedBotData(BotDifficulty difficulty, int groupSize)
	{
		// Find the bot info that matches the difficulty and group size
		for (int i = _botInfoList.Count - 1; i >= 0; i--)
		{
			PrepBotInfo botInfo = _botInfoList[i];
			if (botInfo.Difficulty == difficulty &&
				botInfo.Bots != null &&
				botInfo.Bots.Profiles.Count == groupSize)
			{
				return botInfo.Bots;
			}
		}
#if DEBUG
		using (var sb = ZString.CreateUtf8StringBuilder())
		{
			sb.AppendFormat("{0} {1}::{2}: No cached bots found for difficulty {3}, and target count {4}.",
				DateTime.Now.ToLongTimeString(), GetType().Name, nameof(FindCachedBotData), difficulty.ToString(),
				groupSize.ToString());
			Logger.LogWarning(sb.ToString());
		}
#endif
		return null;
	}

	public void ClearBotCache(BotCreationDataClass botData)
	{
		for (int i = _botInfoList.Count - 1; i >= 0; i--)
		{
			PrepBotInfo botInfo = _botInfoList[i];
			if (botInfo.Bots == botData)
			{
				botInfo.Bots = null;
#if DEBUG
				using (var sb = ZString.CreateUtf8StringBuilder())
				{
					sb.AppendFormat("{0} {1}::{2}: Cleared cached bot info for bot type: {3}",
						DateTime.Now.ToLongTimeString(), GetType().Name, nameof(ClearBotCache), SpawnType.ToString());
					Logger.LogDebug(sb.ToString());
				}
#endif
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

	protected abstract List<string> GetZoneNames(string location);

	private void Initialize(
		[NotNull] BotConfigService configService,
		[NotNull] ManualLogSource logger,
		CancellationToken cancellationToken)
	{
		ConfigService = configService;
		Logger = logger;
		_onDestroyToken = cancellationToken;
		_eftBotSpawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
		_botCreator = (IBotCreator)ReflectionHelper.BotSpawner_botCreator_Field.GetValue(_eftBotSpawner);

		string location = ConfigService.GetMapLocation();
		Dictionary<string,List<Position>> zones = ConfigService.GetAllMapsZoneConfig()!.Maps[location].Zones;
		SpawnPointHelper.SetSpawnPointsForZones(ZoneSpawnPoints, zones, GetZoneNames(location));
	}
}