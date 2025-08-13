using Godot;
using System;

public partial class Follower : CharacterBody2D
{
	[Export] public float Speed = 180f;
	[Export] public float Gravity = 1200f;
	[Export] public float JumpVelocity = -500f;
	[Export] public float JumpCheckDistance = 24f;
	[Export] public float MaxJumpHeight = 80f;

	private NavigationAgent2D agent;
	private Node2D target;

	public override void _Ready()
	{
		agent = GetNode<NavigationAgent2D>("NavigationAgent2D");

		var found = GetTree().GetFirstNodeInGroup("player");
		if (found is Node2D node)
		{
			target = node;
			agent.TargetPosition = target.GlobalPosition;
		}

		agent.PathDesiredDistance = 4f;
		agent.TargetDesiredDistance = 4f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (target == null || agent == null)
			return;

		agent.TargetPosition = target.GlobalPosition;
		if (agent.IsNavigationFinished())
			return;

		Vector2 velocity = Velocity;

		// Gravity
		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		Vector2 nextPos = agent.GetNextPathPosition();
		Vector2 direction = (nextPos - GlobalPosition).Normalized();
		float verticalGap = nextPos.Y - GlobalPosition.Y;

		velocity.X = direction.X * Speed;

		// JUMP LOGIC
		if (IsOnFloor())
		{
			if (verticalGap < -10f && Mathf.Abs(verticalGap) <= MaxJumpHeight)
			{
				// Jump to a higher platform
				velocity.Y = JumpVelocity;
			}
			else if (!HasFloorAhead(direction.X))
			{
				// Jump over a gap
				velocity.Y = JumpVelocity;
			}
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private bool HasFloorAhead(float dirX)
	{
		var space = GetWorld2D().DirectSpaceState;

		Vector2 start = GlobalPosition + new Vector2(dirX * JumpCheckDistance, 0);
		Vector2 end = start + Vector2.Down * 24f;

		var query = new PhysicsRayQueryParameters2D
		{
			From = start,
			To = end,
			CollisionMask = 1,
			Exclude = new Godot.Collections.Array<Rid> { GetRid() }
		};

		var result = space.IntersectRay(query);
		return result.Count > 0;
	}
}
