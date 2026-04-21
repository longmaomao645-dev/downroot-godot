using Downroot.Core.Ids;
using Downroot.Core.Diagnostics;
using Downroot.Game.Infrastructure;
using Downroot.Gameplay.Runtime;
using Downroot.UI.Presentation;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace Downroot.Game.Runtime;

public sealed class HudController
{
    private readonly Node _host;
    private readonly TextureContentLoader _textureLoader;
    private readonly GamePresentationBuilder _builder = new();
    private readonly HudLayoutResolver _layoutResolver = new();
    private readonly Dictionary<string, Texture2D> _itemIconCache = [];
    private readonly HudView _view = new();
    private readonly Dictionary<ContentId, HudView.RecipeRowParts> _recipeRows = [];
    private GameSimulation? _simulation;
    private HudStatusViewData? _lastHudStatus;
    private InteractionPromptViewData? _lastInteractionPrompt;
    private StatusBannerViewData? _lastStatusBanner;
    private DestroyProgressViewData? _lastDestroyProgress;
    private int? _lastHotbarSignature;
    private int? _lastInventorySignature;
    private int? _lastStorageSignature;
    private string? _recipeStructureKey;
    private string? _recipeStateKey;
    private string? _lastLayoutKey;
    private CraftingPanelViewData? _cachedCraftingPanel;
    private Control? _tooltipSource;

    public HudController(Node host, TextureContentLoader textureLoader)
    {
        _host = host;
        _textureLoader = textureLoader;
    }

    public HudView View => _view;

    public bool IsPointerOverBlockingUi(Vector2 screenPosition)
    {
        return _view.IsPointerOverBlockingUi(screenPosition);
    }

    public void Initialize(GameSimulation simulation)
    {
        _simulation = simulation;
        _host.AddChild(_view);
        for (var index = 0; index < _view.InventorySlots.Count; index++)
        {
            var inventoryIndex = index;
            _view.InventorySlots[index].SlotRoot.GuiInput += @event => OnInventorySlotInput(inventoryIndex, @event);
            BindTooltipLifecycle(_view.InventorySlots[index].SlotRoot);
        }

        for (var index = 0; index < _view.StorageSlots.Count; index++)
        {
            var storageIndex = index;
            _view.StorageSlots[index].SlotRoot.GuiInput += @event => OnStorageSlotInput(storageIndex, @event);
            BindTooltipLifecycle(_view.StorageSlots[index].SlotRoot);
        }

        for (var index = 0; index < _view.HotbarSlots.Count; index++)
        {
            BindTooltipLifecycle(_view.HotbarSlots[index].SlotRoot);
        }
    }

    public void Refresh(GameRuntime runtime, Func<NumericsVector2, Vector2> worldToScreen)
    {
        using var refreshScope = RuntimeProfiler.Measure("HudController.Refresh");
        using (RuntimeProfiler.Measure("HudController.HudStatus"))
        {
            RefreshHudStatus(runtime);
        }

        using (RuntimeProfiler.Measure("HudController.Hotbar"))
        {
            RefreshHotbar(runtime);
        }

        using (RuntimeProfiler.Measure("HudController.Crafting"))
        {
            RefreshCraftingPanel(runtime);
        }

        using (RuntimeProfiler.Measure("HudController.InteractionPrompt"))
        {
            RefreshInteractionPrompt(runtime);
        }

        using (RuntimeProfiler.Measure("HudController.StatusBanner"))
        {
            RefreshStatusBanner(runtime);
        }

        using (RuntimeProfiler.Measure("HudController.Layout"))
        {
            RefreshLayout(runtime);
        }

        using (RuntimeProfiler.Measure("HudController.DestroyProgress"))
        {
            RefreshDestroyProgress(runtime, worldToScreen);
        }

        RefreshTooltip(runtime);
    }

