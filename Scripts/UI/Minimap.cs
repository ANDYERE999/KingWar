using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Environment;
using KingWar.Objectives;

namespace KingWar.UI
{
    /// <summary>
    /// Top-left tactical minimap. Draws the battlefield ground raster, capture objectives and
    /// every living soldier, preserving the real map aspect ratio. Press <c>M</c> to enlarge.
    /// </summary>
    public partial class Minimap : Control
    {
        [Export] public Vector2 MinimapSize { get; set; } = new(240, 150);
        [Export] public Vector2 EnlargedSize { get; set; } = new(680, 420);
        [Export] public float ScreenMargin { get; set; } = 20f;

        public bool IsEnlarged { get; private set; }

        /// <summary>Where the battlefield is actually drawn inside the frame, in control space.</summary>
        private Rect2 _contentRect;

        public override void _Ready()
        {
            // Never swallow mouse input: the player shoots through this area.
            MouseFilter = MouseFilterEnum.Ignore;
            ApplyLayout();
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public void ToggleEnlarged()
        {
            IsEnlarged = !IsEnlarged;
            ApplyLayout();
            QueueRedraw();
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.M)
            {
                ToggleEnlarged();
                AcceptEvent();
            }
        }

        private void ApplyLayout()
        {
            Vector2 size = IsEnlarged ? EnlargedSize : MinimapSize;

            SetAnchorsPreset(LayoutPreset.TopLeft);
            OffsetLeft = ScreenMargin;
            OffsetTop = ScreenMargin;
            OffsetRight = ScreenMargin + size.X;
            OffsetBottom = ScreenMargin + size.Y;
        }

        private Vector2 CurrentSize => IsEnlarged ? EnlargedSize : MinimapSize;

        public override void _Draw()
        {
            Vector2 box = CurrentSize;
            var frame = new Rect2(Vector2.Zero, box);

            DrawRect(frame, new Color(0.05f, 0.08f, 0.06f, 0.88f));

            var battlefield = BattlefieldBase.Current;
            float mapWidth = ObjectiveManager.Instance?.MapWidth ?? 5000f;
            float mapHeight = ObjectiveManager.Instance?.MapHeight ?? 1200f;
            if (mapWidth <= 0f || mapHeight <= 0f)
            {
                DrawRect(frame, new Color(0.25f, 0.42f, 0.25f, 0.95f), false, 2f);
                return;
            }

            // Fit the battlefield inside the frame while keeping its true aspect ratio.
            const float padding = 6f;
            float scale = Mathf.Min((box.X - padding * 2f) / mapWidth, (box.Y - padding * 2f) / mapHeight);
            var contentSize = new Vector2(mapWidth * scale, mapHeight * scale);
            var origin = new Vector2((box.X - contentSize.X) * 0.5f, (box.Y - contentSize.Y) * 0.5f);
            _contentRect = new Rect2(origin, contentSize);

            var ground = battlefield?.GroundTexture;
            if (ground != null)
            {
                DrawTextureRect(ground, _contentRect, false);
            }
            else
            {
                DrawRect(_contentRect, new Color(0.14f, 0.19f, 0.13f));
            }
            DrawRect(_contentRect, new Color(0.35f, 0.45f, 0.35f, 0.6f), false, 1f);

            DrawObjectives(scale);
            DrawSoldiers(scale);

            DrawRect(frame, new Color(0.25f, 0.45f, 0.25f, 0.95f), false, 2f);
            DrawHint(box);
        }

        private Vector2 ToMinimap(Vector2 worldPosition, float scale) =>
            _contentRect.Position + worldPosition * scale;

        private void DrawObjectives(float scale)
        {
            if (ObjectiveManager.Instance == null) return;

            bool enlarged = IsEnlarged;

            foreach (var node in ObjectiveManager.Instance.Nodes)
            {
                Color color = node.State switch
                {
                    SectorState.Captured => GameColors.AttackerBlue,
                    SectorState.Active => GameColors.ContestedYellow,
                    _ => Colors.DimGray
                };

                Vector2 centre = ToMinimap(node.GlobalPosition, scale);

                // Show the real capture footprint so the objective size is readable on the map.
                float radius = node.CaptureRadius * scale;
                DrawCircle(centre, radius, new Color(color.R, color.G, color.B, 0.22f));
                DrawArc(centre, radius, 0f, Mathf.Tau, 32, new Color(color.R, color.G, color.B, 0.7f), 1.5f, true);

                DrawCircle(centre, enlarged ? 7f : 5f, color);
                DrawCircle(centre, enlarged ? 3f : 2f, Colors.White);

                if (enlarged)
                {
                    var font = ThemeDB.FallbackFont;
                    if (font != null)
                    {
                        DrawString(font, centre + new Vector2(0f, -12f), node.SectorLetter,
                            HorizontalAlignment.Center, 0f, 13, Colors.White);
                    }
                }
            }
        }

        private void DrawSoldiers(float scale)
        {
            bool enlarged = IsEnlarged;
            float friendlyRadius = enlarged ? 3.5f : 2.5f;

            foreach (var node in GetTree().GetNodesInGroup("Soldiers"))
            {
                if (node is not Soldier soldier || soldier.IsDead) continue;

                Vector2 position = ToMinimap(soldier.GlobalPosition, scale);

                if (soldier.IsPlayerControlled)
                {
                    DrawCircle(position, enlarged ? 6f : 4f, Colors.White);
                    Vector2 forward = Vector2.Right.Rotated(soldier.Rotation) * (enlarged ? 10f : 6f);
                    DrawLine(position, position + forward, Colors.Yellow, 2f);
                }
                else if (soldier.Team == Team.Attackers)
                {
                    DrawCircle(position, friendlyRadius, GameColors.AttackerBlue);
                }
                else
                {
                    DrawCircle(position, friendlyRadius, GameColors.DefenderRed);
                }
            }
        }

        private void DrawHint(Vector2 box)
        {
            var font = ThemeDB.FallbackFont;
            if (font == null) return;

            string hint = IsEnlarged ? "M: 缩小" : "M: 放大";
            DrawString(font, new Vector2(8f, box.Y - 7f), hint,
                HorizontalAlignment.Left, -1f, 12, new Color(0.78f, 0.85f, 0.78f, 0.9f));
        }
    }
}
