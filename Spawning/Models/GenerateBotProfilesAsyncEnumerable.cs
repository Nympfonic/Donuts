using Cysharp.Threading.Tasks;
using Donuts.Spawning.Services;
using Donuts.Utils;
using JetBrains.Annotations;
using System.Threading;

namespace Donuts.Spawning.Models;

public class GenerateBotProfilesAsyncEnumerable(
	[NotNull] IBotDataService dataService,
	int maxBotsToGenerate,
	int minGroupSize,
	int maxGroupSize,
	CancellationToken token)
	: IUniTaskAsyncEnumerable<BotGenerationProgress>
{
	public IUniTaskAsyncEnumerator<BotGenerationProgress> GetAsyncEnumerator(
		CancellationToken cancellationToken = default)
	{
		return new AsyncEnumerator(dataService, maxBotsToGenerate, minGroupSize, maxGroupSize, token);
	}
	
	private class AsyncEnumerator(
		[NotNull] IBotDataService dataService,
		int maxBotsToGenerate,
		int minGroupSize,
		int maxGroupSize,
		CancellationToken token)
		: IUniTaskAsyncEnumerator<BotGenerationProgress>
	{
		public BotGenerationProgress Current { get; } = new(maxBotsToGenerate);
		
		public UniTask DisposeAsync()
		{
			return UniTask.CompletedTask;
		}
		
		public async UniTask<bool> MoveNextAsync()
		{
			if (token.IsCancellationRequested || Current.TotalBotsGenerated >= maxBotsToGenerate)
			{
				return false;
			}
			
			int groupSize = BotHelper.GetBotGroupSize(dataService.GroupChance, minGroupSize, maxGroupSize,
				Current.maxBotsToGenerate - Current.TotalBotsGenerated);
			
			(bool success, PrepBotInfo prepBotInfo) = await dataService.TryGenerateBotProfiles(
				dataService.BotDifficulties.PickRandomElement(),
				groupSize,
				saveToCache: false,
				cancellationToken: token);
			
			if (token.IsCancellationRequested)
			{
				return false;
			}
			
			if (success)
			{
				dataService.StartingBotsCache.Enqueue(prepBotInfo);
				Current.Report(groupSize);
			}
			
			return true;
		}
	}
}