    private void RefreshHudStatus(GameRuntime runtime)
    {
        var hudStatus = _builder.BuildHudStatus(runtime);
        if (_lastHudStatus == hudStatus)
        {
            return;
        }

        _view.TimeOfDayLabel.Text = hudStatus.TimeOfDayLabel;
        _view.NightOverlay.Color = new Color(0.03f, 0.05f, 0.15f, hudStatus.NightOverlayAlpha);
        _view.HitOverlay.Color = new Color(0.85f, 0.08f, 0.08f, hudStatus.PlayerHitFlashAlpha);
        _view.SetBarValue(_view.HealthBarWidget, hudStatus.HealthPercent);
        _view.SetBarValue(_view.HungerBarWidget, hudStatus.HungerPercent);
        _lastHudStatus = hudStatus;
    }

    private void RefreshHotbar(GameRuntime runtime)
    {
        var signature = ComputeSlotSignature(runtime, runtime.Player.HotbarSize, includeSelection: true);
        if (_lastHotbarSignature == signature)
        {
            return;
        }

        var hotbar = _builder.BuildHotbar(runtime);
        for (var index = 0; index < _view.HotbarSlots.Count; index++)
        {
            var slotView = hotbar[index];
            _view.SetSlot(_view.HotbarSlots[index], ResolveItemIcon(slotView.ItemId, runtime), slotView.Quantity, slotView.IsSelected);
            SetTooltipData(
                _view.HotbarSlots[index].SlotRoot,
                slotView.ItemId,
                ResolveItemName(slotView.ItemId, runtime),
                slotView.Quantity > 0 ? $"Quantity: {slotView.Quantity}" : "Empty slot");
        }

        _lastHotbarSignature = signature;
    }

