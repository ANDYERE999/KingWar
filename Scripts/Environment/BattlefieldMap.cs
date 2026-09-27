using System;
using System.Collections.Generic;
using Godot;
using KingWar.Common;
using KingWar.Objectives;

namespace KingWar.Environment
{
    public partial class BattlefieldMap : BattlefieldBase
    {
        /// <summary>Fixed size of the hand-authored demo strip.</summary>
        public const float Width = 5000f;
        public const float Height = 1200f;

        private readonly List<CaptureNode> _captureNodes = new();
        private readonly List<Rect2> _obstacleRects = new();

        public override float MapWidth => Width;
        public override float MapHeight => Height;
        public override IReadOnlyList<CaptureNode> CaptureNodes => _captureNodes;
        public override IReadOnlyList<Rect2> ObstacleRectangles => _obstacleRects;
        public override Vector2 AttackerStagingPosition => new(200f, 600f);
        public override Vector2 DefenderRearPosition => new(4850f, 600f);

        public override void InitializeMap()
        {
            if (_obstacleRects.Count > 0) return;
            BuildMapBoundaries();
            BuildAllSectors();
        }

        private void BuildMapBoundaries()
        {
            // Outer collision bounds
            AddBoundaryWall(new Vector2(MapWidth * 0.5f, -15), new Vector2(MapWidth + 100, 30)); // Top
            AddBoundaryWall(new Vector2(MapWidth * 0.5f, MapHeight + 15), new Vector2(MapWidth + 100, 30)); // Bottom
            AddBoundaryWall(new Vector2(-15, MapHeight * 0.5f), new Vector2(30, MapHeight + 100)); // Left
            AddBoundaryWall(new Vector2(MapWidth + 15, MapHeight * 0.5f), new Vector2(30, MapHeight + 100)); // Right
        }

