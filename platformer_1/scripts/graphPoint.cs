using Godot;
using System.Collections.Generic;

public partial class GraphPoint : Node2D
{
	public List<GraphPoint> Neighbors { get; } = new();
	
	public override void _Draw()
	{
		DrawCircle(Vector2.Zero, 4, Colors.Red);
		foreach (var neighbor in Neighbors)
		{
			if (IsInstanceValid(neighbor))
				DrawLine(Vector2.Zero, ToLocal(neighbor.GlobalPosition), Colors.Green, 1);
		}
	}

	public void ConnectTo(GraphPoint other)
	{
		if (other != null && other != this && !Neighbors.Contains(other))
		{
			Neighbors.Add(other);
			if (!other.Neighbors.Contains(this))
				other.Neighbors.Add(this);
			QueueRedraw();
		}
	}
}