    private CraftingPanelViewData RefreshCraftingPanel(GameRuntime runtime)
    {
        var workspaceMode = runtime.WorldState.WorkspaceMode;
        var storageActive = runtime.WorldState.ActiveStorageEntityId is not null;
        var isVisible = workspaceMode != CraftWorkspaceMode.Hidden || storageActive;
        _view.CraftWorkspacePanel.Visible = isVisible;
        if (!isVisible)
        {
            _recipeStructureKey = null;
            _recipeStateKey = null;
            _lastInventorySignature = null;
            _lastStorageSignature = null;
            _view.StorageRegion.Visible = false;
            _cachedCraftingPanel = new CraftingPanelViewData(false, "Handcraft", CraftModeIconKind.Handcraft, [], [], string.Empty, []);
            return _cachedCraftingPanel;
        }

        var inventorySignature = ComputeSlotSignature(runtime, 16, includeSelection: true);
        var storageSignature = ComputeStorageSignature(runtime);
        var recipeIds = _simulation!.GetRecipesForWorkspace(workspaceMode).Select(recipe => recipe.Id.Value).ToArray();
        var recipeStructureKey = $"{workspaceMode}:{string.Join(',', recipeIds)}";
        var recipeStateKey = $"{workspaceMode}:{inventorySignature}:{storageSignature}:{runtime.WorldState.ActiveFurnaceTask?.RecipeId.Value ?? string.Empty}:{runtime.WorldState.ActiveFurnaceTask is not null}";
        var requiresPanelRebuild = _cachedCraftingPanel is null
            || _recipeStructureKey != recipeStructureKey
            || _recipeStateKey != recipeStateKey
            || _lastInventorySignature != inventorySignature
            || _lastStorageSignature != storageSignature;
        var panelViewData = requiresPanelRebuild
            ? _builder.BuildCraftingPanel(runtime, _simulation!)
            : _cachedCraftingPanel!;
        _cachedCraftingPanel = panelViewData;
        storageSignature = ComputeStorageSignature(runtime);

        _view.CraftModeLabel.Text = panelViewData.CraftModeLabel;
        _view.CraftModeIcon.Texture = _view.CreateCraftModeIcon(panelViewData.CraftModeIcon);
        _view.StorageRegion.Visible = panelViewData.StorageSlots.Count > 0;
        _view.StorageTitleLabel.Text = string.IsNullOrWhiteSpace(panelViewData.StorageTitle)
            ? "Storage"
            : panelViewData.StorageTitle;

        if (_lastInventorySignature != inventorySignature)
        {
            for (var index = 0; index < _view.InventorySlots.Count; index++)
            {
                var slotView = panelViewData.InventorySlots[index];
                _view.SetSlot(_view.InventorySlots[index], ResolveItemIcon(slotView.ItemId, runtime), slotView.Quantity, false);
                _view.InventorySlots[index].SlotRoot.TooltipText = panelViewData.StorageSlots.Count > 0
                    ? "Click to move into storage"
                    : index == runtime.Player.SelectedHotbarIndex
                        ? "Current hand slot"
                        : $"Click to move into hotbar slot {runtime.Player.SelectedHotbarIndex + 1}";
                SetTooltipData(
                    _view.InventorySlots[index].SlotRoot,
                    slotView.ItemId,
                    ResolveItemName(slotView.ItemId, runtime),
                    slotView.Quantity > 0 ? $"Quantity: {slotView.Quantity}" : _view.InventorySlots[index].SlotRoot.TooltipText);
            }

            _lastInventorySignature = inventorySignature;
        }

        if (_lastStorageSignature != storageSignature)
        {
            for (var index = 0; index < _view.StorageSlots.Count; index++)
            {
                var slotView = index < panelViewData.StorageSlots.Count
                    ? panelViewData.StorageSlots[index]
                    : new InventorySlotViewData(null, 0);
                _view.SetSlot(_view.StorageSlots[index], ResolveItemIcon(slotView.ItemId, runtime), slotView.Quantity, false);
                _view.StorageSlots[index].SlotRoot.TooltipText = panelViewData.StorageSlots.Count > 0
                    ? "Click to move into inventory"
                    : string.Empty;
                SetTooltipData(
                    _view.StorageSlots[index].SlotRoot,
                    slotView.ItemId,
                    ResolveItemName(slotView.ItemId, runtime),
                    slotView.Quantity > 0 ? $"Quantity: {slotView.Quantity}" : _view.StorageSlots[index].SlotRoot.TooltipText);
            }

            _lastStorageSignature = storageSignature;
        }

        if (_recipeStructureKey != recipeStructureKey)
        {
            RebuildRecipeList(panelViewData, runtime);
            _recipeStructureKey = recipeStructureKey;
            _recipeStateKey = null;
        }

        if (_recipeStateKey != recipeStateKey)
        {
            RefreshRecipeRows(panelViewData, runtime);
            _recipeStateKey = recipeStateKey;
        }

        RefreshRecipeProgress(runtime);
        return panelViewData;
    }

    private void RefreshInteractionPrompt(GameRuntime runtime)
    {
        var interactionPrompt = _builder.BuildInteractionPrompt(runtime);
        if (_lastInteractionPrompt == interactionPrompt)
        {
            return;
        }

        _view.ContextPromptPanel.Visible = interactionPrompt.IsVisible;
        _view.PromptKeyLabel.Text = interactionPrompt.PromptKeyLabel;
        _view.PromptVerbIcon.Texture = _view.CreatePromptIcon(interactionPrompt.PromptIconKind);
        _view.PromptVerbLabel.Text = interactionPrompt.PromptVerbLabel;
        _view.PromptTargetLabel.Text = interactionPrompt.PromptTargetLabel;
        _lastLayoutKey = null;
        _lastInteractionPrompt = interactionPrompt;
    }

    private void RefreshStatusBanner(GameRuntime runtime)
    {
        var statusBanner = _builder.BuildStatusBanner(runtime);
        if (_lastStatusBanner == statusBanner)
        {
            return;
        }

        _view.StatusBanner.Visible = statusBanner.IsVisible;
        _view.StatusMessageLabel.Text = statusBanner.StatusMessageLabel;
        _lastStatusBanner = statusBanner;
    }

