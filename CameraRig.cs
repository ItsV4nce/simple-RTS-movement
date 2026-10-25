using Godot;

public partial class CameraRig : Node3D
{
	[Export] public float MoveSpeed = 20f;
	[Export] public float FastMultiplier = 2f;
	[Export] public float RotateSpeed = 2f;
	[Export] public float ZoomSpeed = 2f;
	[Export] public float MinZoom = 5f;
	[Export] public float MaxZoom = 30f;
	[Export] public float Smoothing = 10f;
	[Export] public float PitchDegrees = 55f;

	[Export] public bool EdgeScrolling = true;
	[Export] public int EdgeMargin = 12;

	[Export] public Vector2 MapMin = new Vector2(-100, -100);
	[Export] public Vector2 MapMax = new Vector2(100, 100);

	private Camera3D _camera;
	private Vector3 _targetPosition;
	private float _targetYaw;
	private float _targetZoom;
	private Vector3 _zoomDir;
	private float _zoom;

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");

		float pitch = Mathf.DegToRad(PitchDegrees);
		_zoomDir = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch));
		_camera.RotationDegrees = new Vector3(-PitchDegrees, 0, 0);

		_targetZoom = Mathf.Clamp(15f, MinZoom, MaxZoom);
		_zoom = _targetZoom;
		_camera.Position = _zoomDir * _zoom;

		_targetPosition = GlobalPosition;
		_targetYaw = Rotation.Y;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				_targetZoom -= ZoomSpeed;
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				_targetZoom += ZoomSpeed;

			_targetZoom = Mathf.Clamp(_targetZoom, MinZoom, MaxZoom);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// Keyboard movement
		Vector2 input = Input.GetVector("camera_left", "camera_right", "camera_forward", "camera_back");

		// Edge scrolling
		if (EdgeScrolling && input == Vector2.Zero)
			input = GetEdgeScrollInput();

		// Move relative to camera yaw so "forward" always means "away from the screen"
		Vector3 direction = new Vector3(input.X, 0, input.Y).Rotated(Vector3.Up, _targetYaw);

		float speed = MoveSpeed * (Input.IsKeyPressed(Key.Shift) ? FastMultiplier : 1f);
		// Scale with zoom so it feels consistent when zoomed out
		speed *= _targetZoom / MinZoom * 0.5f + 0.5f;

		_targetPosition += direction * speed * dt;
		_targetPosition.X = Mathf.Clamp(_targetPosition.X, MapMin.X, MapMax.X);
		_targetPosition.Z = Mathf.Clamp(_targetPosition.Z, MapMin.Y, MapMax.Y);

		// Rotation
		float rot = Input.GetAxis("camera_rotate_left", "camera_rotate_right");
		_targetYaw += rot * RotateSpeed * dt;

		// Smooth everything toward targets (framerate independent)
		float t = 1f - Mathf.Exp(-Smoothing * dt);

		GlobalPosition = GlobalPosition.Lerp(_targetPosition, t);

		Vector3 rotation = Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, _targetYaw, t);
		Rotation = rotation;

		_zoom = Mathf.Lerp(_zoom, _targetZoom, t);
		_camera.Position = _zoomDir * _zoom;
	}

	private Vector2 GetEdgeScrollInput()
	{
		Viewport viewport = GetViewport();
		Vector2 mouse = viewport.GetMousePosition();
		Vector2 size = viewport.GetVisibleRect().Size;

		// Ignore if mouse is outside the window
		if (mouse.X < 0 || mouse.Y < 0 || mouse.X > size.X || mouse.Y > size.Y)
			return Vector2.Zero;

		Vector2 result = Vector2.Zero;
		if (mouse.X <= EdgeMargin) result.X = -1;
		else if (mouse.X >= size.X - EdgeMargin) result.X = 1;
		if (mouse.Y <= EdgeMargin) result.Y = -1;
		else if (mouse.Y >= size.Y - EdgeMargin) result.Y = 1;
		return result;
	}
}
