using System.Collections.Generic;
using Godot;

public partial class Squad : CharacterBody3D
{
	[Export] public int SquadSize = 4;
	[Export] public float MoveSpeed = 4f;
	[Export] public float TurnSpeed = 8f;
	[Export] public float FormationSpacing = 1.3f;
	[Export] public Color SquadColor = new Color(0.3f, 0.5f, 1f);
	[Export] public float StopDistance = 0.4f;
	[Export] public float DotSpacing = 0.8f;
	[Export] public Color PathColor = new Color(0.2f, 1f, 0.3f);
	[Export] public float ArrowRadius = 1.1f;
	[Export] public float ScatterRadius = 2.0f;        // how far soldiers spread from the squad center
	[Export] public float MinSoldierDistance = 1.2f;   // keeps soldiers from overlapping
	[Export] public float FollowRateMin = 2.5f;        // lower = lags further behind
	[Export] public float FollowRateMax = 5.0f;
	
	private const int MaxDots = 400;
	private const float ArrowGap = 1.6f;

	private NavigationAgent3D _agent;
	private MeshInstance3D _selectionRing;
	private readonly List<Node3D> _soldiers = new();
	private readonly List<Vector3> _slots = new();
	private bool _hasOrder;
	private bool _isSelected;
	private Node3D _pathVisual;
	private MultiMesh _dotsMultiMesh;
	private readonly MeshInstance3D[] _arrows = new MeshInstance3D[3];
	private readonly List<float> _followRates = new();

	public override void _Ready()
	{
		AddToGroup("squads");
		_agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		if (GetNode<CollisionShape3D>("CollisionShape3D").Shape is CylinderShape3D cyl)
			cyl.Radius = ScatterRadius + 0.5f;
		BuildFormationSlots();
		SpawnSoldiers();
		BuildSelectionRing();
		BuildPathVisual();
	}

	// ---- Public API used by the SelectionManager ----

	public void SetSelected(bool selected)
	{
		_isSelected = selected;
		_selectionRing.Visible = selected;
		if (!selected)
			_pathVisual.Visible = false;
	}

	public void MoveTo(Vector3 target)
	{
		_agent.TargetPosition = target;
		_hasOrder = true;
	}

	// ---- Movement ----

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		Vector3 toTarget = _agent.TargetPosition - GlobalPosition;
		toTarget.Y = 0;

		if (!_hasOrder || toTarget.Length() < StopDistance || _agent.IsNavigationFinished())
		{
			_hasOrder = false;
			Velocity = Vector3.Zero;
			return;
		}