    private void RefreshLayout(GameRuntime runtime)
    {
        var viewportSize = _host.GetViewport().GetVisibleRect().Size;
        var layoutKey = $"{viewportSize.X}:{viewportSize.Y}:{_view.CraftWorkspacePanel.Visible}:{_view.StorageRegion.Visible}:{_view.StatusBanner.Visible}:{_view.ContextPromptPanel.Visible}";
        if (_lastLayoutKey == layoutKey)
        {
            return;
        }

        _layoutResolver.Apply(_view, viewportSize);
        _lastLayoutKey = layoutKey;
    }

    private void RefreshDestroyProgress(GameRuntime runtime, Func<NumericsVector2, Vector2> worldToScreen)
    {
        var destroyProgress = _builder.BuildDestroyProgress(runtime);
        _view.DestroyProgressPanel.Visible = destroyProgress.IsVisible;
        if (destroyProgress != _lastDestroyProgress)
        {
            if (destroyProgress.IsVisible)
            {
                _view.DestroyTargetLabel.Text = destroyProgress.DestroyTargetLabel;
                _view.SetBarValue(_view.DestroyProgressWidget, destroyProgress.Progress01);
            }

            _lastDestroyProgress = destroyProgress;
        }

        if (destroyProgress.IsVisible)
        {
            var screenPosition = worldToScreen(destroyProgress.WorldPosition);
            _view.DestroyProgressPanel.Position = _layoutResolver.ResolveDestroyPanelPosition(
                _view,
                _host.GetViewport().GetVisibleRect().Size,
                screenPosition);
        }
    }

    private void RebuildRecipeList(CraftingPanelViewData panelViewData, GameRuntime runtime)
    {
        foreach (var child in _view.RecipeListContainer.GetChildren())
        {
            child.QueueFree();
        }
        _recipeRows.Clear();

        if (!panelViewData.IsVisible)
        {
            return;
        }

        foreach (var recipe in panelViewData.Recipes)
        {
            var row = _view.CreateRecipeRow(recipe, OnCraftRequested);
            SyncCostChips(row, recipe, runtime);
            BindTooltipLifecycle(row.RecipeResultIcon);
            SetTooltipData(row.RecipeResultIcon, recipe.ResultItemId, recipe.RecipeName, recipe.ActionLabel);
            _view.ApplyRecipeRowState(row, recipe.CanCraft, recipe.IsRunning);

            _view.RecipeListContainer.AddChild(row.RowRoot);
            _recipeRows[recipe.RecipeId] = row;
        }
    }

    private void RefreshRecipeRows(CraftingPanelViewData panelViewData, GameRuntime runtime)
    {
        foreach (var recipe in panelViewData.Recipes)
        {
            if (!_recipeRows.TryGetValue(recipe.RecipeId, out var row))
            {
                continue;
            }

            row.RecipeResultIcon.Texture = ResolveItemIcon(recipe.ResultItemId, runtime);
            row.RecipeNameLabel.Text = recipe.RecipeName;
            row.RecipeNameLabel.TooltipText = recipe.RecipeName;
            row.RecipeNameLabel.Modulate = recipe.CanCraft ? Colors.White : new Color(0.72f, 0.72f, 0.72f);
            SetTooltipData(row.RecipeResultIcon, recipe.ResultItemId, recipe.RecipeName, recipe.ActionLabel);

            SyncCostChips(row, recipe, runtime);

            row.RecipeCraftButton.Disabled = !recipe.CanCraft || recipe.IsRunning;
            row.RecipeCraftButton.Text = recipe.ActionLabel;
            row.RecipeProgressWidget.BarRoot.Visible = recipe.IsRunning || recipe.ActionLabel == "Smelt";
            _view.SetBarValue(row.RecipeProgressWidget, recipe.Progress01);
            row.RecipeUnavailableMask.Visible = !recipe.CanCraft;
            _view.ApplyRecipeRowState(row, recipe.CanCraft, recipe.IsRunning);
        }
    }

