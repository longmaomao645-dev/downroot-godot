using Downroot.Content.Registries;
using Downroot.Core.Definitions;
using Downroot.Core.Gameplay;
using Downroot.Core.Ids;
using Downroot.Gameplay.Runtime;
using Downroot.Gameplay.Runtime.Systems;

namespace Downroot.UI.Presentation;

public sealed class GamePresentationBuilder
{
    public GamePresentationSnapshot Build(GameRuntime runtime, GameSimulation simulation)
    {
        return new GamePresentationSnapshot(
            BuildHudStatus(runtime),
            BuildHotbar(runtime),
            BuildCraftingPanel(runtime, simulation),
            BuildInteractionPrompt(runtime),
            BuildStatusBanner(runtime),
            BuildDestroyProgress(runtime));
    }

    public HudStatusViewData BuildHudStatus(GameRuntime runtime)
    {
        var dayLength = runtime.BootstrapConfig.DayLengthSeconds;
        var isNight = runtime.WorldState.IsNight(dayLength);
        var timeProgress = TimeOfDayRules.NormalizeProgress(runtime.WorldState.TimeOfDaySeconds, dayLength);
        return new HudStatusViewData(
            FormatTimeOfDayLabel(timeProgress, runtime.WorldState.TotalElapsedSeconds, dayLength),
            isNight,
            ResolveNightOverlayAlpha(runtime.WorldState.TimeOfDaySeconds, dayLength),
            ToPercent(runtime.Player.Survival.Health, runtime.Player.Survival.MaxHealth),
            ToPercent(runtime.Player.Survival.Hunger, runtime.Player.Survival.MaxHunger),
            Math.Clamp(runtime.WorldState.PlayerHitFlashSeconds / 0.18f, 0f, 1f) * 0.62f);
    }

    public IReadOnlyList<HotbarSlotViewData> BuildHotbar(GameRuntime runtime)
    {
        return runtime.Player.Inventory.Slots
            .Take(runtime.Player.HotbarSize)
            .Select((slot, index) => new HotbarSlotViewData(slot.ItemId, slot.Quantity, index == runtime.Player.SelectedHotbarIndex))
            .ToArray();
    }

    public CraftingPanelViewData BuildCraftingPanel(GameRuntime runtime, GameSimulation simulation)
    {
        var mode = runtime.WorldState.WorkspaceMode;
        var storageSlots = BuildStorageSlots(runtime);
        var storageOnly = mode == CraftWorkspaceMode.Hidden && storageSlots.Count > 0;
        return new CraftingPanelViewData(
            mode != CraftWorkspaceMode.Hidden || storageSlots.Count > 0,
            storageOnly
                ? "Storage"
                : mode switch
            {
                CraftWorkspaceMode.Furnace => "Furnace",
                CraftWorkspaceMode.WeaponsBench => "Weapons Bench",
                CraftWorkspaceMode.Workbench => "Workbench",
                _ => "Handcraft"
            },
            storageOnly
                ? CraftModeIconKind.Handcraft
                : mode switch
            {
                CraftWorkspaceMode.Furnace => CraftModeIconKind.Furnace,
                CraftWorkspaceMode.Workbench => CraftModeIconKind.Workbench,
                CraftWorkspaceMode.WeaponsBench => CraftModeIconKind.Workbench,
                _ => CraftModeIconKind.Handcraft
            },
            mode == CraftWorkspaceMode.Hidden ? [] : BuildRecipeRows(runtime, simulation, mode),
            runtime.Player.Inventory.Slots
                .Take(16)
                .Select(slot => new InventorySlotViewData(slot.ItemId, slot.Quantity))
                .ToArray(),
            ResolveStorageTitle(runtime),
            storageSlots);
    }

    public IReadOnlyList<CraftRecipeViewData> BuildRecipeRows(GameRuntime runtime, GameSimulation simulation, CraftWorkspaceMode mode)
    {
        var activeTask = runtime.WorldState.ActiveFurnaceTask;
        return simulation.GetRecipesForWorkspace(mode)
            .Select(recipe =>
            {
                var costs = recipe.Ingredients
                    .Select(ingredient =>
                    {
                        var ownedAmount = runtime.Player.Inventory.Count(ingredient.ItemId);
                        var missingAmount = Math.Max(0, ingredient.Amount - ownedAmount);
                        return new RecipeCostViewData(
                            ingredient.ItemId,
                            ResolveItemName(runtime.Content, ingredient.ItemId),
                            ingredient.Amount,
                            missingAmount == 0,
                            missingAmount);
                    })
                    .ToArray();

                var outputs = recipe.ExtraResults is null
                    ? new[] { recipe.Result }
                    : (new[] { recipe.Result }).Concat(recipe.ExtraResults).ToArray();
                var isRunning = activeTask?.RecipeId == recipe.Id;
                var canCraft = recipe.Ingredients.All(ingredient => runtime.Player.Inventory.Has(ingredient.ItemId, ingredient.Amount))
                    && runtime.Player.Inventory.CanAddMany(outputs, runtime.Content)
                    && (!IsFurnaceRecipe(recipe) || activeTask is null || isRunning);

                return new CraftRecipeViewData(
                    recipe.Id,
                    recipe.Result.ItemId,
                    recipe.DisplayName,
                    costs,
                    canCraft,
                    isRunning ? "Busy" : ResolveActionLabel(recipe),
                    isRunning,
                    isRunning ? activeTask!.Progress01 : 0f);
            })
            .ToArray();
    }

