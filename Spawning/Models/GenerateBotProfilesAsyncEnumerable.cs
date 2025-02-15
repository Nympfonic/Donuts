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
	: IUniTaskAsyncEnumerable<(int botsGenerated, int maxBotsToGenerate)>
{
	public IUniTaskAsyncEnumerator<(int botsGenerated, int maxBotsToGenerate)> GetAsyncEnumerator(
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
		: IUniTaskAsyncEnumerator<(int botsGenerated, int maxBotsToGenerate)>
	{
		public (int botsGenerated, int maxBotsToGenerate) Current { get; private set; } = (0, maxBotsToGenerate);
		
		public UniTask DisposeAsync()
		{
			return UniTask.CompletedTask;
		}
		
		public async UniTask<bool> MoveNextAsync()
		{
			if (token.IsCancellationRequested || Current.botsGenerated >= maxBotsToGenerate)
			{
				return false;
			}
			
			int groupSize = BotHelper.GetBotGroupSize(dataService.GroupChance, minGroupSize, maxGroupSize,
				maxBotsToGenerate - Current.botsGenerated);
			
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
				int newBotsGeneratedValue = Current.botsGenerated + groupSize;
				Current = (newBotsGeneratedValue, Current.maxBotsToGenerate);
			}
			
			return true;
		}
	}
}