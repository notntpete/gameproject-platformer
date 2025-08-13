using Godot;

public partial class Player : CharacterBody2D
{
	// Movement
	[Export] public float Speed = 200f;
	[Export] public float SprintSpeed = 300f;
	[Export] public float Acceleration = 15f;
	[Export] public float AirControl = 8f;
	
	// Jumping
	[Export] public float JumpVelocity = -400f;
	[Export] public float Gravity = 1200f;
	[Export] public float CoyoteTime = 0.1f;
	[Export] public float JumpBufferTime = 0.1f;
	
	// Combat
	[Export] public float AttackCooldown = 0.5f;
	[Export] public int AttackDamage = 1;
	[Export] public int MaxHealth = 3;
	
	private Sprite2D _sprite;
	private Timer _coyoteTimer;
	private Timer _jumpBufferTimer;
	private Timer _attackCooldownTimer;
	private Area2D _attackArea;
	private Vector2 _velocity;
	private bool _wasOnFloor;
	private float _currentSpeed;
	private int _health;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_attackArea = GetNode<Area2D>("AttackArea");
		_health = MaxHealth;
		
		_coyoteTimer = new Timer { WaitTime = CoyoteTime, OneShot = true };
		_jumpBufferTimer = new Timer { WaitTime = JumpBufferTime, OneShot = true };
		_attackCooldownTimer = new Timer { WaitTime = AttackCooldown, OneShot = true };
		
		AddChild(_coyoteTimer);
		AddChild(_jumpBufferTimer);
		AddChild(_attackCooldownTimer);
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovement(delta);
		HandleJump();
		HandleAttack();
		ApplyGravity(delta);
		
		_wasOnFloor = IsOnFloor();
		Velocity = _velocity;
		MoveAndSlide();
		
		UpdateSprite();
	}

	private void HandleMovement(double delta)
	{
		float direction = Input.GetAxis("move_left", "move_right");
		float accel = IsOnFloor() ? Acceleration : AirControl;
		
		_currentSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : Speed;
		_velocity.X = Mathf.Lerp(_velocity.X, direction * _currentSpeed, accel * (float)delta);
	}

	private void HandleJump()
	{
		if (Input.IsActionJustPressed("jump"))
			_jumpBufferTimer.Start();
		
		if (!_wasOnFloor && IsOnFloor())
			_coyoteTimer.Start();
		
		if (CanJump())
		{
			_velocity.Y = JumpVelocity;
			_jumpBufferTimer.Stop();
			_coyoteTimer.Stop();
		}
		
		if (Input.IsActionJustReleased("jump") && _velocity.Y < 0)
			_velocity.Y *= 0.5f;
	}

	private bool CanJump() => 
		_jumpBufferTimer.TimeLeft > 0 && (IsOnFloor() || _coyoteTimer.TimeLeft > 0);

	private void HandleAttack()
	{
		if (_attackCooldownTimer.TimeLeft > 0 || !Input.IsActionJustPressed("attack")) return;
		
		PerformAttack();
		_attackCooldownTimer.Start();
	}

	private void PerformAttack()
	{
		foreach (var body in _attackArea.GetOverlappingBodies())
		{
			if (body is Enemy enemy && IsInstanceValid(enemy))
				enemy.TakeDamage(AttackDamage);
		}
	}

	private void ApplyGravity(double delta)
	{
		if (!IsOnFloor())
			_velocity.Y += Gravity * (float)delta;
	}

	private void UpdateSprite()
	{
		if (Mathf.Abs(_velocity.X) > 1f)
			_sprite.FlipH = _velocity.X < 0;
		
		_sprite.Modulate = IsOnFloor() ? Colors.White : new Color(1, 0.9f, 0.9f);
	}

	public void TakeDamage(int damage)
	{
		_health -= damage;
		GD.Print($"Player took {damage} damage! Health: {_health}/{MaxHealth}");
		if (_health <= 0) Die();
	}

	private void Die() => QueueFree();
}
