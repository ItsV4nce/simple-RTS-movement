using System.Collections.Generic;
using Godot;
using System.Linq;

public partial class SelectionManager : Node3D
{
	[Export] public float LineSpacing = 4f;
	
	private const uint GroundMask = 1;
	private const uint UnitMask = 2;
	private const float DragThreshold = 6f; // pixels before a click becomes a drag

	private readonly List<Squad> _selected = new();

	private bool _mouseDown;
	private bool _dragging;
	private Vector2 _dragStart;
	private Vector2 _dragEnd;
	private Control _overlay;

	public override void _Ready()
	{
		// Overlay for drawing the selection rectangle (built in code, no scene setup needed)
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);

		_overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_overlay.Draw += DrawSelectionBox;
		layer.AddChild(_overlay);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_mouseDown = true;
					_dragging = false;
					_dragStart = _dragEnd = mb.Position;
				}
				else if (_mouseDown)
				{
					_dragEnd = mb.Position;
					FinishLeftRelease(mb.Position, mb.ShiftPressed);
				}
			}
			else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
			{
				HandleOrder(mb.Position);
			}
		}
		else if (@event is InputEventMouseMotion mm && _mouseDown)
		{
			_dragEnd = mm.Position;

			if (!_dragging && _dragStart.DistanceTo(_dragEnd) > DragThreshold)
				_dragging = true;

			if (_dragging)
				_overlay.QueueRedraw();
		}
	}

	private void FinishLeftRelease(Vector2 pos, bool additive)
	{
		_mouseDown = false;

		if (_dragging)
		{
			_dragging = false;
			_overlay.QueueRedraw();
			BoxSelect(GetDragRect(), additive);
		}
		else
		{
			ClickSelect(pos, additive);
		}
	}

	// ---- Selection ----

	private void ClickSelect(Vector2 screenPos, bool additive)
	{
		var hit = Raycast(screenPos, UnitMask);

		if (!additive)
			ClearSelection();

		if (hit.Count > 0 && hit["collider"].As<GodotObject>() is Squad squad)
			AddToSelection(squad);
	}

	private void BoxSelect(Rect2 rect, bool additive)
	{
		if (!additive)
			ClearSelection();

		Camera3D cam = GetViewport().GetCamera3D();

		foreach (Node node in GetTree().GetNodesInGroup("squads"))
		{
			if (node is not Squad squad)
				continue;

			Vector3 worldPos = squad.GlobalPosition;
			if (cam.IsPositionBehind(worldPos))
				continue;

			if (rect.HasPoint(cam.UnprojectPosition(worldPos)))
				AddToSelection(squad);
		}
	}

	private void AddToSelection(Squad squad)
	{
		if (_selected.Contains(squad))
			return;

		_selected.Add(squad);
		squad.SetSelected(true);
	}

	private void ClearSelection()
	{
		foreach (Squad s in _selected)
			if (IsInstanceValid(s))
				s.SetSelected(false);
		_selected.Clear();
	}

	// ---- Orders ----

	private void HandleOrder(Vector2 screenPos)
	{
		_selected.RemoveAll(s => !IsInstanceValid(s));
		if (_selected.Count == 0)
			return;

		var hit = Raycast(screenPos, GroundMask);
		if (hit.Count == 0)
			return;

		Vector3 target = hit["position"].AsVector3();

	// A single squad just goes to the clicked point
		if (_selected.Count == 1)
		{
			_selected[0].MoveTo(target);
			return;
		}

	// Direction the group is travelling: from the group's center toward the click
		Vector3 centroid = Vector3.Zero;
		foreach (Squad s in _selected)
			centroid += s.GlobalPosition;
			centroid /= _selected.Count;

		Vector3 forward = target - centroid;
		forward.Y = 0;

	// If the click is right on top of the group, fall back to the camera's facing
		if (forward.LengthSquared() < 1f)
		{
			forward = -GetViewport().GetCamera3D().GlobalTransform.Basis.Z;
			forward.Y = 0;
		}
		forward = forward.Normalized();

	// The line runs perpendicular to the travel direction
		Vector3 right = forward.Cross(Vector3.Up);

	// Sort squads left to right so their paths don't cross each other
		var ordered = _selected.OrderBy(s => s.GlobalPosition.Dot(right)).ToList();

		Rid navMap = GetWorld3D().NavigationMap;
		int count = ordered.Count;

		for (int i = 0; i < count; i++)
		{
			float offset = (i - (count - 1) / 2f) * LineSpacing;
			Vector3 slot = target + right * offset;

		// Snap to the navmesh so slots near map edges or obstacles stay reachable
			slot = NavigationServer3D.MapGetClosestPoint(navMap, slot);

			ordered[i].MoveTo(slot);
		}
	}

	// ---- Helpers ----

	private Rect2 GetDragRect() => new Rect2(_dragStart, Vector2.Zero).Expand(_dragEnd);

	private void DrawSelectionBox()
	{
		if (!_dragging)
			return;

		Rect2 rect = GetDragRect();
		_overlay.DrawRect(rect, new Color(0.2f, 1f, 0.3f, 0.15f), true);
		_overlay.DrawRect(rect, new Color(0.2f, 1f, 0.3f, 0.9f), false, 1.5f);
	}

	private Godot.Collections.Dictionary Raycast(Vector2 screenPos, uint mask)
	{
		Camera3D cam = GetViewport().GetCamera3D();
		Vector3 from = cam.ProjectRayOrigin(screenPos);
		Vector3 to = from + cam.ProjectRayNormal(screenPos) * 1000f;

		var query = PhysicsRayQueryParameters3D.Create(from, to, mask);
		return GetWorld3D().DirectSpaceState.IntersectRay(query);
	}
}