		Vector3 next = _agent.GetNextPathPosition();
		Vector3 dir = next - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.0001f)
		{
			Velocity = Vector3.Zero;
			return;
		}

		dir = dir.Normalized();
		Velocity = dir * MoveSpeed;

		float targetYaw = Mathf.Atan2(-dir.X, -dir.Z);
		Vector3 rot = Rotation;
		rot.Y = Mathf.LerpAngle(rot.Y, targetYaw, 1f - Mathf.Exp(-TurnSpeed * dt));
		Rotation = rot;

	MoveAndSlide();
	}

	// ---- Soldiers (visual followers only) ----

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		for (int i = 0; i < _soldiers.Count; i++)
		{
			Node3D soldier = _soldiers[i];
			Vector3 target = GlobalTransform * _slots[i];
			target.Y = GlobalPosition.Y;

			Vector3 toTarget = target - soldier.GlobalPosition;
			soldier.GlobalPosition += toTarget * (1f - Mathf.Exp(-_followRates[i] * dt));

			// Face movement direction when walking, otherwise face squad forward
			Vector3 look = toTarget.LengthSquared() > 0.05f ? toTarget : -GlobalTransform.Basis.Z;
			look.Y = 0;
			if (look.LengthSquared() > 0.001f)
			{
				float yaw = Mathf.Atan2(-look.X, -look.Z);
				Vector3 r = soldier.Rotation;
				r.Y = Mathf.LerpAngle(r.Y, yaw, 1f - Mathf.Exp(-10f * dt));
				soldier.Rotation = r;
			}
		}
		UpdatePathVisual();
	}

	private void BuildFormationSlots()
	{
		_slots.Clear();
		_followRates.Clear();

	// Seeded per squad: each squad looks different, but stays the same during the game
		var rng = new RandomNumberGenerator { Seed = GetInstanceId() };

		for (int i = 0; i < SquadSize; i++)
		{
			Vector3 slot = Vector3.Zero;

		// Try random spots in a disc until one isn't crowding another soldier
			for (int attempt = 0; attempt < 40; attempt++)
			{
				float angle = rng.RandfRange(0f, Mathf.Tau);
				float dist = ScatterRadius * Mathf.Sqrt(rng.Randf());
				slot = new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);

				bool crowded = false;
				foreach (Vector3 other in _slots)
				{
					if (other.DistanceTo(slot) < MinSoldierDistance)
					{
						crowded = true;
						break;
					}
				}
				if (!crowded)
					break;
			}

			_slots.Add(slot);
			_followRates.Add(rng.RandfRange(FollowRateMin, FollowRateMax));
		}
	}

	private void SpawnSoldiers()
	{
		var material = new StandardMaterial3D { AlbedoColor = SquadColor };

		for (int i = 0; i < SquadSize; i++)
		{
			// Placeholder capsule. Swap for your real soldier scene later.
			var soldier = new MeshInstance3D
			{
				Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.7f },
				MaterialOverride = material,
				TopLevel = true // moves independently of the squad's transform
			};
			AddChild(soldier);
			soldier.GlobalPosition = GlobalTransform * _slots[i] + new Vector3(0, 0.85f, 0);
			_soldiers.Add(soldier);
		}
	}

	private void BuildSelectionRing()
	{
		_selectionRing = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = ScatterRadius + 0.4f, OuterRadius = ScatterRadius + 0.55f },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.2f, 1f, 0.3f),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			},
			Position = new Vector3(0, 0.05f, 0),
			Visible = false
		};
		AddChild(_selectionRing);
	}
	
	private void BuildPathVisual()
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = PathColor,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

	// TopLevel + identity transform means children use plain world coordinates
		_pathVisual = new Node3D { TopLevel = true, Visible = false };
		AddChild(_pathVisual);

	// Dots: one MultiMesh, allocated once, with only the needed number shown
		_dotsMultiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.02f },
			InstanceCount = MaxDots,
			VisibleInstanceCount = 0
		};
		_pathVisual.AddChild(new MultiMeshInstance3D
		{
			Multimesh = _dotsMultiMesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		});

	// Three arrows around the destination
		ArrayMesh arrowMesh = BuildArrowMesh();
		for (int i = 0; i < _arrows.Length; i++)
		{
			_arrows[i] = new MeshInstance3D
			{
				Mesh = arrowMesh,
				MaterialOverride = material,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			_pathVisual.AddChild(_arrows[i]);
		}
	}

	private static ArrayMesh BuildArrowMesh()
	{
	// Flat dart shape lying on the ground, tip pointing along -Z
		Vector3 tip = new Vector3(0, 0, -0.5f);
		Vector3 left = new Vector3(-0.35f, 0, 0.4f);
		Vector3 notch = new Vector3(0, 0, 0.15f);
		Vector3 right = new Vector3(0.35f, 0, 0.4f);

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		foreach (Vector3 v in new[] { tip, right, notch, tip, notch, left })
			st.AddVertex(v);
		return st.Commit();
	}

	private void UpdatePathVisual()
	{
		if (!_isSelected || !_hasOrder)
		{
			_pathVisual.Visible = false;
			return;
		}

	// Remaining route: the squad's position followed by the path points not yet reached
		var points = new List<Vector3> { GlobalPosition };
		Vector3[] path = _agent.GetCurrentNavigationPath();
		for (int i = Mathf.Max(_agent.GetCurrentNavigationPathIndex(), 0); i < path.Length; i++)
			points.Add(path[i]);

	// Path not computed yet on the first frame after an order: fall back to a straight line
		if (points.Count < 2)
			points.Add(_agent.TargetPosition);

	// The squad's origin is above the ground, so borrow the height of the next point
		points[0] = new Vector3(points[0].X, points[1].Y, points[0].Z);

		Vector3 end = points[points.Count - 1];
		Vector3 lift = new Vector3(0, 0.06f, 0);

	// Dotted line: walk along the path dropping a dot every DotSpacing
		int count = 0;
		float carry = 0f;
		for (int i = 0; i < points.Count - 1 && count < MaxDots; i++)
		{
			Vector3 a = points[i];
			Vector3 b = points[i + 1];
			float segLen = a.DistanceTo(b);
			if (segLen < 0.001f)
			continue;

			Vector3 dir = (b - a) / segLen;
			float d = carry;
			while (d < segLen && count < MaxDots)
			{
				Vector3 p = a + dir * d;
				if (p.DistanceTo(end) > ArrowGap)
					_dotsMultiMesh.SetInstanceTransform(count++, new Transform3D(Basis.Identity, p + lift));
				d += DotSpacing;
			}
			carry = d - segLen;
		}
		_dotsMultiMesh.VisibleInstanceCount = count;

	// Arrows: a triangle around the destination, each one pointing at it
		Vector3 approach = end - points[points.Count - 2];
		approach.Y = 0;
		if (approach.LengthSquared() < 0.01f)
			approach = -GlobalTransform.Basis.Z;
		approach = approach.Normalized();

		for (int i = 0; i < _arrows.Length; i++)
		{
		// One arrow directly behind the destination, the other two flanking it
			Vector3 offset = (-approach).Rotated(Vector3.Up, Mathf.DegToRad(120f * i)) * ArrowRadius;
			Vector3 toEnd = -offset;

			_arrows[i].Position = end + offset + lift * 1.2f;
			_arrows[i].Rotation = new Vector3(0, Mathf.Atan2(-toEnd.X, -toEnd.Z), 0);
		}

		_pathVisual.Visible = true;
	}
}
