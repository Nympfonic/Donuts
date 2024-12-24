using EFT;

namespace Donuts.Bots.SpawnCheckProcessor;

public class PlayerVicinitySpawnCheckProcessor : SpawnCheckProcessorBase
{
	public override void Process(SpawnCheckData data)
	{
		if (!DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value)
		{
			data.Success = false;
			return;
		}
		
		float triggerDistance = GetMinDistanceFromPlayer(data.MapLocation);
		float triggerSqrMagnitude = triggerDistance * triggerDistance;

		foreach (Player player in data.AlivePlayers)
		{
			if (player.IsAI)
			{
				continue;
			}

			float actualSqrMagnitude = (((IPlayer)player).Position - data.Position).sqrMagnitude;
			// At least one player should be in the vicinity of the spawn position to trigger the spawn
			if (actualSqrMagnitude <= triggerSqrMagnitude)
			{
				data.Success = true;
				base.Process(data);
				return;
			}
		}
		
		data.Success = false;
	}
	
	private static float GetMinDistanceFromPlayer(string mapLocation) =>
		mapLocation switch
		{
			"bigmap" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerCustoms.Value,
			"factory4_day" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerFactory.Value,
			"factory4_night" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerStreets.Value,
			"sandbox" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerGroundZero.Value,
			"sandbox_high" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerShoreline.Value,
			"woods" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerWoods.Value,
			"laboratory" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerInterchange.Value,
			_ => 50f,
		};
}