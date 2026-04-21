using Downroot.Core.Save;
using Downroot.Game.Infrastructure;
using Downroot.Gameplay.Bootstrap;
using Downroot.UI.Presentation;
using Godot;

namespace Downroot.Game.Runtime;

public partial class AppRoot : Control
{
    private static readonly bool EnablePauseInputLogging = true;
    private SavePathResolver? _paths;
    private JsonFileStore? _store;
    private SaveGameRepository? _saves;
    private SettingsRepository? _settings;
    private ModSettingsRepository? _mods;
    private SettingsApplier? _settingsApplier;
    private SessionController? _session;
    private MainMenuController? _mainMenu;
    private NewGameController? _newGame;
    private LoadGameController? _loadGame;
    private ModManagementController? _modManagement;
    private SettingsController? _settingsPage;
    private CanvasLayer? _pageLayer;
    private Control? _pageHost;
    private bool _pauseMenuActive;
    private bool _shutdownSaveCompleted;
    private Control? _currentPage;

    public override void _Ready()
    {
        ProcessMode = Node.ProcessModeEnum.Always;
        _paths = new SavePathResolver();
        _store = new JsonFileStore(_paths);
        _saves = new SaveGameRepository(_paths, _store);
        _settings = new SettingsRepository(_paths, _store);
        _mods = new ModSettingsRepository(_paths, _store);
        _settingsApplier = new SettingsApplier();
        _settingsApplier.Apply(_settings.Load());
        GameInputMapInstaller.Install();

        _pageLayer = new CanvasLayer
        {
            Name = "AppPageLayer",
            ProcessMode = Node.ProcessModeEnum.Always,
            Layer = 100
        };
        AddChild(_pageLayer);
        _pageHost = new Control
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Name = "AppPageHost",
            ProcessMode = Node.ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _pageLayer.AddChild(_pageHost);
        _session = new SessionController(this, _saves);

        _mainMenu = new MainMenuController();
        _mainMenu.ContinueRequested += HandleContinueRequested;
        _mainMenu.NewGameRequested += HandleNewGameRequested;
        _mainMenu.QuickStartRequested += HandleQuickStartRequested;
        _mainMenu.LoadGameRequested += HandleLoadGameRequested;
        _mainMenu.ModManagementRequested += HandleModManagementRequested;
        _mainMenu.SettingsRequested += HandleSettingsRequested;
        _mainMenu.QuitRequested += HandleQuitRequested;

        _newGame = new NewGameController();
        _newGame.CreateRequested += CreateNewGame;
        _newGame.BackRequested += () => ShowMainMenu();

        _loadGame = new LoadGameController();
        _loadGame.LoadRequested += LoadSlot;
        _loadGame.DeleteRequested += DeleteSlot;
        _loadGame.BackRequested += () => ShowMainMenu();

        _modManagement = new ModManagementController(_mods);
        _modManagement.BackRequested += () => ShowMainMenu();

        _settingsPage = new SettingsController();
        _settingsPage.ApplyRequested += ApplySettings;
        _settingsPage.BackRequested += HandleSettingsBackRequested;

        ShowMainMenu();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            return;
        }

        LogPauseInput($"Esc received paused={GetTree().Paused} pageVisible={_pageHost?.Visible} pauseMenu={_pauseMenuActive} page={_currentPage?.Name ?? "<none>"}");

        if (_session?.GameRoot is null)
        {
            LogPauseInput("Esc ignored because no active session exists.");
            return;
        }

