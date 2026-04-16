using Godot;

namespace Downroot.Game.Infrastructure;

public sealed class SavePathResolver
{
    public string GetSettingsPath() => "user://settings.json";
    public string GetModSettingsPath() => "user://mods.json";
    public string GetRuntimeProfilerLogPath() => "user://logs/runtime-profiler.jsonl";

    public string GetManifestPath() => "user://saves/manifest.json";

    public string GetSavePath(string slotId) => $"user://saves/{slotId}/save.json";

    public string Globalize(string godotPath) => ProjectSettings.GlobalizePath(godotPath);
}
