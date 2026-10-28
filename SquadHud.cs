using Godot;

public partial class SquadHud : Node2D
{
    private Squad _squad;

    public void Setup(Squad squad)
    {
        _squad = squad;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_squad))
        {
            QueueFree();
            return;
        }

        Camera3D cam = GetViewport().GetCamera3D();
        if (cam == null)
        {
            Visible = false;
            return;
        }

        Vector3 anchor = _squad.GlobalPosition + Vector3.Up * _squad.HudHeight;
        if (cam.IsPositionBehind(anchor))
        {
            Visible = false;
            return;
        }

        Vector2 screen = cam.UnprojectPosition(anchor);
        if (!float.IsFinite(screen.X) || !float.IsFinite(screen.Y))
        {
            Visible = false;
            return;
        }

        Visible = true;
        Position = screen;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_squad == null)
            return;

        float scale = Mathf.Max(_squad.HudScale, 0.1f);
        Vector2 barSize = _squad.HudBarSize * scale;
        int fontSize = Mathf.Max(Mathf.RoundToInt(_squad.HudFontSize * scale), 1);

        float pct = Mathf.Clamp(_squad.Health / _squad.MaxHealth, 0f, 1f);
        Vector2 topLeft = -barSize / 2f; // bar is centered on the node's position

        // Border and background
        DrawRect(new Rect2(topLeft, barSize).Grow(Mathf.Max(1f, scale)), new Color(0, 0, 0, 0.75f));

        // Fill: hue slides from red (empty) to green (full)
        DrawRect(
            new Rect2(topLeft, new Vector2(barSize.X * pct, barSize.Y)),
            Color.FromHsv(pct * 0.33f, 0.85f, 0.95f));

        // Unit type label, only while selected
        if (_squad.IsSelected)
        {
            Font font = ThemeDB.FallbackFont;
            float width = 300f * scale;
            Vector2 pos = new Vector2(-width / 2f, topLeft.Y - 5f * scale);
            int outline = Mathf.Max(Mathf.RoundToInt(4 * scale), 1);

            DrawStringOutline(font, pos, _squad.UnitType, HorizontalAlignment.Center, width, fontSize, outline, Colors.Black);
            DrawString(font, pos, _squad.UnitType, HorizontalAlignment.Center, width, fontSize, Colors.White);
        }
    }
}