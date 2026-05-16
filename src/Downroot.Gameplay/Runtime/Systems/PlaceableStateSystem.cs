using Downroot.Core.Definitions;

namespace Downroot.Gameplay.Runtime.Systems;

public sealed class PlaceableStateSystem(GameRuntime runtime, WorldRuntimeFacade worldFacade)
{
    public void Update(float deltaSeconds)
    {
        SyncWorld(runtime.Overworld);
        foreach (var pocketWorld in runtime.PocketWorlds.Values)
        {
            SyncWorld(pocketWorld);
        }
    }

    private void SyncWorld(LoadedWorldState world)
    {
        foreach (var entity in world.LoadedChunks.Values.SelectMany(chunk => chunk.RuntimeEntities.Values))
        {
            if (entity.Removed || entity.Kind != WorldEntityKind.Placeable)
            {
                continue;
            }

            if (!runtime.Content.Placeables.TryGet(entity.DefinitionId, out var placeableDef)
                || !placeableDef!.HasBehavior(PlaceableBehaviorKind.LightSource)
                || entity.PlaceableState is null)
            {
                continue;
            }

            var state = entity.PlaceableState;
            if (!state.IsLit || state.FuelSecondsRemaining <= 0f)
            {
                continue;
            }

            var elapsed = runtime.WorldState.TotalElapsedSeconds - state.FuelLastUpdatedTotalSeconds;
            if (elapsed <= 0f)
            {
                continue;
            }

            state.FuelSecondsRemaining = Math.Max(0f, state.FuelSecondsRemaining - elapsed);
            state.FuelLastUpdatedTotalSeconds = runtime.WorldState.TotalElapsedSeconds;
            if (state.FuelSecondsRemaining <= 0f)
            {
                state.IsLit = false;
                worldFacade.NotifyEntityStateChanged(entity);
                runtime.WorldState.SetStatusEvent(new StatusEventState(StatusEventKind.LightBurnedOut, entity.DefinitionId), 1.5f);
            }
        }
    }
}