    public InteractionPromptViewData BuildInteractionPrompt(GameRuntime runtime)
    {
        var context = runtime.WorldState.CurrentInteraction;
        if (context is null)
        {
            return new InteractionPromptViewData(false, "F", PromptIconKind.Use, string.Empty, string.Empty);
        }

        return new InteractionPromptViewData(
            true,
            "F",
            context.Verb switch
            {
                InteractionVerb.Sleep => PromptIconKind.Use,
                InteractionVerb.SetHome => PromptIconKind.Use,
                InteractionVerb.Light => PromptIconKind.Use,
                InteractionVerb.Extinguish => PromptIconKind.Use,
                InteractionVerb.Open => PromptIconKind.Open,
                InteractionVerb.Close => PromptIconKind.Close,
                InteractionVerb.Gather => PromptIconKind.Gather,
                InteractionVerb.Eat => PromptIconKind.Eat,
                InteractionVerb.PickUp => PromptIconKind.PickUp,
                _ => PromptIconKind.Use
            },
            context.Verb switch
            {
                InteractionVerb.Sleep => "Sleep",
                InteractionVerb.SetHome => "Set Home",
                InteractionVerb.Light => "Light",
                InteractionVerb.Extinguish => "Extinguish",
                InteractionVerb.Open => "Open",
                InteractionVerb.Close => "Close",
                InteractionVerb.Gather => "Gather",
                InteractionVerb.Eat => "Eat",
                InteractionVerb.PickUp => "Pick Up",
                _ => "Use"
            },
            ResolveTargetName(runtime.Content, context.EntityKind, context.ContentId));
    }

    public StatusBannerViewData BuildStatusBanner(GameRuntime runtime)
    {
        if (runtime.WorldState.ActiveStatusEvent is null)
        {
            return new StatusBannerViewData(false, string.Empty);
        }

        var statusEvent = runtime.WorldState.ActiveStatusEvent;
        return runtime.WorldState.ActiveStatusEvent switch
        {
            { Kind: StatusEventKind.CraftedItem } => new StatusBannerViewData(true, $"Crafted {ResolveItemName(runtime.Content, statusEvent.PrimaryContentId!.Value)}"),
            { Kind: StatusEventKind.SmeltingStarted } => new StatusBannerViewData(true, $"Smelting {ResolveItemName(runtime.Content, statusEvent.PrimaryContentId!.Value)}"),
            { Kind: StatusEventKind.SmeltingCompleted } => new StatusBannerViewData(true, $"Smelted {ResolveItemName(runtime.Content, statusEvent.PrimaryContentId!.Value)}"),
            { Kind: StatusEventKind.MissingIngredient } => new StatusBannerViewData(true, $"Need {ResolveItemName(runtime.Content, statusEvent.PrimaryContentId!.Value)} x{statusEvent.Amount}"),
            { Kind: StatusEventKind.StationRequired } => new StatusBannerViewData(true, "Need a nearby station"),
            { Kind: StatusEventKind.InventoryFull } => new StatusBannerViewData(true, "Inventory full"),
            { Kind: StatusEventKind.EnteredPortal } => new StatusBannerViewData(true, "Entering Portal"),
            { Kind: StatusEventKind.ReturnedThroughPortal } => new StatusBannerViewData(true, "Returned to Overworld"),
            { Kind: StatusEventKind.Respawned } => new StatusBannerViewData(true, "Respawned at spawn point"),
            { Kind: StatusEventKind.HomeSet } => new StatusBannerViewData(true, "Home bed updated"),
            { Kind: StatusEventKind.SleptUntilMorning } => new StatusBannerViewData(true, "Slept until morning"),
            { Kind: StatusEventKind.LightLit } => new StatusBannerViewData(true, "Light source lit"),
            { Kind: StatusEventKind.LightExtinguished } => new StatusBannerViewData(true, "Light source extinguished"),
            { Kind: StatusEventKind.LightBurnedOut } => new StatusBannerViewData(true, "This torch has burned out"),
            { Kind: StatusEventKind.StationUpgraded } => new StatusBannerViewData(true, "Workbench upgraded"),
            _ => new StatusBannerViewData(true, "Craft failed")
        };
    }