        if (!_pageHost!.Visible)
        {
            LogPauseInput("Esc opening pause menu.");
            ShowPauseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_pauseMenuActive && ReferenceEquals(_currentPage, _mainMenu?.View))
        {
            LogPauseInput("Esc resuming from pause menu.");
            ResumeSession();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_pauseMenuActive)
        {
            LogPauseInput("Esc returning to pause root page.");
            ShowPauseMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ShowMainMenu(string? errorMessage = null)
    {
        GetTree().Paused = false;
        _pauseMenuActive = false;
        _session!.Stop(saveBeforeClose: true);
        _pageHost!.Visible = true;
        _mainMenu!.Bind(new MainMenuViewData
        {
            CanContinue = _saves!.LoadManifest().LastPlayedSlotId is not null,
            CanLoadGame = _saves.ListSlots().Count > 0,
            ErrorMessage = errorMessage,
            VersionLabel = $"v{ProjectSettings.GetSetting("application/config/version", "0.4")}"
        });
        ShowPage(_mainMenu.View);
    }

    private void ShowPauseMenu()
    {
        if (_session?.GameRoot is null)
        {
            LogPauseInput("ShowPauseMenu skipped because session is missing.");
            return;
        }

        LogPauseInput("ShowPauseMenu applying paused state.");
        GetTree().Paused = true;
        _pauseMenuActive = true;
        _pageHost!.Visible = true;
        _mainMenu!.BindPauseMenu(_session.CurrentSlotId is not null);
        ShowPage(_mainMenu.View);
    }

    private void ResumeSession()
    {
        LogPauseInput("ResumeSession clearing paused state.");
        _pauseMenuActive = false;
        _pageHost!.Visible = false;
        GetTree().Paused = false;
    }

    private void ShowLoadGame()
    {
        _loadGame!.Bind(_saves!.ListSlots().Select(slot => new SaveSlotViewData
        {
            SlotId = slot.SlotId,
            DisplayName = slot.DisplayName,
            WorldSeed = slot.WorldSeed,
            EnabledPackIds = slot.EnabledPackIds,
            CurrentWorldSpace = slot.CurrentWorldSpace,
            PlayerHealth = slot.PlayerHealth,
            PlayerHunger = slot.PlayerHunger,
            LastWriteUtc = slot.LastWriteUtc
        }).ToArray());
        ShowPage(_loadGame.View);
    }

    private void ShowLoadGame(string? errorMessage)
    {
        _loadGame!.Bind(_saves!.ListSlots().Select(slot => new SaveSlotViewData
        {
            SlotId = slot.SlotId,
            DisplayName = slot.DisplayName,
            WorldSeed = slot.WorldSeed,
            EnabledPackIds = slot.EnabledPackIds,
            CurrentWorldSpace = slot.CurrentWorldSpace,
            PlayerHealth = slot.PlayerHealth,
            PlayerHunger = slot.PlayerHunger,
            LastWriteUtc = slot.LastWriteUtc
        }).ToArray(), errorMessage);
        ShowPage(_loadGame.View);
    }

    private void ShowModManagement()
    {
        _modManagement!.Bind();
        ShowPage(_modManagement.View);
    }

    private void ShowSettings()
    {
        var current = _settings!.Load();
        _settingsPage!.Bind(new SettingsViewData
        {
            Fullscreen = current.Fullscreen,
            VSync = current.VSync,
            MasterVolume = current.MasterVolume,
            UiScale = current.UiScale
        });
        ShowPage(_settingsPage.View);
    }

    private void ContinueLastSave()
    {
        var last = _saves!.LoadManifest().LastPlayedSlotId;
        if (string.IsNullOrWhiteSpace(last))
        {
            return;
        }

        LoadSlot(last);
    }

    private void QuickStart()
    {
        var displayName = $"Quick Start {DateTime.Now:yyyy-MM-dd HHmmss}";
        var slotId = _saves!.CreateSlotId(displayName);
        StartSession(new GameBootstrapRequest
        {
            StartOptions = new GameStartOptions
            {
                SaveSlotId = slotId,
                DisplayName = displayName,
                WorldSeed = Random.Shared.Next(),
                EnabledPackIds = _mods!.Load().EnabledPackIds,
                IsNewGame = true
            }
        });
    }

    private void CreateNewGame(string displayName, string seedText)
    {
        var resolvedName = string.IsNullOrWhiteSpace(displayName) ? "New World" : displayName.Trim();
        var slotId = _saves!.CreateSlotId(resolvedName);
        StartSession(new GameBootstrapRequest
        {
            StartOptions = new GameStartOptions
            {
                SaveSlotId = slotId,
                DisplayName = resolvedName,
                WorldSeed = ResolveSeed(seedText),
                EnabledPackIds = _mods!.Load().EnabledPackIds,
                IsNewGame = true
            }
        });
    }

    private void LoadSlot(string slotId)
    {
        var save = _saves!.LoadSave(slotId);
        if (save is null)
        {
            ShowLoadGame();
            return;
        }

        StartSession(new GameBootstrapRequest
        {
            StartOptions = new GameStartOptions
            {
                SaveSlotId = save.SlotId,
                DisplayName = save.DisplayName,
                WorldSeed = save.WorldSeed,
                EnabledPackIds = save.Mods.EnabledPackIds,
                IsNewGame = false
            },
            ExistingSave = save
        });
    }

    private void DeleteSlot(string slotId)
    {
        _saves!.DeleteSave(slotId);
        ShowLoadGame();
    }

    private void ApplySettings(GameSettingsData settings)
    {
        _settings!.Save(settings);
        _settingsApplier!.Apply(settings);
    }

    private void StartSession(GameBootstrapRequest request)
    {
        GetTree().Paused = false;
        _pauseMenuActive = false;
        _pageHost!.Visible = false;
        if (!_session!.Start(request))
        {
            _pageHost.Visible = true;
            if (request.ExistingSave is null)
            {
                ShowMainMenu(_session.LastStartError);
                return;
            }

            ShowLoadGame(FormatLoadFailure(request.ExistingSave, _session.LastStartError));
        }
    }

    private static string FormatLoadFailure(SaveGameData save, string? reason)
    {
        var requiredMods = save.Mods.EnabledPackIds.Count == 0
            ? "basegame"
            : string.Join(", ", save.Mods.EnabledPackIds);
        var detail = string.IsNullOrWhiteSpace(reason) ? "Failed to resolve the save's mod set." : reason;
        return $"{detail} Required mods: {requiredMods}. Enable the required built-in mods in Mod Management before loading this save.";
    }

    private void ShowPage(Control page)
    {
        foreach (var child in _pageHost!.GetChildren())
        {
            _pageHost.RemoveChild(child);
        }

        if (page.GetParent() is Node parent)
        {
            parent.RemoveChild(page);
        }

        _pageHost.AddChild(page);
        _currentPage = page;
    }

    private void HandleContinueRequested()
    {
        if (_pauseMenuActive)
        {
            ResumeSession();
            return;
        }

        ContinueLastSave();
    }

    private void HandleNewGameRequested()
    {
        if (_pauseMenuActive)
        {
            _session?.SaveCurrent();
            return;
        }

        ShowPage(_newGame!.View);
    }

    private void HandleQuickStartRequested()
    {
        if (_pauseMenuActive)
        {
            return;
        }

        QuickStart();
    }

    private void HandleLoadGameRequested()
    {
        if (_pauseMenuActive)
        {
            ShowMainMenu();
            return;
        }

        ShowLoadGame();
    }

    private void HandleModManagementRequested()
    {
        if (_pauseMenuActive)
        {
            return;
        }

        ShowModManagement();
    }

    private void HandleSettingsRequested()
    {
        ShowSettings();
    }

    private void HandleQuitRequested()
    {
        if (_pauseMenuActive)
        {
            _session?.Stop(saveBeforeClose: true);
        }

        GetTree().Quit();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationWMCloseRequest)
        {
            SaveSessionOnShutdown();
        }
    }

    public override void _ExitTree()
    {
        SaveSessionOnShutdown();
        base._ExitTree();
    }

    private void HandleSettingsBackRequested()
    {
        if (_pauseMenuActive)
        {
            ShowPauseMenu();
            return;
        }

        ShowMainMenu();
    }

    private static int ResolveSeed(string seedText)
    {
        if (string.IsNullOrWhiteSpace(seedText))
        {
            return Random.Shared.Next();
        }

        if (int.TryParse(seedText.Trim(), out var numeric))
        {
            return numeric;
        }

        unchecked
        {
            var hash = 17;
            foreach (var character in seedText.Trim())
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }

    private static void LogPauseInput(string message)
    {
        if (!EnablePauseInputLogging)
        {
            return;
        }

        GD.Print($"[PauseInput] {message}");
    }

    private void SaveSessionOnShutdown()
    {
        if (_shutdownSaveCompleted)
        {
            return;
        }

        _shutdownSaveCompleted = true;
        _session?.SaveCurrent();
    }
}