    private void RefreshRecipeProgress(GameRuntime runtime)
    {
        foreach (var row in _recipeRows.Values)
        {
            row.RecipeProgressWidget.BarRoot.Visible = false;
            _view.SetBarValue(row.RecipeProgressWidget, 0f);
        }

        var task = runtime.WorldState.ActiveFurnaceTask;
        if (task is null || !_recipeRows.TryGetValue(task.RecipeId, out var activeRow))
        {
            return;
        }

        activeRow.RecipeProgressWidget.BarRoot.Visible = true;
        _view.SetBarValue(activeRow.RecipeProgressWidget, task.Progress01);
    }

    private void SyncCostChips(HudView.RecipeRowParts row, CraftRecipeViewData recipe, GameRuntime runtime)
    {
        if (row.RecipeCostContainer.GetChildCount() != recipe.Costs.Count)
        {
            foreach (var child in row.RecipeCostContainer.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var cost in recipe.Costs)
            {
                row.RecipeCostContainer.AddChild(_view.CreateCostChip(cost, ResolveItemIcon(cost.ItemId, runtime)));
            }

            for (var index = 0; index < recipe.Costs.Count; index++)
            {
                if (row.RecipeCostContainer.GetChild(index) is Control chip)
                {
                    BindTooltipLifecycle(chip);
                    SetTooltipData(
                        chip,
                        recipe.Costs[index].ItemId,
                        recipe.Costs[index].ItemName,
                        recipe.Costs[index].IsSatisfied
                            ? $"Need x{recipe.Costs[index].Amount}"
                            : $"Missing {recipe.Costs[index].MissingAmount} of {recipe.Costs[index].Amount}");
                }
            }

            return;
        }

        for (var index = 0; index < recipe.Costs.Count; index++)
        {
            var cost = recipe.Costs[index];
            var chip = (Control)row.RecipeCostContainer.GetChild(index);
            chip.TooltipText = cost.IsSatisfied
                ? $"{cost.ItemName} x{cost.Amount}"
                : $"{cost.ItemName}: missing {cost.MissingAmount}";

            if (chip.GetChild(0) is not HBoxContainer chipRow)
            {
                continue;
            }

            if (chipRow.GetChildCount() >= 2)
            {
                if (chipRow.GetChild(0) is TextureRect icon)
                {
                    icon.Texture = ResolveItemIcon(cost.ItemId, runtime);
                }

                if (chipRow.GetChild(1) is Label amountLabel)
                {
                    amountLabel.Text = $"x{cost.Amount}";
                    amountLabel.Modulate = cost.IsSatisfied ? new Color(0.74f, 0.92f, 0.74f) : new Color(0.96f, 0.62f, 0.62f);
                }
            }

            SetTooltipData(
                chip,
                cost.ItemId,
                cost.ItemName,
                cost.IsSatisfied
                    ? $"Need x{cost.Amount}"
                    : $"Missing {cost.MissingAmount} of {cost.Amount}");
        }
    }

    private static int ComputeSlotSignature(GameRuntime runtime, int slotCount, bool includeSelection)
    {
        var hash = new HashCode();
        hash.Add(slotCount);
        if (includeSelection)
        {
            hash.Add(runtime.Player.SelectedHotbarIndex);
        }

        for (var index = 0; index < slotCount; index++)
        {
            var slot = runtime.Player.Inventory.Slots[index];
            hash.Add(slot.ItemId?.Value);
            hash.Add(slot.Quantity);
        }

        return hash.ToHashCode();
    }

    private Texture2D? ResolveItemIcon(ContentId? itemId, GameRuntime runtime)
    {
        if (itemId is null)
        {
            return null;
        }

        var key = itemId.Value.Value;
        if (_itemIconCache.TryGetValue(key, out var texture))
        {
            return texture;
        }

        texture = _textureLoader.LoadItem(runtime.Content.Items.Get(itemId.Value)).Texture;
        _itemIconCache[key] = texture;
        return texture;
    }

