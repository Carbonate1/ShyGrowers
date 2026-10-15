using Godot;
using System;
using System.Collections.Generic;

public partial class PlantSelectionMenu : CanvasLayer
{
	private const int LAYER = 10;
	private const int OFFSET_X = 180;
	private const int OFFSET_Y = 120;

	private PlanterBox pendingPlanterBox;
	private int pendingPlotIdx;
	private IReadOnlyDictionary<string, int> seedStock;
	private Func<string, bool> isPlantUnlocked;
	private Func<string, bool> tryConsumeSeed;
	private Control menuRoot;
	private PlantOverview plantOverview;

	public event Action<Plant> PlantPlanted;
	public bool IsOpen => menuRoot is not null;

	public override void _Ready()
	{
		Layer = LAYER;
		Hide();
	}

	public void Open(
		PlanterBox planterBox,
		int plotIdx,
		IReadOnlyDictionary<string, int> currentSeedStock,
		Func<string, bool> isUnlocked,
		Func<string, bool> tryConsume)
	{
		ArgumentNullException.ThrowIfNull(planterBox);
		ArgumentNullException.ThrowIfNull(currentSeedStock);
		ArgumentNullException.ThrowIfNull(isUnlocked);
		ArgumentNullException.ThrowIfNull(tryConsume);
		if (IsOpen)
		{
			return;
		}

		SetPendingSelection(
			planterBox, 
			plotIdx, 
			currentSeedStock, 
			isUnlocked, 
			tryConsume);
		PlantOverview overview = GetPlantOverview();
		Control backdrop = CreateBackdrop();
		PanelContainer menu = CreateMenuPanel();
		menu.AddChild(CreateOptions(overview));
		backdrop.AddChild(menu);
		ShowMenu(backdrop);
	}

	private void SetPendingSelection(
		PlanterBox planterBox,
		int plotIdx,
		IReadOnlyDictionary<string, int> currentSeedStock,
		Func<string, bool> isUnlocked,
		Func<string, bool> tryConsume)
	{
		pendingPlanterBox = planterBox;
		pendingPlotIdx = plotIdx;
		seedStock = currentSeedStock;
		isPlantUnlocked = isUnlocked;
		tryConsumeSeed = tryConsume;
	}

	private ColorRect CreateBackdrop()
	{
		ColorRect backdrop = new ColorRect
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
		PanelContainer menu = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -OFFSET_X,
			OffsetTop = -OFFSET_Y,
			OffsetRight = OFFSET_X,
			OffsetBottom = OFFSET_Y,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		return menu;
	}

	private VBoxContainer CreateOptions(PlantOverview overview)
	{
		VBoxContainer options = new VBoxContainer();
		options.AddChild(new Label
		{
			Text = "Choose a plant",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		foreach (SeedOffer offer in SeedCatalog.Offers)
		{
			if (isPlantUnlocked(offer.Name))
			{
				options.AddChild(CreatePlantButton(offer, overview));
			}
		}

		Button cancelButton = new Button { Text = "Cancel" };
		cancelButton.Pressed += Close;
		options.AddChild(cancelButton);
		return options;
	}

	private Button CreatePlantButton(SeedOffer offer, PlantOverview overview)
	{
		seedStock.TryGetValue(offer.Name, out int count);
		PlantData plantData = LoadPlantData(offer);
		Button button = new Button { Text = $"{plantData.DisplayName} (x{count})" };
		button.Pressed += () => SelectPlant(offer);
		Plant plant = LoadPlant(offer);
		button.Disabled = count <= 0 || !IsPlantableHere(plant, pendingPlanterBox, pendingPlotIdx);
		button.MouseEntered += () => overview.ShowOverview(plantData);
		button.MouseExited += overview.HideOverview;
		plant.Free();
		return button;
	}

	private PlantOverview GetPlantOverview()
	{
		if (plantOverview is null)
		{
			PackedScene overviewScene = GD.Load<PackedScene>("res://scenes/Canvases/plant_overview.tscn")
				?? throw new InvalidOperationException("Could not load the plant overview scene.");
			plantOverview = overviewScene.Instantiate<PlantOverview>();
			AddChild(plantOverview);
		}

		return plantOverview;
	}

	private void ShowMenu(Control backdrop)
	{
		menuRoot = backdrop;
		AddChild(menuRoot);
		Show();
	}

	public void Close()
	{
		plantOverview?.HideOverview();
		menuRoot?.QueueFree();
		menuRoot = null;
		pendingPlanterBox = null;
		seedStock = null;
		isPlantUnlocked = null;
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
		if (!isPlantUnlocked(offer.Name) ||
			!seedStock.TryGetValue(offer.Name, out int count) ||
			count <= 0)
		{
			return;
		}

		Plant plant = LoadPlant(offer);
		if (!pendingPlanterBox.TryPlant(plant, pendingPlotIdx))
		{
			plant.Free();
			GD.PushWarning($"{plant.Data.DisplayName} cannot be planted in this location.");
			return;
		}

		if (!tryConsumeSeed(offer.Name))
		{
			pendingPlanterBox.RemovePlant(plant);
			plant.QueueFree();
			throw new InvalidOperationException($"Potential race condition: Seed stock for '{offer.Name}' changed while planting.");
		}

		PlantPlanted?.Invoke(plant);
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

	private static PlantData LoadPlantData(SeedOffer offer)
	{
		return GD.Load<PlantData>(offer.PlantDataPath)
			?? throw new InvalidOperationException($"Could not load plant data at '{offer.PlantDataPath}'.");
	}
}
