using Godot;
using System;
using System.Collections.Generic;

public partial class PlantSelectionMenu : CanvasLayer
{
	private PlanterBox pendingPlanterBox;
	private int pendingPlotIdx;
	private IReadOnlyDictionary<string, int> seedStock;
	private Func<string, bool> tryConsumeSeed;
	private Control menuRoot;

	public bool IsOpen => menuRoot is not null;

	public override void _Ready()
	{
		Layer = 10;
		Hide();
	}

	public void Open(
		PlanterBox planterBox,
		int plotIdx,
		IReadOnlyDictionary<string, int> currentSeedStock,
		Func<string, bool> tryConsume)
	{
		ArgumentNullException.ThrowIfNull(planterBox);
		ArgumentNullException.ThrowIfNull(currentSeedStock);
		ArgumentNullException.ThrowIfNull(tryConsume);
		if (IsOpen)
		{
			return;
		}

		SetPendingSelection(planterBox, plotIdx, currentSeedStock, tryConsume);
		Control backdrop = CreateBackdrop();
		PanelContainer menu = CreateMenuPanel();
		menu.AddChild(CreateOptions());
		backdrop.AddChild(menu);
		ShowMenu(backdrop);
	}

	private void SetPendingSelection(
		PlanterBox planterBox,
		int plotIdx,
		IReadOnlyDictionary<string, int> currentSeedStock,
		Func<string, bool> tryConsume)
	{
		pendingPlanterBox = planterBox;
		pendingPlotIdx = plotIdx;
		seedStock = currentSeedStock;
		tryConsumeSeed = tryConsume;
	}

	private ColorRect CreateBackdrop()
	{
		var backdrop = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.6f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorRight = 1,
			AnchorBottom = 1
		};
		backdrop.GuiInput += OnBackdropInput;
		return backdrop;
	}

	private static PanelContainer CreateMenuPanel()
	{
		var menu = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -180,
			OffsetTop = -120,
			OffsetRight = 180,
			OffsetBottom = 120,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		return menu;
	}

	private VBoxContainer CreateOptions()
	{
		var options = new VBoxContainer();
		options.AddChild(new Label
		{
			Text = "Choose a plant",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			options.AddChild(CreatePlantButton(offer));
		}

		var cancelButton = new Button { Text = "Cancel" };
		cancelButton.Pressed += Close;
		options.AddChild(cancelButton);
		return options;
	}

	private Button CreatePlantButton(SeedOffer offer)
	{
		seedStock.TryGetValue(offer.Id, out int count);
		var button = new Button { Text = $"{offer.DisplayName} (x{count})" };
		button.Pressed += () => SelectPlant(offer);
		Plant plant = LoadPlant(offer);
		button.Disabled = count <= 0 || !IsPlantableHere(plant, pendingPlanterBox, pendingPlotIdx);
		plant.Free();
		return button;
	}

	private void ShowMenu(Control backdrop)
	{
		menuRoot = backdrop;
		AddChild(menuRoot);
		Show();
	}

	public void Close()
	{
		menuRoot?.QueueFree();
		menuRoot = null;
		pendingPlanterBox = null;
		seedStock = null;
		tryConsumeSeed = null;
		Hide();
	}

	private static bool IsPlantableHere(Plant option, PlanterBox planterBox, int plotIdx)
	{
		return planterBox.CanPlant(option, plotIdx);
	}

	private void OnBackdropInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true })
		{
			GetViewport().SetInputAsHandled();
		}
	}

	private void SelectPlant(SeedOffer offer)
	{
		if (!seedStock.TryGetValue(offer.Id, out int count) || count <= 0)
		{
			return;
		}

		Plant plant = LoadPlant(offer);
		if (!pendingPlanterBox.TryPlant(plant, pendingPlotIdx))
		{
			plant.Free();
			GD.PushWarning($"{offer.DisplayName} cannot be planted in this location.");
			return;
		}

		if (!tryConsumeSeed(offer.Id))
		{
			pendingPlanterBox.RemovePlant(plant);
			plant.QueueFree();
			throw new InvalidOperationException($"Potential race condition: Seed stock for '{offer.Id}' changed while planting.");
		}

		Close();
	}

	private static Plant LoadPlant(SeedOffer offer)
	{
		PackedScene plantScene = GD.Load<PackedScene>(offer.ScenePath);
		if (plantScene is null)
		{
			throw new InvalidOperationException($"Could not load plant scene at '{offer.ScenePath}'.");
		}

		return plantScene.Instantiate<Plant>();
	}
}
