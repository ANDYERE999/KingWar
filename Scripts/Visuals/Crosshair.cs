using System;
using Godot;
using KingWar.Entities;

namespace KingWar.Visuals
{
    public partial class Crosshair : Node2D
    {
        [Export] public Soldier? TrackedSoldier { get; set; }

        private float _hitmarkerTimer = 0f;

        public override void _Ready()
        {
            ZIndex = 100;
        }

        public override void _Process(double delta)
        {
            GlobalPosition = GetGlobalMousePosition();

            if (_hitmarkerTimer > 0f)
            {
                _hitmarkerTimer -= (float)delta;
            }

            QueueRedraw();
        }

        public void TriggerHitmarker()
        {
            _hitmarkerTimer = 0.12f;
        }

        public override void _Draw()
        {
            float spreadRadius = 12f;
            if (TrackedSoldier != null)
            {
                spreadRadius = Mathf.Clamp(TrackedSoldier.CurrentSpreadDeg * 6.5f, 10f, 65f);
            }

            // Outer crosshair pips
            Color pipColor = new Color(1f, 1f, 1f, 0.85f);
            float pipLength = 6f;
            float pipWidth = 1.6f;

            // Top
            DrawLine(new Vector2(0, -spreadRadius), new Vector2(0, -spreadRadius - pipLength), pipColor, pipWidth);
            // Bottom
            DrawLine(new Vector2(0, spreadRadius), new Vector2(0, spreadRadius + pipLength), pipColor, pipWidth);
            // Left
            DrawLine(new Vector2(-spreadRadius, 0), new Vector2(-spreadRadius - pipLength, 0), pipColor, pipWidth);
            // Right
            DrawLine(new Vector2(spreadRadius, 0), new Vector2(spreadRadius + pipLength, 0), pipColor, pipWidth);

            // Center dot
            DrawCircle(Vector2.Zero, 2f, new Color(1f, 1f, 1f, 0.9f));

            // Hitmarker (X shape)
            if (_hitmarkerTimer > 0f)
            {
                float a = _hitmarkerTimer / 0.12f;
                Color hitColor = new Color(1f, 0.2f, 0.2f, a);
                float hSize = 8f;
                DrawLine(new Vector2(-hSize, -hSize), new Vector2(hSize, hSize), hitColor, 2f);
                DrawLine(new Vector2(-hSize, hSize), new Vector2(hSize, -hSize), hitColor, 2f);
            }
        }
    }
}
