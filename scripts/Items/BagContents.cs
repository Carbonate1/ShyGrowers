using Godot;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class BagContents : Node3D
{
	[Export] public PackedScene Flower { get; set; }
	[Export] public Node3D Shown { get; set; }
	[Export] public Node3D Bodies { get; set; }
	[Export] public Area3D Gravity { get; set; }
	[Export] public Marker3D Opening { get; set; }
	[Export] public float Rim { get; set; } = 0.24f;
	[Export] public float Spread { get; set; } = 0.015f;
	[Export] public float DropSpeed { get; set; } = 0.6f;
	[Export] public int Capacity { get; set; } = 12;
	[Export(PropertyHint.Layers3DPhysics)] public uint LidLayer { get; set; } = 2;

	private readonly Dictionary<InventoryItem, BaggedFlower> flowers = new();
	private Inventory inventory;

	public override void _Ready()
	{
		inventory = GetTree().GetFirstNodeInGroup("inventory") as Inventory;
		if (inventory is null)
		{
			return;
		}

		inventory.ItemsChanged += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (inventory is not null)
		{
			inventory.ItemsChanged -= Refresh;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Gravity.GravityDirection = GlobalBasis.Orthonormalized().Inverse() * Vector3.Down;

		foreach ((InventoryItem item, BaggedFlower flower) in flowers)
		{
			if (flower.Position.LengthSquared() > 1f)
			{
				Drop(flower);
			}
			else if (flower.Position.Y < Rim)
			{
				flower.CollisionMask |= LidLayer;
			}

			item.bagPose = flower.Transform;
		}
	}

	private void Refresh()
	{
		HashSet<InventoryItem> held = inventory.Items.Take(Capacity).ToHashSet();

		foreach (InventoryItem item in flowers.Keys.Where(item => !held.Contains(item)).ToList())
		{
			flowers[item].Pop(Opening.Position);
			flowers.Remove(item);
		}

		foreach (InventoryItem item in held.Where(item => !flowers.ContainsKey(item)))
		{
			BaggedFlower flower = Flower.Instantiate<BaggedFlower>();
			Bodies.AddChild(flower);

			if (item.bagPose is Transform3D pose)
			{
				flower.Transform = pose;
			}
			else
			{
				Drop(flower);
			}

			flower.ShowIn(Shown, item.texture);
			flowers[item] = flower;
		}
	}

	private void Drop(BaggedFlower flower)
	{
		Vector3 offset = new((float)GD.RandRange(-Spread, Spread), 0f, (float)GD.RandRange(-Spread, Spread));
		Vector3 tumble = new((float)GD.RandRange(-0.4, 0.4), (float)GD.RandRange(-Mathf.Pi, Mathf.Pi), (float)GD.RandRange(-0.4, 0.4));

		flower.Transform = new Transform3D(Basis.FromEuler(tumble), Opening.Position + offset);
		flower.CollisionMask &= ~LidLayer;
		flower.LinearVelocity = Vector3.Down * DropSpeed;
		flower.AngularVelocity = tumble;
	}
}