    public DestroyProgressViewData BuildDestroyProgress(GameRuntime runtime)
    {
        return runtime.WorldState.ActiveDestroyProgress switch
        {
            null => new DestroyProgressViewData(false, string.Empty, 0f, default),
            var progress => new DestroyProgressViewData(
                true,
                progress.IsRaisedFeature
                    ? runtime.Content.RaisedFeatures.Get(progress.ContentId).DisplayName
                    : ResolveTargetName(runtime.Content, progress.EntityKind!.Value, progress.ContentId),
                progress.Progress01,
                progress.WorldPosition)
        };
    }

    private static string ResolveTargetName(ContentRegistrySet content, WorldEntityKind entityKind, ContentId contentId)
    {
        return entityKind switch
        {
            WorldEntityKind.ResourceNode => content.ResourceNodes.Get(contentId).DisplayName,
            WorldEntityKind.Placeable => content.Placeables.Get(contentId).DisplayName,
            WorldEntityKind.ItemDrop => content.Items.Get(contentId).DisplayName,
            WorldEntityKind.Creature => content.Creatures.Get(contentId).DisplayName,
            _ => contentId.Value
        };
    }

    private static string ResolveItemName(ContentRegistrySet content, ContentId contentId) => content.Items.Get(contentId).DisplayName;

    private static bool IsFurnaceRecipe(RecipeDef recipe) => recipe.RequiredStationKind == CraftingStationKind.Furnace;

    private static string ResolveActionLabel(RecipeDef recipe)
    {
        if (recipe.ExecutionKind == RecipeExecutionKind.UpgradeActiveStation)
        {
            return "Upgrade";
        }

        return IsFurnaceRecipe(recipe) ? "Smelt" : "Craft";
    }

    private static float ToPercent(int current, int max) => max <= 0 ? 0f : Math.Clamp((float)current / max, 0f, 1f);

    private static string ResolveStorageTitle(GameRuntime runtime)
    {
        if (runtime.WorldState.ActiveStorageEntityId is not { } storageId
            || !runtime.WorldState.GetActiveWorld().TryGetEntity(storageId, out var storageEntity))
        {
            return string.Empty;
        }

        return runtime.Content.Placeables.Get(storageEntity.DefinitionId).DisplayName;
    }

    private static IReadOnlyList<InventorySlotViewData> BuildStorageSlots(GameRuntime runtime)
    {
        if (runtime.WorldState.ActiveStorageEntityId is not { } storageId
            || !runtime.WorldState.GetActiveWorld().TryGetEntity(storageId, out var storageEntity)
            || storageEntity.StorageInventory is null)
        {
            return [];
        }

        return storageEntity.StorageInventory.Slots
            .Select(slot => new InventorySlotViewData(slot.ItemId, slot.Quantity))
            .ToArray();
    }

    private static string FormatTimeOfDayLabel(float timeProgress, float totalElapsedSeconds, float dayLengthSeconds)
    {
        var dayNumber = dayLengthSeconds <= 0f
            ? 1
            : (int)MathF.Floor(totalElapsedSeconds / dayLengthSeconds) + 1;
        var clockHours = TimeOfDayRules.ResolveClockHours(timeProgress);
        var hour = (int)MathF.Floor(clockHours);
        var minute = (int)MathF.Floor((clockHours - hour) * 60f);
        var phase = ResolveTimePhase(clockHours);
        return $"Day {dayNumber} {hour:00}:{minute:00} {phase}";
    }

    private static string ResolveTimePhase(float clockHours)
    {
        if (clockHours is >= 5f and < 7f)
        {
            return "Dawn";
        }

        if (clockHours is >= 7f and < 19f)
        {
            return "Day";
        }

        if (clockHours is >= 19f and < 21f)
        {
            return "Dusk";
        }

        return "Night";
    }

    private static float ResolveNightOverlayAlpha(float timeOfDaySeconds, float dayLengthSeconds)
    {
        var outdoorLight = LightingFieldSystem.ResolveOutdoorSkylightLevel(timeOfDaySeconds, dayLengthSeconds);
        var darkness = 1f - Math.Clamp(outdoorLight, 0f, 1f);
        return SmoothStep(darkness) * 0.24f;
    }

    private static float SmoothStep(float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        return clamped * clamped * (3f - (2f * clamped));
    }
}