        private void AddBoundaryWall(Vector2 pos, Vector2 size)
        {
            _obstacleRects.Add(new Rect2(pos - size * 0.5f, size));
            var body = new StaticBody2D { Position = pos };
            var col = new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = size }
            };
            body.AddChild(col);
            AddChild(body);
        }

        private void BuildAllSectors()
        {
            // === ZONE 0: 攻方集结大本营 (Attacker Staging Base - No Capture Flag) ===
            // Located at X = 200, Y = 600
            SpawnCover(CoverType.Sandbag, new Vector2(240, 460), new Vector2(100, 20));
            SpawnCover(CoverType.Sandbag, new Vector2(240, 740), new Vector2(100, 20));
            SpawnCover(CoverType.Crate, new Vector2(160, 500), new Vector2(36, 36));
            SpawnCover(CoverType.Crate, new Vector2(160, 700), new Vector2(36, 36));
            SpawnCover(CoverType.Tree, new Vector2(350, 300), new Vector2(80, 80));
            SpawnCover(CoverType.Tree, new Vector2(350, 900), new Vector2(80, 80));

            // === SECTOR 0 (A): 但泽 (Danzig) — 波罗的海造船厂与港口滩头 ===
            // Node A at X = 1250, Y = 600
            var nodeA = CreateNode(0, "但泽", "A", new Vector2(1250, 600));
            // 港口仓库（可摧毁）与码头集装箱堆场，中央保留 160px 主干道走廊
            SpawnBuilding(new Vector2(1250, 360), new Vector2(240, 140), true, true, false, true);
            SpawnCover(CoverType.MetalContainer, new Vector2(1060, 480), new Vector2(90, 40));
            SpawnCover(CoverType.MetalContainer, new Vector2(1440, 480), new Vector2(90, 40));
            SpawnCover(CoverType.MetalContainer, new Vector2(1060, 730), new Vector2(90, 40));
            SpawnCover(CoverType.MetalContainer, new Vector2(1440, 730), new Vector2(90, 40));
            SpawnCover(CoverType.Sandbag, new Vector2(1250, 490), new Vector2(90, 20));
            SpawnCover(CoverType.Sandbag, new Vector2(1250, 715), new Vector2(90, 20));
            SpawnCover(CoverType.Crate, new Vector2(980, 715), new Vector2(36, 36));
            SpawnCover(CoverType.Crate, new Vector2(1520, 490), new Vector2(36, 36));
            SpawnCover(CoverType.Tree, new Vector2(950, 280), new Vector2(85, 85));
            SpawnCover(CoverType.Tree, new Vector2(1560, 930), new Vector2(85, 85));

            // === SECTOR 1 (B): 波兹南 (Poznan) — 要塞城堡与交叉火力 ===
            // Node B at X = 2700, Y = 600
            var nodeB = CreateNode(1, "波兹南", "B", new Vector2(2700, 600));
            // 波兹南要塞（钢筋混凝土永备工事，不可摧毁）与两翼混凝土火力点
            SpawnBuilding(new Vector2(2700, 360), new Vector2(260, 150), true, false, true, true);
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(2530, 490), new Vector2(60, 24));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(2870, 490), new Vector2(60, 24));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(2530, 715), new Vector2(60, 24));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(2870, 715), new Vector2(60, 24));
            SpawnCover(CoverType.Sandbag, new Vector2(2700, 485), new Vector2(110, 24));
            SpawnCover(CoverType.Sandbag, new Vector2(2700, 720), new Vector2(110, 24));
            SpawnCover(CoverType.Tree, new Vector2(2380, 270), new Vector2(85, 85));
            SpawnCover(CoverType.Tree, new Vector2(3020, 930), new Vector2(85, 85));

            // === SECTOR 2 (C): 华沙 (Warsaw) — 起义街区废墟与中央街垒 ===
            // Node C at X = 4100, Y = 600
            var nodeC = CreateNode(2, "华沙", "C", new Vector2(4100, 600));
            // 南北两片起义废墟街区（均可摧毁）与维斯瓦河西岸街垒阵地
            SpawnBuilding(new Vector2(4040, 355), new Vector2(250, 150), true, true, false, true);
            SpawnBuilding(new Vector2(4260, 850), new Vector2(240, 150), true, true, true, false);
            SpawnCover(CoverType.Sandbag, new Vector2(3890, 490), new Vector2(90, 24));
            SpawnCover(CoverType.Sandbag, new Vector2(4300, 715), new Vector2(90, 24));
            SpawnCover(CoverType.Crate, new Vector2(3960, 715), new Vector2(36, 36));
            SpawnCover(CoverType.Crate, new Vector2(4240, 490), new Vector2(36, 36));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(4440, 490), new Vector2(60, 24));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(3760, 715), new Vector2(60, 24));
            SpawnCover(CoverType.Tree, new Vector2(3820, 260), new Vector2(80, 80));
            SpawnCover(CoverType.Tree, new Vector2(4460, 930), new Vector2(85, 85));

            // === ZONE 6: 守方战略后方要塞基地 (Defender Rear Strategic Base - No Capture Flag) ===
            // Located at X = 4850, Y = 600
            SpawnCover(CoverType.Sandbag, new Vector2(4760, 470), new Vector2(100, 24));
            SpawnCover(CoverType.Sandbag, new Vector2(4760, 730), new Vector2(100, 24));
            SpawnCover(CoverType.MetalContainer, new Vector2(4880, 420), new Vector2(80, 36));
            SpawnCover(CoverType.MetalContainer, new Vector2(4880, 780), new Vector2(80, 36));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(4940, 560), new Vector2(40, 24));
            SpawnCover(CoverType.ConcreteBarrier, new Vector2(4940, 640), new Vector2(40, 24));
        }

        private CaptureNode CreateNode(int index, string name, string letter, Vector2 pos)
        {
            var node = new CaptureNode
            {
                SectorIndex = index,
                SectorName = name,
                SectorLetter = letter,
                GlobalPosition = pos,
                CaptureRadius = 160f
            };
            AddChild(node);
            _captureNodes.Add(node);
            return node;
        }

        private void SpawnBuilding(Vector2 pos, Vector2 size, bool leftDoor, bool rightDoor, bool topDoor, bool bottomDoor)
        {
            _obstacleRects.Add(new Rect2(pos - size * 0.5f, size));
            var b = new Building
            {
                Position = pos,
                BuildingSize = size,
                HasLeftDoor = leftDoor,
                HasRightDoor = rightDoor,
                HasTopDoor = topDoor,
                HasBottomDoor = bottomDoor
            };
            AddChild(b);
        }

        private void SpawnCover(CoverType type, Vector2 pos, Vector2 size)
        {
            _obstacleRects.Add(new Rect2(pos - size * 0.5f, size));
            var c = new CoverObject
            {
                Position = pos,
                Type = type,
                Size = size,
                IsDestructible = (type == CoverType.Crate || type == CoverType.Sandbag)
            };
            AddChild(c);
        }

        public override void _Draw()
        {
            // Base terrain ground: military battlefield olive/earth
            DrawRect(new Rect2(0, 0, MapWidth, MapHeight), new Color(0.25f, 0.31f, 0.20f));

            // Zone 0 Attacker staging base ground indicator
            DrawRect(new Rect2(50, 420, 300, 360), new Color(0.20f, 0.26f, 0.32f, 0.45f));
            DrawRect(new Rect2(50, 420, 300, 360), GameColors.AttackerBlue, false, 2f);

            // Zone 6 Defender rear base ground indicator
            DrawRect(new Rect2(4700, 420, 250, 360), new Color(0.32f, 0.20f, 0.20f, 0.45f));
            DrawRect(new Rect2(4700, 420, 250, 360), GameColors.DefenderRed, false, 2f);

            // Main central military asphalt / dirt road (160px wide, unobstructed)
            DrawRect(new Rect2(0, 520, MapWidth, 160), new Color(0.38f, 0.35f, 0.28f));
            // Road dashes
            for (float x = 20; x < MapWidth; x += 60)
            {
                DrawLine(new Vector2(x, 600), new Vector2(x + 30, 600), new Color(0.5f, 0.46f, 0.36f, 0.6f), 3f);
            }

            // North and South flanking dirt trails
            DrawRect(new Rect2(0, 260, MapWidth, 70), new Color(0.30f, 0.28f, 0.22f));
            DrawRect(new Rect2(0, 870, MapWidth, 70), new Color(0.30f, 0.28f, 0.22f));

            // Decorative mud patches and craters
            DrawCircle(new Vector2(560, 450), 35f, new Color(0.20f, 0.17f, 0.12f, 0.7f));
            DrawCircle(new Vector2(1900, 720), 45f, new Color(0.20f, 0.17f, 0.12f, 0.7f));
            DrawCircle(new Vector2(2280, 440), 50f, new Color(0.20f, 0.17f, 0.12f, 0.7f));
            DrawCircle(new Vector2(3380, 750), 55f, new Color(0.20f, 0.17f, 0.12f, 0.7f));
            DrawCircle(new Vector2(4620, 470), 40f, new Color(0.20f, 0.17f, 0.12f, 0.7f));
        }
    }
}
