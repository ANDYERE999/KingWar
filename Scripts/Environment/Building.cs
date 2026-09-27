using System;
using System.Collections.Generic;
using Godot;

namespace KingWar.Environment
{
    public partial class Building : Node2D
    {
        [Export] public Vector2 BuildingSize { get; set; } = new Vector2(240, 160);
        [Export] public Color WallColor { get; set; } = new Color(0.35f, 0.35f, 0.38f);
        [Export] public Color FloorColor { get; set; } = new Color(0.24f, 0.23f, 0.22f);
        [Export] public bool HasLeftDoor { get; set; } = true;
        [Export] public bool HasRightDoor { get; set; } = true;
        [Export] public bool HasTopDoor { get; set; } = false;
        [Export] public bool HasBottomDoor { get; set; } = false;

        private const float WallThickness = 12f;
        private const float DoorWidth = 48f;

        public override void _Ready()
        {
            BuildCollisionWalls();
        }

        private void BuildCollisionWalls()
        {
            float halfW = BuildingSize.X * 0.5f;
            float halfH = BuildingSize.Y * 0.5f;

            // Top Wall
            if (HasTopDoor)
            {
                float segLen = (BuildingSize.X - DoorWidth) * 0.5f;
                AddWallSegment(new Vector2(-halfW + segLen * 0.5f, -halfH + WallThickness * 0.5f), new Vector2(segLen, WallThickness));
                AddWallSegment(new Vector2(halfW - segLen * 0.5f, -halfH + WallThickness * 0.5f), new Vector2(segLen, WallThickness));
            }
            else
            {
                AddWallSegment(new Vector2(0, -halfH + WallThickness * 0.5f), new Vector2(BuildingSize.X, WallThickness));
            }

            // Bottom Wall
            if (HasBottomDoor)
            {
                float segLen = (BuildingSize.X - DoorWidth) * 0.5f;
                AddWallSegment(new Vector2(-halfW + segLen * 0.5f, halfH - WallThickness * 0.5f), new Vector2(segLen, WallThickness));
                AddWallSegment(new Vector2(halfW - segLen * 0.5f, halfH - WallThickness * 0.5f), new Vector2(segLen, WallThickness));
            }
            else
            {
                AddWallSegment(new Vector2(0, halfH - WallThickness * 0.5f), new Vector2(BuildingSize.X, WallThickness));
            }

            // Left Wall
            if (HasLeftDoor)
            {
                float segLen = (BuildingSize.Y - DoorWidth) * 0.5f;
                AddWallSegment(new Vector2(-halfW + WallThickness * 0.5f, -halfH + segLen * 0.5f), new Vector2(WallThickness, segLen));
                AddWallSegment(new Vector2(-halfW + WallThickness * 0.5f, halfH - segLen * 0.5f), new Vector2(WallThickness, segLen));
            }
            else
            {
                AddWallSegment(new Vector2(-halfW + WallThickness * 0.5f, 0), new Vector2(WallThickness, BuildingSize.Y));
            }

            // Right Wall
            if (HasRightDoor)
            {
                float segLen = (BuildingSize.Y - DoorWidth) * 0.5f;
                AddWallSegment(new Vector2(halfW - WallThickness * 0.5f, -halfH + segLen * 0.5f), new Vector2(WallThickness, segLen));
                AddWallSegment(new Vector2(halfW - WallThickness * 0.5f, halfH - segLen * 0.5f), new Vector2(WallThickness, segLen));
            }
            else
            {
                AddWallSegment(new Vector2(halfW - WallThickness * 0.5f, 0), new Vector2(WallThickness, BuildingSize.Y));
            }
        }

        private void AddWallSegment(Vector2 localPos, Vector2 size)
        {
            var body = new StaticBody2D { Position = localPos };
            var col = new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = size }
            };
            body.AddChild(col);
            AddChild(body);
        }

        public override void _Draw()
        {
            float halfW = BuildingSize.X * 0.5f;
            float halfH = BuildingSize.Y * 0.5f;

            // Interior floor
            DrawRect(new Rect2(-halfW, -halfH, BuildingSize.X, BuildingSize.Y), FloorColor);

            // Floor tile grids
            for (float x = -halfW + 32; x < halfW; x += 32)
            {
                DrawLine(new Vector2(x, -halfH), new Vector2(x, halfH), new Color(0.2f, 0.19f, 0.18f, 0.5f), 1f);
            }
            for (float y = -halfH + 32; y < halfH; y += 32)
            {
                DrawLine(new Vector2(-halfW, y), new Vector2(halfW, y), new Color(0.2f, 0.19f, 0.18f, 0.5f), 1f);
            }

            // Draw walls corresponding to collision
            DrawWallParts(halfW, halfH);
        }

        private void DrawWallParts(float halfW, float halfH)
        {
            Color border = WallColor.Darkened(0.3f);

            // Helper to draw wall segment
            void DrawSeg(Vector2 pos, Vector2 size)
            {
                Rect2 r = new Rect2(pos - size * 0.5f, size);
                DrawRect(r, WallColor);
                DrawRect(r, border, false, 1.5f);
            }

            // Top
            if (HasTopDoor)
            {
                float segLen = (BuildingSize.X - DoorWidth) * 0.5f;
                DrawSeg(new Vector2(-halfW + segLen * 0.5f, -halfH + WallThickness * 0.5f), new Vector2(segLen, WallThickness));
                DrawSeg(new Vector2(halfW - segLen * 0.5f, -halfH + WallThickness * 0.5f), new Vector2(segLen, WallThickness));
            }
            else
            {
                DrawSeg(new Vector2(0, -halfH + WallThickness * 0.5f), new Vector2(BuildingSize.X, WallThickness));
            }

            // Bottom
            if (HasBottomDoor)
            {
                float segLen = (BuildingSize.X - DoorWidth) * 0.5f;
                DrawSeg(new Vector2(-halfW + segLen * 0.5f, halfH - WallThickness * 0.5f), new Vector2(segLen, WallThickness));
                DrawSeg(new Vector2(halfW - segLen * 0.5f, halfH - WallThickness * 0.5f), new Vector2(segLen, WallThickness));
            }
            else
            {
                DrawSeg(new Vector2(0, halfH - WallThickness * 0.5f), new Vector2(BuildingSize.X, WallThickness));
            }

            // Left
            if (HasLeftDoor)
            {
                float segLen = (BuildingSize.Y - DoorWidth) * 0.5f;
                DrawSeg(new Vector2(-halfW + WallThickness * 0.5f, -halfH + segLen * 0.5f), new Vector2(WallThickness, segLen));
                DrawSeg(new Vector2(-halfW + WallThickness * 0.5f, halfH - segLen * 0.5f), new Vector2(WallThickness, segLen));
            }
            else
            {
                DrawSeg(new Vector2(-halfW + WallThickness * 0.5f, 0), new Vector2(WallThickness, BuildingSize.Y));
            }

            // Right
            if (HasRightDoor)
            {
                float segLen = (BuildingSize.Y - DoorWidth) * 0.5f;
                DrawSeg(new Vector2(halfW - WallThickness * 0.5f, -halfH + segLen * 0.5f), new Vector2(WallThickness, segLen));
                DrawSeg(new Vector2(halfW - WallThickness * 0.5f, halfH - segLen * 0.5f), new Vector2(WallThickness, segLen));
            }
            else
            {
                DrawSeg(new Vector2(halfW - WallThickness * 0.5f, 0), new Vector2(WallThickness, BuildingSize.Y));
            }
        }
    }
}
