using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class GraphBuilder : Node2D
{
	[Export] public PackedScene GraphPointScene;
	[Export] public TileMapLayer GroundLayer;
	[Export] public Vector2I TileSize = new(16, 16);
	[Export] public bool DebugDraw = true;
	
	private Node2D _pointsContainer;
	private List<GraphPoint> _points = new();
	private Dictionary<Vector2I, GraphPoint> _positionMap = new();

	public override void _Ready()
	{
		InitializePointsContainer();
		
		if (!ValidateSetup())
		{
			GD.PrintErr("GraphBuilder initialization failed - check exports!");
			SetProcess(false);
			return;
		}

		GenerateNavigationGraph();
		AddToGroup("graph_builder");
	}

	private void InitializePointsContainer()
	{
		_pointsContainer = GetNodeOrNull<Node2D>("PointsContainer") ?? new Node2D();
		_pointsContainer.Name = "PointsContainer";
		AddChild(_pointsContainer);
	}

	private bool ValidateSetup()
	{
		bool isValid = true;
		
		if (GraphPointScene == null)
		{
			GD.PrintErr("Missing GraphPointScene reference!");
			isValid = false;
		}
		
		if (GroundLayer == null)
		{
			GD.PrintErr("Missing GroundLayer reference!");
			isValid = false;
		}
		else if (GroundLayer.GetUsedCells().Count() == 0)
		{
			GD.PrintErr("GroundLayer has no tiles!");
			isValid = false;
		}
		
		return isValid;
	}

	private void GenerateNavigationGraph()
	{
		ClearExistingPoints();
		
		foreach (Vector2I coords in GroundLayer.GetUsedCells())
		{
			if (!IsValidTile(coords)) continue;
			
			CreateNavigationPoint(coords);
		}
		
		if (_points.Count == 0)
		{
			GD.PrintErr("No valid navigation points generated!");
			return;
		}

		ConnectAllPoints();
		GD.Print($"Generated {_points.Count} navigation points");
		QueueRedraw();
	}

	private bool IsValidTile(Vector2I coords)
	{
		// Check tile exists and has valid data
		if (GroundLayer.GetCellSourceId(coords) == -1) return false;
		
		var tileData = GroundLayer.GetCellTileData(coords);
		if (tileData == null) return false;
		
		// Optional: Add any additional tile validity checks here
		return true;
	}

	private void CreateNavigationPoint(Vector2I coords)
	{
		var point = GraphPointScene.Instantiate<GraphPoint>();
		point.Position = GroundLayer.MapToLocal(coords) + (TileSize / 2);
		point.Name = $"NavPoint_{coords.X}_{coords.Y}";
		_pointsContainer.AddChild(point);
		_points.Add(point);
		_positionMap[coords] = point;
	}

	private void ConnectAllPoints()
	{
		var directions = new Vector2I[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };
		
		foreach (var point in _points.ToList()) // Safe iteration
		{
			if (!IsInstanceValid(point)) continue;
			
			var coords = GroundLayer.LocalToMap(point.Position);
			foreach (var dir in directions)
			{
				if (_positionMap.TryGetValue(coords + dir, out var neighbor) && IsInstanceValid(neighbor))
				{
					point.ConnectTo(neighbor);
				}
			}
		}
	}

	private void ClearExistingPoints()
	{
		foreach (Node child in _pointsContainer.GetChildren())
			child.QueueFree();
		
		_points.Clear();
		_positionMap.Clear();
	}

	public GraphPoint GetClosestPoint(Vector2 position)
	{
		if (_points.Count == 0)
		{
			GD.PrintErr("No navigation points available!");
			return null;
		}

		return _points
			.Where(IsInstanceValid)
			.OrderBy(p => position.DistanceTo(p.GlobalPosition))
			.FirstOrDefault();
	}

	public List<GraphPoint> FindPath(GraphPoint start, GraphPoint end)
	{
		// Validate inputs
		if (start == null || end == null)
		{
			GD.PrintErr("Pathfinding called with null start/end points!");
			return new List<GraphPoint>();
		}
		
		if (start == end)
			return new List<GraphPoint> { start };

		var frontier = new PriorityQueue<GraphPoint, float>();
		var cameFrom = new Dictionary<GraphPoint, GraphPoint>();
		var costSoFar = new Dictionary<GraphPoint, float>();
		
		frontier.Enqueue(start, 0);
		cameFrom[start] = null;
		costSoFar[start] = 0;

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();
			
			if (current == end)
				return ReconstructPath(cameFrom, end);
			
			foreach (var neighbor in current.Neighbors.Where(IsInstanceValid))
			{
				float newCost = costSoFar[current] + current.Position.DistanceTo(neighbor.Position);
				if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
				{
					costSoFar[neighbor] = newCost;
					float priority = newCost + Heuristic(neighbor, end);
					frontier.Enqueue(neighbor, priority);
					cameFrom[neighbor] = current;
				}
			}
		}
		
		GD.Print("No valid path found!");
		return new List<GraphPoint>();
	}

	private List<GraphPoint> ReconstructPath(Dictionary<GraphPoint, GraphPoint> cameFrom, GraphPoint end)
	{
		var path = new List<GraphPoint>();
		var current = end;
		
		while (current != null && cameFrom.ContainsKey(current))
		{
			path.Insert(0, current);
			current = cameFrom[current];
		}
		
		return path;
	}

	private float Heuristic(GraphPoint a, GraphPoint b) => a.Position.DistanceTo(b.Position);

	public override void _Draw()
	{
		if (!DebugDraw) return;
		
		foreach (var point in _points.Where(IsInstanceValid))
		{
			DrawCircle(point.Position - GlobalPosition, 3, new Color(0, 1, 0, 0.5f));
			foreach (var neighbor in point.Neighbors.Where(IsInstanceValid))
			{
				DrawLine(point.Position - GlobalPosition, 
						neighbor.Position - GlobalPosition, 
						new Color(0, 0.8f, 0, 0.3f), 
						1.5f);
			}
		}
	}
}
