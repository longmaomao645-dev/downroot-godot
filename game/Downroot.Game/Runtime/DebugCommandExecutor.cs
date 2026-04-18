using Downroot.Core.Ids;
using Downroot.Core.World;
using Downroot.Gameplay.Runtime;
using Downroot.Gameplay.Runtime.Systems;

namespace Downroot.Game.Runtime;

public sealed class DebugCommandExecutor
{
    private readonly GameRuntime _runtime;
    private readonly DebugRuntimeState _debugState;
    private readonly Action _saveAction;
    private readonly Action _reloadAction;

    public DebugCommandExecutor(GameRuntime runtime, DebugRuntimeState debugState, Action saveAction, Action reloadAction)
    {
        _runtime = runtime;
        _debugState = debugState;
        _saveAction = saveAction;
        _reloadAction = reloadAction;
    }

    public string Execute(string commandLine)
    {
        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "give":
                if (parts.Length < 3 || !int.TryParse(parts[2], out var amount))
                {
                    return "Usage: give <contentId> <amount>";
                }

                return _runtime.Player.Inventory.TryAdd(new ContentId(parts[1]), amount, _runtime.Content)
                    ? $"Gave {parts[1]} x{amount}"
                    : $"Failed to add {parts[1]}";
            case "heal":
                _runtime.Player.Survival.SetHealth(_runtime.Player.Survival.MaxHealth);
                _runtime.Player.Survival.SetHunger(_runtime.Player.Survival.MaxHunger);
                return "Player restored";
            case "set_time":
                if (parts.Length < 2)
                {
                    return "Usage: set_time day|night";
                }

                _runtime.WorldState.TimeOfDaySeconds = string.Equals(parts[1], "night", StringComparison.OrdinalIgnoreCase)
                    ? _runtime.BootstrapConfig.DayLengthSeconds * TimeOfDayRules.ResolveNormalizedTimeForHour(23f)
                    : _runtime.BootstrapConfig.DayLengthSeconds * TimeOfDayRules.ResolveNormalizedTimeForHour(12f);
                return $"Time set to {parts[1]}";
            case "teleport_portal":
                return TeleportPortal();
            case "save":
                _saveAction();
                return "Saved current slot";
            case "reload":
                _reloadAction();
                return "Reload requested";
            default:
                return $"Unknown command: {parts[0]}";
        }
    }

    private string TeleportPortal()
    {
        var world = _runtime.GetWorld(_runtime.ActiveWorldSpaceKind);
        var portalLink = _runtime.Content.PortalWorldLinks.FirstOrDefault(link =>
            link.SourceWorldSpaceKind == _runtime.ActiveWorldSpaceKind
            || link.TargetWorldSpaceKind == _runtime.ActiveWorldSpaceKind);
        if (portalLink is null)
        {
            return "No portal link available";
        }

        var chunk = portalLink.SourceWorldSpaceKind == _runtime.ActiveWorldSpaceKind
            ? portalLink.SourcePortalChunk
            : portalLink.TargetPortalChunk;
        var tile = WorldTileCoord.FromChunkAndLocal(chunk, new LocalTileCoord(0, 0), _runtime.ChunkWidth, _runtime.ChunkHeight);
        _runtime.Player.Position = _runtime.GetWorldPosition(tile);
        new WorldStreamingSystem(_runtime, new WorldRuntimeFacade(_runtime)).UpdateLoadedChunksForWorld(world, tile);
        _runtime.WorldState.MarkEntityProjectionDirty();
        return $"Teleported to portal chunk {chunk.X},{chunk.Y}";
    }
}