    private void OnCraftRequested(ContentId recipeId)
    {
        GD.Print($"[Craft UI] Clicked {recipeId.Value}");
        if (!_simulation!.TryCraft(recipeId, out var failureReason))
        {
            GD.Print($"[Craft UI] Blocked {recipeId.Value}: {failureReason}");
        }

        _recipeStateKey = null;
        _host.GetViewport().GuiReleaseFocus();
    }

    private void OnInventorySlotInput(int inventoryIndex, InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (_simulation is null)
        {
            return;
        }

        if (_cachedCraftingPanel?.StorageSlots.Count > 0)
        {
            _simulation.MoveInventorySlotToStorage(inventoryIndex);
        }
        else
        {
            _simulation!.MoveInventorySlotToSelectedHotbar(inventoryIndex);
        }

        _recipeStateKey = null;
        _host.GetViewport().GuiReleaseFocus();
    }

    private void OnStorageSlotInput(int storageIndex, InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (_simulation is null)
        {
            return;
        }

        _simulation!.MoveStorageSlotToInventory(storageIndex);
        _recipeStateKey = null;
        _host.GetViewport().GuiReleaseFocus();
    }

    private static int ComputeStorageSignature(GameRuntime runtime)
    {
        if (runtime.WorldState.ActiveStorageEntityId is not { } storageId
            || !runtime.WorldState.GetActiveWorld().TryGetEntity(storageId, out var storageEntity)
            || storageEntity.StorageInventory is null)
        {
            return 0;
        }

        var storageHash = new HashCode();
        foreach (var slot in storageEntity.StorageInventory.Slots)
        {
            storageHash.Add(slot.ItemId?.Value);
            storageHash.Add(slot.Quantity);
        }

        return storageHash.ToHashCode();
    }

    private void BindTooltipLifecycle(Control control)
    {
        control.MouseEntered += () => _tooltipSource = control;
        control.MouseExited += () =>
        {
            if (_tooltipSource == control)
            {
                _tooltipSource = null;
            }
        };
    }

    private void SetTooltipData(Control control, ContentId? itemId, string title, string detail)
    {
        control.SetMeta("tooltip_item_id", itemId?.Value ?? string.Empty);
        control.SetMeta("tooltip_title", title);
        control.SetMeta("tooltip_detail", detail);
    }

    private void RefreshTooltip(GameRuntime runtime)
    {
        if (_tooltipSource is null || !GodotObject.IsInstanceValid(_tooltipSource) || !_tooltipSource.IsVisibleInTree())
        {
            _tooltipSource = null;
            _view.HideTooltip();
            return;
        }

        var tooltipSource = _tooltipSource;
        var title = tooltipSource.GetMeta("tooltip_title", string.Empty).AsString();
        var detail = tooltipSource.GetMeta("tooltip_detail", string.Empty).AsString();
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(detail))
        {
            _view.HideTooltip();
            return;
        }

        var itemIdValue = tooltipSource.GetMeta("tooltip_item_id", string.Empty).AsString();
        var itemId = string.IsNullOrWhiteSpace(itemIdValue) ? (ContentId?)null : new ContentId(itemIdValue);
        _view.ShowTooltip(
            itemId is null ? null : ResolveItemIcon(itemId, runtime),
            title,
            detail,
            _host.GetViewport().GetMousePosition(),
            _host.GetViewport().GetVisibleRect().Size);
    }

    private string ResolveItemName(ContentId? itemId, GameRuntime runtime)
    {
        if (itemId is null)
        {
            return "Empty";
        }

        if (!runtime.Content.Items.TryGet(itemId.Value, out var item))
        {
            return itemId.Value.Value;
        }

        return item!.DisplayName;
    }
}
