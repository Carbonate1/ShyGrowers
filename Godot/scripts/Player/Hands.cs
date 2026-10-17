using Godot;
using System;

[GlobalClass]
public partial class Hands : Node
{
	[Signal] public delegate void ChangedEventHandler();

	[Export] public Hand LeftHand { get; set; }
	[Export] public Hand RightHand { get; set; }

	public override void _Ready()
	{
		ArgumentNullException.ThrowIfNull(LeftHand);
		ArgumentNullException.ThrowIfNull(RightHand);
		if (LeftHand == RightHand)
		{
			throw new InvalidOperationException("Hands must reference different Hand nodes.");
		}

		LeftHand.ItemChanged += OnHandItemChanged;
		RightHand.ItemChanged += OnHandItemChanged;
	}

	public override void _ExitTree()
	{
		if (LeftHand is not null)
		{
			LeftHand.ItemChanged -= OnHandItemChanged;
		}

		if (RightHand is not null)
		{
			RightHand.ItemChanged -= OnHandItemChanged;
		}
	}

	public Hand GetHand(MouseButton button)
	{
		if (button == MouseButton.Left)
		{
			return LeftHand;
		}
		else
		{
			return RightHand;
		}
	}

	public Hand OtherHand(Hand hand)
	{
		return hand == LeftHand ? RightHand : LeftHand;
	}

	private void OnHandItemChanged()
	{
		EmitSignal(SignalName.Changed);
	}
}
