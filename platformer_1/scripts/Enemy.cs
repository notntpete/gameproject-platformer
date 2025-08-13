using Godot;
using System.Collections.Generic;

public partial class Enemy : CharacterBody2D
{
	// Movement
	[Export] public float Speed = 120f;
	[Export] public float Gravity = 1200f;
	[Export] public float JumpVelocity = -350f;
	[Export] public float PathUpdateInterval = 0.3f;
	
	// Combat
	[Export] public int MaxHealth = 2;
	[Export] public int ContactDamage = 1;
	[Export] public float KnockbackForce = 200f;
	[Export] public float InvincibilityTime = 0.5f;
	
	// Navigation
	[Export] public GraphBuilder GraphBuilder;
	[Export] public float PlayerDetectionRange = 400f;
	[Export] public float WaypointDistance = 8f;
	
	// Nodes
	private Sprite2D _sprite;
	private RayCast2D _ledgeCheck;
	private Player _player;
	
	// State
	private List<GraphPoint> _currentPath = new();
	private int _pathIndex = 0;
	private float _pathUpdateTimer = 0f;
	private int _health;
	private bool _isInvincible;
	private Vector2 _knockback;
	private float _invincibilityTimer = 0f;

	public override void _Ready()
	{
		_health = MaxHealth;
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_ledgeCheck = GetNode<RayCast2D>("LedgeCheck");
		
		TryFindPlayer();
		
		if (_ledgeCheck == null)
			GD.PrintErr("Missing LedgeCheck RayCast2D node!");
		else
			UpdateLedgeCheckDirection();
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleInvincibility(delta);
		
		if (!ValidateDependencies())
		{
			ApplyGravity(delta);
			MoveAndSlide();
			return;
		}

		HandleKnockback(delta);
		
		if (!IsKnockbackActive())
		{
			HandlePathfinding(delta);
		}

		MoveAndSlide();
		HandleCollisions();
	}

	private void HandleInvincibility(double delta)
	{
		if (!_isInvincible) return;
		
		_invincibilityTimer -= (float)delta;
		if (_invincibilityTimer <= 0)
		{
			_isInvincible = false;
			_sprite.Modulate = Colors.White;
		}
		else
		{
			// Flash effect during invincibility
			float alpha = 0.5f + Mathf.Sin(_invincibilityTimer * 20f) * 0.5f;
			_sprite.Modulate = new Color(1, 1, 1, alpha);
		}
	}

	private void TryFindPlayer()
	{
		if (_player == null)
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
	}

	private bool ValidateDependencies()
	{
		if (GraphBuilder == null)
		{
			GraphBuilder = GetTree().GetFirstNodeInGroup("graph_builder") as GraphBuilder;
			if (GraphBuilder == null)
			{
				GD.PrintErr("GraphBuilder not found - enemy will not pathfind");
				return false;
			}
		}
		
		if (_player == null)
		{
			TryFindPlayer();
			if (_player == null)
			{
				GD.PrintErr("Player not found - enemy will not chase");
				return false;
			}
		}
		
		return true;
	}

	private void ApplyGravity(double delta)
	{
		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * (float)delta);
	}

	private void HandleKnockback(double delta)
	{
		_knockback = _knockback.Lerp(Vector2.Zero, 0.2f);
		Velocity = new Vector2(_knockback.X, Velocity.Y);
	}

	private void HandlePathfinding(double delta)
	{
		if (GlobalPosition.DistanceTo(_player.GlobalPosition) > PlayerDetectionRange)
		{
			Velocity = new Vector2(0, Velocity.Y);
			return;
		}

		_pathUpdateTimer -= (float)delta;
		if (_pathUpdateTimer <= 0 || _pathIndex >= _currentPath.Count)
		{
			UpdateNavigationPath();
			_pathUpdateTimer = PathUpdateInterval;
		}

		if (_currentPath.Count > 0 && _pathIndex < _currentPath.Count)
		{
			FollowPath();
		}
	}

	private void UpdateNavigationPath()
	{
		var start = GraphBuilder?.GetClosestPoint(GlobalPosition);
		var end = GraphBuilder?.GetClosestPoint(_player.GlobalPosition);
		
		if (start != null && end != null && start != end)
		{
			_currentPath = GraphBuilder.FindPath(start, end) ?? new List<GraphPoint>();
			_pathIndex = 0;
		}
		else
		{
			_currentPath.Clear();
		}
	}

	private void FollowPath()
	{
		Vector2 target = _currentPath[_pathIndex].GlobalPosition;
		Vector2 direction = (target - GlobalPosition).Normalized();
		
		Velocity = new Vector2(direction.X * Speed, Velocity.Y);
		
		if (ShouldJump(direction))
			Velocity = new Vector2(Velocity.X, JumpVelocity);
		
		UpdateMovementDirection(direction);
		
		if (GlobalPosition.DistanceTo(target) < WaypointDistance)
			_pathIndex++;
	}

	private void UpdateMovementDirection(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) > 0.1f)
		{
			_sprite.FlipH = direction.X < 0;
			UpdateLedgeCheckDirection();
		}
	}

	private bool ShouldJump(Vector2 direction)
	{
		return IsOnFloor() && 
			  (direction.Y < -0.3f || (_ledgeCheck != null && !_ledgeCheck.IsColliding()));
	}

	private void HandleCollisions()
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var collision = GetSlideCollision(i);
			if (collision.GetCollider() is Player player)
			{
				player.TakeDamage(ContactDamage);
				ApplyKnockback(collision.GetNormal());
			}
		}
	}

	/*public void TakeDamage(int damage, Vector2 sourcePosition)
	{
		if (_isInvincible) return;
		
		_health -= damage;
		GD.Print($"Enemy took {damage} damage! Health: {_health}/{MaxHealth}");
		
		if (_health <= 0)
		{
			Die();
			return;
		}

		// Start invincibility and knockback
		_isInvincible = true;
		_invincibilityTimer = InvincibilityTime;
		_knockback = (GlobalPosition - sourcePosition).Normalized() * KnockbackForce;
	}*/

	private void Die() => QueueFree();

	private void ApplyKnockback(Vector2 direction) => 
		_knockback = direction * KnockbackForce;

	private void UpdateLedgeCheckDirection()
	{
		if (_ledgeCheck != null)
			_ledgeCheck.TargetPosition = new Vector2(24 * (_sprite.FlipH ? -1 : 1), 0);
	}

	private bool IsKnockbackActive() => _knockback.Length() > 10f;
}
