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
	
	private class AsyncEnumerator : IUniTaskAsyncEnumerator<BotGenerationProgress>
	{
		[NotNull] private readonly IBotDataService _dataService;
		private readonly int _minGroupSize;
		private readonly int _maxGroupSize;
		private CancellationToken _token;
		
		public AsyncEnumerator([NotNull] IBotDataService dataService,
			int maxBotsToGenerate,
			int minGroupSize,
			int maxGroupSize,
			CancellationToken token)
		{
			_dataService = dataService;
			_minGroupSize = minGroupSize;
			_maxGroupSize = maxGroupSize;
			_token = token;
			Current = new BotGenerationProgress(maxBotsToGenerate);
		}
		
		public BotGenerationProgress Current { get; }
		
		public UniTask DisposeAsync()
		{
			return UniTask.CompletedTask;
		}
		
		public async UniTask<bool> MoveNextAsync()
		{
			if (_token.IsCancellationRequested || Current.Progress >= 1)
			{
				return false;
			}
			
			int groupSize = BotHelper.GetBotGroupSize(_dataService.GroupChance, _minGroupSize, _maxGroupSize,
				Current.maxBotsToGenerate - Current.BotsGenerated);
			
			(bool success, PrepBotInfo prepBotInfo) = await _dataService.TryGenerateBotProfiles(
				_dataService.BotDifficulties.PickRandomElement(),
				groupSize,
				saveToCache: false,
				cancellationToken: _token);
			
			if (_token.IsCancellationRequested)
			{
				return false;
			}
			
			if (success)
			{
				_dataService.StartingBotsCache.Enqueue(prepBotInfo);
				int newBotsGeneratedValue = Current.BotsGenerated + groupSize;
				Current.Report(newBotsGeneratedValue);
			}
			
			return true;
		}
	}
}