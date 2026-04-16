using Downroot.Core.Ids;
using Downroot.Core.Input;
using Downroot.Core.Diagnostics;
using Downroot.Game.Infrastructure;
using Downroot.Gameplay.Runtime;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace Downroot.Game.Runtime;

public partial class GameRoot : Node2D
{
    private const bool EnableRuntimeProfiler = true;
    private GameRuntime? _runtime;
    private GameSimulation? _simulation;
    private IInputService? _inputService;
    private TextureContentLoader? _textureLoader;
    private PlayerAnimationFactory? _animationFactory;
    private StartupOverlayController? _startupOverlay;
    private WorldRenderer? _worldRenderer;
    private WorldLightController? _lightingPresentationController;
    private HudController? _hudController;
    private CanvasLayer? _travelOverlayLayer;
    private ColorRect? _travelOverlay;
    private DebugRuntimeState? _debugState;
    private DebugPanelController? _debugPanel;
    private Action? _saveAction;
    private Action? _reloadAction;
    private bool _initialized;
    private bool _previousDebugToggleHeld;
    private bool _previousManualSaveHeld;

    public GameRuntime Runtime => _runtime ?? throw new InvalidOperationException("GameRoot has not been configured.");
    public GameSimulation Simulation => _simulation ?? throw new InvalidOperationException("GameRoot has not been configured.");

    public void Configure(GameRuntime runtime, DebugRuntimeState debugState, Action saveAction, Action reloadAction)
    {
        if (_initialized || _runtime is not null)
        {
            throw new InvalidOperationException("GameRoot has already been configured.");
        }

        _runtime = runtime;
        _debugState = debugState;
        _saveAction = saveAction;
        _reloadAction = reloadAction;
    }

    public override void _Ready()
    {
        try
        {
            _startupOverlay = new StartupOverlayController(this);
            _startupOverlay.Show("Configuring input");
            GameInputMapInstaller.Install();

            _startupOverlay.UpdateStatus("Bootstrapping runtime");
            RuntimeProfiler.Enabled = EnableRuntimeProfiler;
            var savePathResolver = new SavePathResolver();
            RuntimeProfiler.Configure(
                savePathResolver.Globalize(savePathResolver.GetRuntimeProfilerLogPath()),
                TimeSpan.FromSeconds(5));
            if (_runtime is null)
            {
                throw new InvalidOperationException("GameRoot requires a preconfigured runtime.");
            }

            _simulation = new GameSimulation(_runtime, _debugState);

            _startupOverlay.UpdateStatus("Resolving content root");
            var packPathResolver = new PackPathResolver();
            _textureLoader = new TextureContentLoader(packPathResolver);
            _animationFactory = new PlayerAnimationFactory(packPathResolver);
            GD.Print("Content root resolved.");

            _startupOverlay.UpdateStatus("Creating HUD");
            _hudController = new HudController(this, _textureLoader);
            _hudController.Initialize(_simulation);

            _inputService = new GodotInputService(() =>
            {
                var pointer = GetGlobalMousePosition();
                return new NumericsVector2(pointer.X, pointer.Y);
            }, () => _hudController.IsPointerOverBlockingUi(GetViewport().GetMousePosition()));

            _startupOverlay.UpdateStatus("Creating world renderer");
            _worldRenderer = new WorldRenderer(_textureLoader, _animationFactory);
            AddChild(_worldRenderer);
            _worldRenderer.Initialize(_runtime);
            _lightingPresentationController = new WorldLightController();
            AddChild(_lightingPresentationController);
            _lightingPresentationController.Initialize(_runtime);
            InitializeTravelOverlay();
            _debugPanel = new DebugPanelController(this, _debugState!, new DebugCommandExecutor(_runtime, _debugState!, () => _saveAction?.Invoke(), () => _reloadAction?.Invoke()));

            _startupOverlay.UpdateStatus("Validating content");
            _worldRenderer.ValidateContentLoads(_runtime);

            _worldRenderer.Update(new InputFrame(default, default, false, false, false, false, false, false, 0, null));
            _hudController.Refresh(_runtime, _worldRenderer.WorldToScreen);
            _startupOverlay.Hide();
            _initialized = true;
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _startupOverlay ??= new StartupOverlayController(this);
            _startupOverlay.ShowError(exception);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_runtime is null || _simulation is null || _inputService is null || _worldRenderer is null || _hudController is null)
        {
            return;
        }

        RuntimeProfiler.BeginFrame();
        using var frameScope = RuntimeProfiler.Measure("GameRoot.PhysicsProcess");
        var debugToggleHeld = Input.IsKeyPressed(Key.F9);
        if (debugToggleHeld && !_previousDebugToggleHeld)
        {
            _debugPanel?.ToggleVisibility();
        }
        _previousDebugToggleHeld = debugToggleHeld;

        var manualSaveHeld = Input.IsKeyPressed(Key.F5);
        if (manualSaveHeld && !_previousManualSaveHeld)
        {
            _saveAction?.Invoke();
            GD.Print("[Save] Manual save completed.");
        }
        _previousManualSaveHeld = manualSaveHeld;

        var frame = _inputService.CaptureFrame();
        using (RuntimeProfiler.Measure("GameRoot.Simulation"))
        {
            _simulation.Tick((float)delta, frame);
        }

        using (RuntimeProfiler.Measure("GameRoot.Renderer"))
        {
            _worldRenderer.Update(frame);
            _lightingPresentationController?.UpdateLighting();
        }

        using (RuntimeProfiler.Measure("GameRoot.Hud"))
        {
            _hudController.Refresh(_runtime, _worldRenderer.WorldToScreen);
        }

        using (RuntimeProfiler.Measure("GameRoot.Overlay"))
        {
            UpdateTravelOverlay();
        }

        _worldRenderer.SetShowChunkBounds(_debugState?.ShowChunkBounds ?? false);
        _debugPanel?.Refresh();

        RuntimeProfiler.EndFrame();
    }

    public override void _ExitTree()
    {
        RuntimeProfiler.FlushNow();
    }

    private void InitializeTravelOverlay()
    {
        _travelOverlayLayer = new CanvasLayer();
        _travelOverlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _travelOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _travelOverlay.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        _travelOverlayLayer.AddChild(_travelOverlay);
        AddChild(_travelOverlayLayer);
    }

    private void UpdateTravelOverlay()
    {
        if (_runtime is null || _travelOverlay is null)
        {
            return;
        }

        var alpha = _runtime.WorldState.Travel.OverlayAlpha01;
        _travelOverlay.Color = new Color(0f, 0f, 0f, alpha);
        _travelOverlay.Visible = alpha > 0f;
    }
}
