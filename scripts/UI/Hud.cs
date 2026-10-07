using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Hud : CanvasLayer
{
	[Export] public Hands Hands { get; set; }
	[Export] public Drawer Drawer { get; set; }
	[Export] public RoomCamera Camera { get; set; }
	[Export] public Gardener Gardener { get; set; }
	[Export] public SeedStock Seeds { get; set; }

	[ExportGroup("Widgets")]
	[Export] public ItemSlot LeftHand { get; set; }
	[Export] public ItemSlot RightHand { get; set; }
	[Export] public Label ViewName { get; set; }
	[Export] public Control DrawerPanel { get; set; }
	[Export] public Container DrawerSlots { get; set; }
	[Export] public Label Harvested { get; set; }
	[Export] public Container SeedCounters { get; set; }
	[Export] public Control EndScreen { get; set; }
	[Export] public Label EndScore { get; set; }

	private List<ItemSlot> drawerSlots;

	public override void _Ready()
	{
		drawerSlots = DrawerSlots.GetChildren().OfType<ItemSlot>().ToList();
		foreach (ItemSlot slot in drawerSlots)
		{
			slot.Clicked += OnDrawerSlotClicked;
		}

		Hands.Changed += ShowHands;
		Drawer.Opened += DrawerPanel.Show;
		Drawer.Closed += DrawerPanel.Hide;
		Drawer.ContentsChanged += ShowDrawer;
		Camera.ViewChanged += OnViewChanged;
		Seeds.Changed += ShowSeeds;
		Gardener.PlantHarvested += ShowHarvested;
		Gardener.GardenEmpty += OnGardenEmpty;

		DrawerPanel.Hide();
		EndScreen.Hide();
		ShowHands();
		ShowDrawer();
		ShowSeeds();
		ShowHarvested(0);
		ViewName.Text = Camera.CurrentView.Name;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && Drawer.IsOpen)
		{
			Drawer.Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnViewChanged(Node3D view)
	{
		ViewName.Text = view.Name;
		Drawer.Close();
	}

	private void OnDrawerSlotClicked(ItemSlot slot, MouseButton button)
	{
		int index = drawerSlots.IndexOf(slot);
		if (index >= Drawer.Items.Count)
		{
			return;
		}

		Drawer.ClickSlot(index, button);
	}

	private void ShowHands()
	{
		LeftHand.SetItem(Hands.Left);
		RightHand.SetItem(Hands.Right);
	}

	private void ShowDrawer()
	{
		for (int i = 0; i < drawerSlots.Count; i++)
		{
			drawerSlots[i].Visible = i < Drawer.Items.Count;
			drawerSlots[i].SetItem(i < Drawer.Items.Count ? Drawer.Items[i] : null);
		}
	}

	private void ShowSeeds()
	{
		foreach (SeedCounter counter in SeedCounters.GetChildren().OfType<SeedCounter>())
		{
			counter.ShowCount(Seeds.Count(counter.Seeds));
		}
	}

	private void ShowHarvested(int total)
	{
		Harvested.Text = $"flowers grown: {total}";
	}

	private void OnGardenEmpty(int total)
	{
		Drawer.Close();
		EndScore.Text = total == 1 ? "you grew 1 flower" : $"you grew {total} flowers";
		EndScreen.Show();
	}
}
