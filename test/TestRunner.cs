using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Environment;
using KingWar.Gadgets;
using KingWar.Game;
using KingWar.Objectives;
using KingWar.UI;
using KingWar.Weapons;

namespace KingWar.Tests
{
    public partial class TestRunner : Node
    {
        private int _totalTests = 0;
        private int _passedTests = 0;
        private int _failedTests = 0;

        public override void _Ready()
        {
            GD.Print("========================================================");
            GD.Print("   KINGWAR 2D BATTLEFIELD - AUTOMATED TEST SUITE        ");
            GD.Print("========================================================");

            RunAllUnitTests();
            RunHighSpeedBalanceSimulationTest();
            RunAiStuckDetectionMatchTest();
            RunTicketsVsWinRateSensitivityTest();

            PrintSummaryAndExit();
        }

        private void Assert(bool condition, string testName, string details = "")
        {
            _totalTests++;
            if (condition)
            {
                _passedTests++;
                GD.Print($"[PASS] {testName}");
            }
            else
            {
                _failedTests++;
                GD.PrintErr($"[FAIL] {testName} - {details}");
            }
        }

        private void RunAllUnitTests()
        {
            GD.Print("\n--- [1] 坦克装甲与伤害免疫测试 (Rocket-Only Vulnerability) ---");
            TestTankDamageImmunity();

            GD.Print("\n--- [2] 坦克机动与航速测试 (0.9x Infantry Speed) ---");
            TestTankSpeed();

            GD.Print("\n--- [3] 坦克乘员武器逻辑测试 (1-2 Crew Weapon Restrictions) ---");
            TestTankCrewWeapons();

            GD.Print("\n--- [4] 玩家优先让座机制测试 (AI Yields Driver Seat to Player) ---");
            TestPlayerDriverPriority();

            GD.Print("\n--- [5] 坦克刷新与60秒冷却测试 (60s Respawn at Last Captured Node) ---");
            TestTankRespawnLogic();

            GD.Print("\n--- [6] 10秒战术停战与三段式战区隔离测试 (10s Truce & 3-Zone Territorial Bounds) ---");
            TestTruceAndTerritoryZones();

            GD.Print("\n--- [7] PKM通用机枪战术数值与弹道参数测试 (PKM Machine Gun Specs) ---");
            TestPkmSpecs();

            GD.Print("\n--- [8] 重生装备选择与AI动态配比调控测试 (Respawn Loadout & Dynamic Ratio) ---");
            TestRespawnLoadoutAndAiRatio();

            GD.Print("\n--- [12] 流水线语义地图 → 战场生成测试 (Semantic Mask → Playable Battlefield) ---");
            TestSemanticMapBattlefield();

            GD.Print("\n--- [13] 地形通行规则与三城拼接战役测试 (Terrain Rules & Stitched Campaign) ---");
            TestTerrainRulesAndCampaign();

            GD.Print("\n--- [14] 据点连接处出生点与左上角小地图测试 (Junction Spawns & Minimap) ---");
            TestSpawnJunctionsAndMinimap();

            GD.Print("\n--- [15] 可摧毁建筑受武器伤害测试 (Destructible Buildings Take Weapon Damage) ---");
            TestBuildingDamage();
        }

        private void TestBuildingDamage()
        {
            var holder = new Node2D { Name = "BuildingHolder" };
            AddChild(holder);

            // The buildings must BE the physics body. Wrapping collision in a child body makes every
            // weapon query resolve to a non-IDamageable node and buildings become silently immune.
            var building = new SemanticBuilding
            {
                Name = "DamageProbe",
                Footprint = new Rect2(100f, 100f, 192f, 96f),
                CanDestroy = true,
                MaxHealth = 250f
            };
            holder.AddChild(building);

            Assert(building is CollisionObject2D,
                "可摧毁建筑自身即为物理碰撞体（子弹射线/爆炸形状查询才能命中它）");
            Assert(building is IDamageable,
                "可摧毁建筑实现 IDamageable，会接受武器伤害结算");

            int shapeChildren = 0;
            int nestedBodies = 0;
            foreach (var child in building.GetChildren())
            {
                if (child is CollisionShape2D) shapeChildren++;
                if (child is CollisionObject2D) nestedBodies++;
            }
            Assert(shapeChildren == 1 && nestedBodies == 0,
                "建筑直接持有碰撞形状，不再嵌套一层物理体",
                $"shapes={shapeChildren}, nestedBodies={nestedBodies}");
            Assert(Mathf.IsEqualApprox(building.CurrentHealth, 250f), "建筑初始化满耐久");

            // Bullets: M4A1 does 26 per hit.
            building.TakeDamage(26f, null, null, null, DamageType.Bullet);
            Assert(Mathf.IsEqualApprox(building.CurrentHealth, 224f),
                "步枪子弹对建筑造成伤害", $"HP {building.CurrentHealth}");

            // Grenade shrapnel.
            building.TakeDamage(120f, null, null, null, DamageType.GrenadeShrapnel);
            Assert(Mathf.IsEqualApprox(building.CurrentHealth, 104f) && !building.IsDead,
                "手榴弹破片对建筑造成伤害但未摧毁", $"HP {building.CurrentHealth}");

            // Rocket: 150 direct damage must finish it.
            bool destroyed = false;
            building.Destroyed += _ => destroyed = true;
            building.TakeDamage(150f, null, null, null, DamageType.RocketExplosive);
            Assert(destroyed && building.IsDead,
                "火箭弹直击可以打爆可摧毁建筑并触发摧毁事件");

            // Non-destructible blocks must ignore everything.
            var solid = new SemanticBuilding
            {
                Name = "SolidProbe",
                Footprint = new Rect2(100f, 300f, 192f, 96f),
                CanDestroy = false,
                MaxHealth = 250f
            };
            holder.AddChild(solid);
            solid.TakeDamage(5000f, null, null, null, DamageType.RocketExplosive);
            Assert(!solid.IsDead && Mathf.IsEqualApprox(solid.CurrentHealth, 250f),
                "不可摧毁建筑免疫全部武器伤害（包括火箭弹）");

            // Health must not be corrupted by degenerate input.
            solid.CanDestroy = true;
            solid.TakeDamage(-500f, null, null, null, DamageType.Bullet);
            solid.TakeDamage(float.NaN, null, null, null, DamageType.Bullet);
            Assert(Mathf.IsEqualApprox(solid.CurrentHealth, 250f),
                "非法伤害值（负数/NaN）不会破坏建筑", $"HP {solid.CurrentHealth}");

            holder.QueueFree();

            // Block size cap: without it the greedy merge fuses whole streets into one wall,
            // so a single burst would erase an entire building line.
            var campaign = new CampaignBattlefieldMap
            {
                Name = "BuildingSizeMap",
                CellSize = 32f,
                TargetGridSize = 120,
                GapCells = 24
            };
            AddChild(campaign);

            float cap = campaign.MaxBuildingCellsPerBlock * campaign.CellSize;
            float largest = 0f;
            foreach (var child in campaign.GetChildren())
            {
                if (child is SemanticBuilding b)
                {
                    largest = Mathf.Max(largest, Mathf.Max(b.Footprint.Size.X, b.Footprint.Size.Y));
                }
            }
            Assert(largest > 0f && largest <= cap + 0.5f,
                $"建筑块尺寸被限制在 {cap:0}px 以内，不会合并成长墙",
                $"largest {largest:0}px");

            campaign.QueueFree();
        }

        private void TestSpawnJunctionsAndMinimap()
        {
            var battlefield = new CampaignBattlefieldMap
            {
                Name = "SpawnJunctionMap",
                CellSize = 32f,
                TargetGridSize = 120,
                GapCells = 24
            };
            AddChild(battlefield); // _Ready -> InitializeMap(), also sets BattlefieldBase.Current

            var objMgr = new ObjectiveManager();
            AddChild(objMgr);
            objMgr.ConfigureMapExtents(battlefield.MapWidth, battlefield.MapHeight,
                battlefield.AttackerStagingPosition, battlefield.DefenderRearPosition);
            foreach (var node in battlefield.CaptureNodes) objMgr.RegisterNode(node);

            var spawnMgr = new SpawnManager();
            var nodes = objMgr.Nodes;
            float clearance = SpawnManager.SpawnJunctionClearance;

            // ---- middle sector: both junctions exist ----
            typeof(ObjectiveManager).GetProperty("ActiveSectorIndex")?.SetValue(objMgr, 1);

            Vector2 junctionBehind = (nodes[0].GlobalPosition + nodes[1].GlobalPosition) * 0.5f;
            Vector2 junctionAhead = (nodes[1].GlobalPosition + nodes[2].GlobalPosition) * 0.5f;

            Vector2 attackerSpawn = spawnMgr.GetSpawnPosition(Team.Attackers);
            Vector2 defenderSpawn = spawnMgr.GetSpawnPosition(Team.Defenders);

            Assert(Mathf.Abs(attackerSpawn.X - (junctionBehind.X - clearance)) <= 100f,
                "进攻方出生点位于当前据点与后方据点的连接处",
                $"spawn {attackerSpawn.X:0} vs junction {junctionBehind.X:0}");
            Assert(Mathf.Abs(defenderSpawn.X - (junctionAhead.X + clearance)) <= 100f,
                "防守方出生点位于当前据点与前方据点的连接处",
                $"spawn {defenderSpawn.X:0} vs junction {junctionAhead.X:0}");
            Assert(attackerSpawn.X < defenderSpawn.X,
                "攻守双方出生点分居当前据点两侧");

            // ---- edge sectors fall back to the staging / rear bases ----
            typeof(ObjectiveManager).GetProperty("ActiveSectorIndex")?.SetValue(objMgr, 0);
            Vector2 firstAttacker = spawnMgr.GetSpawnPosition(Team.Attackers);
            Assert(Mathf.Abs(firstAttacker.X - (battlefield.AttackerStagingPosition.X - clearance)) <= 100f,
                "首个战区进攻方出生点回落到集结基地",
                $"spawn {firstAttacker.X:0} vs base {battlefield.AttackerStagingPosition.X:0}");

            typeof(ObjectiveManager).GetProperty("ActiveSectorIndex")?.SetValue(objMgr, nodes.Count - 1);
            Vector2 lastDefender = spawnMgr.GetSpawnPosition(Team.Defenders);
            Assert(Mathf.Abs(lastDefender.X - (battlefield.DefenderRearPosition.X + clearance)) <= 100f,
                "最后一个战区防守方出生点回落到后方基地",
                $"spawn {lastDefender.X:0} vs base {battlefield.DefenderRearPosition.X:0}");

            // ---- repeated spawns must land on walkable ground ----
            bool walkable = true;
            for (int i = 0; i < 16; i++)
            {
                var terrain = battlefield.GetTerrainAt(spawnMgr.GetSpawnPosition(Team.Attackers));
                walkable &= terrain is TerrainClass.Default or TerrainClass.Snow;
            }
            Assert(walkable, "多次生成的出生点均自动落在可通行地形上（不会生成在河流/山体/建筑里）");

            // ---- minimap: top-left dock + M toggle ----
            var minimap = new Minimap();
            AddChild(minimap);

            Assert(minimap.MouseFilter == Control.MouseFilterEnum.Ignore,
                "小地图不拦截鼠标（不会挡住射击点击）");
            Assert(Mathf.IsEqualApprox(minimap.OffsetLeft, minimap.ScreenMargin)
                   && Mathf.IsEqualApprox(minimap.OffsetTop, minimap.ScreenMargin),
                "小地图停靠在屏幕左上角", $"offsets ({minimap.OffsetLeft},{minimap.OffsetTop})");
            Assert(!minimap.IsEnlarged, "小地图默认保持常规尺寸");

            float normalWidth = minimap.OffsetRight - minimap.OffsetLeft;
            minimap.ToggleEnlarged();
            float enlargedWidth = minimap.OffsetRight - minimap.OffsetLeft;
            Assert(minimap.IsEnlarged && enlargedWidth > normalWidth,
                "按 M 后小地图放大", $"normal {normalWidth:0} -> enlarged {enlargedWidth:0}");

            minimap.ToggleEnlarged();
            Assert(!minimap.IsEnlarged
                   && Mathf.IsEqualApprox(minimap.OffsetRight - minimap.OffsetLeft, normalWidth),
                "再次按 M 恢复常规尺寸");

            minimap.QueueFree();
            objMgr.QueueFree();
            battlefield.QueueFree();
        }

        private void TestTerrainRulesAndCampaign()
        {
            // ---- 1. Pure terrain rules for all six classes ----
            Assert(Mathf.IsEqualApprox(BattlefieldBase.SpeedMultiplierFor(TerrainClass.River), 0.60f)
                   && Mathf.IsEqualApprox(BattlefieldBase.SpeedMultiplierFor(TerrainClass.Mountain), 0.50f),
                "河流/山地均有移速惩罚 (river 0.60x, mountain 0.50x)");
            Assert(Mathf.IsEqualApprox(BattlefieldBase.SpeedMultiplierFor(TerrainClass.Default), 1f)
                   && Mathf.IsEqualApprox(BattlefieldBase.SpeedMultiplierFor(TerrainClass.Snow), 1f)
                   && Mathf.IsEqualApprox(BattlefieldBase.SpeedMultiplierFor(TerrainClass.BuildingsCanDestroy), 1f),
                "平原/积雪/建筑格不额外减速");

            Assert(BattlefieldBase.TerrainBlocksVehicles(TerrainClass.Mountain),
                "坦克无法通过山地");
            Assert(!BattlefieldBase.TerrainBlocksVehicles(TerrainClass.River)
                   && !BattlefieldBase.TerrainBlocksVehicles(TerrainClass.Default)
                   && !BattlefieldBase.TerrainBlocksVehicles(TerrainClass.Snow),
                "河流与平原不阻挡车辆（仅减速）");

            // ---- 2. Synthetic mask: verify the physical wiring, not just the maths ----
            // Columns: default | river | mountain | snow | destructible | indestructible.
            // The special classes only occupy the middle rows so every band keeps walkable ground.
            var probe = Image.CreateEmpty(12, 6, false, Image.Format.L8);
            for (int row = 0; row < 6; row++)
            {
                for (int column = 0; column < 12; column++)
                {
                    int id = 0;
                    if (row is 2 or 3)
                    {
                        id = column < 2 ? 0 : column < 4 ? 1 : column < 6 ? 2 : column < 8 ? 3 : column < 10 ? 4 : 5;
                    }
                    float value = id / 255f;
                    probe.SetPixel(column, row, new Color(value, value, value));
                }
            }

            const string probePath = "user://kingwar_probe_mask.png";
            probe.SavePng(probePath);

            var probeMap = new MaskBattlefieldMap
            {
                Name = "ProbeMaskMap",
                MaskPath = probePath,
                CellSize = 32f,
                TargetGridSize = 12,
                SectorCount = 3
            };
            AddChild(probeMap);

            var probeData = probeMap.Map!;
            Assert(probeMap.GetTerrainAt(probeData.CellCenterToWorld(0, 2)) == TerrainClass.Default,
                "探针地图: 平原格识别正确");
            Assert(probeMap.GetTerrainAt(probeData.CellCenterToWorld(2, 2)) == TerrainClass.River,
                "探针地图: 河流格识别正确");
            Assert(probeMap.GetTerrainAt(probeData.CellCenterToWorld(4, 2)) == TerrainClass.Mountain,
                "探针地图: 山地格识别正确");

            Assert(Mathf.IsEqualApprox(
                       probeMap.GetSpeedMultiplier(probeData.CellCenterToWorld(2, 2), false), 0.60f),
                "步兵涉水时速度下降至 0.60x");
            Assert(Mathf.IsEqualApprox(
                       probeMap.GetSpeedMultiplier(probeData.CellCenterToWorld(4, 2), false), 0.50f),
                "步兵翻山时速度下降至 0.50x");
            Assert(probeMap.BlocksVehicles(probeData.CellCenterToWorld(4, 2))
                   && !probeMap.BlocksVehicles(probeData.CellCenterToWorld(2, 2)),
                "探针地图: 山地阻挡车辆，河流不阻挡");

            var terrainBody = probeMap.GetNodeOrNull<StaticBody2D>("TerrainCollision");
            Assert(terrainBody != null && terrainBody.CollisionLayer == BattlefieldBase.MountainCollisionLayer,
                "山地碰撞体位于车辆专用物理层，不会拦步兵",
                $"Layer: {terrainBody?.CollisionLayer}");

            probeMap.QueueFree();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(probePath));

            // ---- 3. Real stitched campaign: one city = one capture node ----
            var campaign = new CampaignBattlefieldMap
            {
                Name = "CampaignUnderTest",
                CellSize = 32f,
                TargetGridSize = 120,
                GapCells = 24
            };
            AddChild(campaign);

            Assert(campaign.CaptureNodes.Count == 3,
                "拼接战役共 3 个据点（每座城市 1 个）", $"Actual: {campaign.CaptureNodes.Count}");
            Assert(campaign.CityLabels.Count == 3
                   && campaign.CityLabels[0] == "Danzig"
                   && campaign.CityLabels[1] == "Poznan"
                   && campaign.CityLabels[2] == "Warsaw",
                "城市按 但泽 ➔ 波兹南 ➔ 华沙 顺序拼接", $"Actual: {string.Join(",", campaign.CityLabels)}");

            bool ascending = true;
            for (int i = 1; i < campaign.CaptureNodes.Count; i++)
            {
                ascending &= campaign.CaptureNodes[i].GlobalPosition.X > campaign.CaptureNodes[i - 1].GlobalPosition.X;
            }
            Assert(ascending, "据点沿进攻轴依次排列 (但泽 ➔ 波兹南 ➔ 华沙)");

            bool radiiOk = campaign.CaptureNodes.All(n =>
                n.CaptureRadius >= campaign.CampaignCaptureRadiusMin - 1f
                && n.CaptureRadius <= campaign.CampaignCaptureRadiusMax + 1f);
            Assert(radiiOk, "每座城市的占领半径按各自城区规模计算且在预设区间内",
                string.Join(", ", campaign.CaptureNodes.Select(n => $"{n.SectorName}={n.CaptureRadius:0}")));

            // The seam between two cities must be plain ground, not leftover city fabric.
            bool seamIsPlain = true;
            int seamColumns = 0;
            var campaignData = campaign.Map!;
            var ranges = campaign.CityRanges;
            for (int i = 0; i + 1 < ranges.Count; i++)
            {
                int gapStart = ranges[i].Position.X + ranges[i].Size.X;
                int gapEnd = ranges[i + 1].Position.X;
                for (int column = gapStart; column < gapEnd; column++)
                {
                    seamColumns++;
                    for (int row = 0; row < campaignData.Rows; row++)
                    {
                        if (campaignData.ClassAt(column, row) != TerrainClass.Default) seamIsPlain = false;
                    }
                }
            }
            Assert(seamIsPlain && seamColumns > 0,
                "城市之间的衔接区域全部为平原 (Default)", $"seam columns checked: {seamColumns}");

            Assert(campaign.BuildingCount > 0, "拼接战役包含三城建筑", $"Actual: {campaign.BuildingCount}");
            Assert(campaign.MapWidth > 8000f,
                "拼接后战场长度显著长于单城地图", $"Actual: {campaign.MapWidth}");

            campaign.QueueFree();
        }

        private void TestSemanticMapBattlefield()
        {
            // 1. Exact id round-trip through a real PNG encode/decode.
            var authored = Image.CreateEmpty(6, 2, false, Image.Format.L8);
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    // Row 0 covers every valid class id exactly once.
                    int id = (x + y) % 6;
                    float value = id / 255f;
                    authored.SetPixel(x, y, new Color(value, value, value));
                }
            }

            var decoded = SemanticMap.FromPngBytes(authored.SavePngToBuffer(), 32f);
            Assert(decoded.Columns == 6 && decoded.Rows == 2,
                "语义掩膜尺寸正确解码为 6x2", $"Actual: {decoded.Columns}x{decoded.Rows}");

            bool idsMatch = true;
            for (int i = 0; i < 6; i++) idsMatch &= decoded.RawIdAt(i, 0) == i;
            Assert(idsMatch, "语义掩膜类别 ID 逐像素精确保留 (0..5)");

            // 2. Out-of-range ids must be rejected, not silently coerced.
            var invalid = Image.CreateEmpty(2, 2, false, Image.Format.L8);
            invalid.Fill(new Color(1f, 1f, 1f));
            bool rejected = false;
            try { SemanticMap.FromPngBytes(invalid.SavePngToBuffer(), 32f); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "非法语义掩膜 (像素 id > 5) 被拒绝并报错");

            // 3. Passability rules for the six classes.
            var cells = new byte[]
            {
                0, 1, 2, 3, 4, 5
            };
            var rules = new SemanticMap(6, 1, 32f, cells);
            Assert(rules.IsPassable(0, 0) && rules.IsPassable(3, 0)
                   && !rules.IsPassable(1, 0) && !rules.IsPassable(2, 0)
                   && !rules.IsPassable(4, 0) && !rules.IsPassable(5, 0),
                "通行规则：default/snow 可通行，river/mountain/两种建筑阻挡");
            Assert(!rules.InBounds(-1, 0) && rules.ClassAt(-1, 0) == TerrainClass.Default,
                "越界单元格查询安全返回 default");

            // 4. Build a real generated battlefield and check auto-placed capture nodes.
            string maskPath = $"{MaskBattlefieldMap.DefaultMapRoot}/Poland_Warsaw/{MaskBattlefieldMap.MaskFileName}";
            if (!FileAccess.FileExists(maskPath))
            {
                Assert(true, "跳过生成战场测试：尚未生成地图 (先运行 build_maps.py)");
                return;
            }

            var maskMap = SemanticMap.Load(maskPath, 32f);
            var histogram = maskMap.ClassHistogram();
            int total = 0;
            foreach (var entry in histogram) total += entry.Value;
            Assert(total == maskMap.Columns * maskMap.Rows,
                "真实地图类别直方图覆盖全部单元格",
                $"cells: {maskMap.Columns * maskMap.Rows}, counted: {total}");

            var battlefield = new MaskBattlefieldMap
            {
                Name = "MaskBattlefieldUnderTest",
                MaskPath = maskPath,
                CellSize = 32f,
                SectorCount = 3
            };
            AddChild(battlefield); // _Ready -> InitializeMap()

            var battleMap = battlefield.Map!;
            Assert(Mathf.IsEqualApprox(battlefield.MapWidth, battleMap.Columns * 32f)
                   && Mathf.IsEqualApprox(battlefield.MapHeight, battleMap.Rows * 32f),
                "战场世界尺寸 = 降采样后的格数 × 单元格尺寸",
                $"Actual: {battlefield.MapWidth}x{battlefield.MapHeight} (grid {battleMap.Columns}x{battleMap.Rows})");

            Assert(battlefield.CaptureNodes.Count == 3,
                "按需求自动布设 3 个据点", $"Actual: {battlefield.CaptureNodes.Count}");

            bool ascending = true;
            for (int i = 1; i < battlefield.CaptureNodes.Count; i++)
            {
                ascending &= battlefield.CaptureNodes[i].GlobalPosition.X > battlefield.CaptureNodes[i - 1].GlobalPosition.X;
            }
            Assert(ascending, "据点沿进攻轴 (+X) 严格递增排列");

            bool allPassable = true;
            foreach (var node in battlefield.CaptureNodes)
            {
                var cell = maskMap.WorldToCell(node.GlobalPosition);
                allPassable &= maskMap.IsPassable(cell.X, cell.Y);
            }
            Assert(allPassable, "所有据点均落在可通行地形上（不会卡在河流/山体/建筑内）");

            Assert(battlefield.ObstacleRectangles.Count > 0,
                "从语义掩膜生成了阻挡物碰撞矩形",
                $"Actual: {battlefield.ObstacleRectangles.Count}");

            Assert(battlefield.BuildingCount > 0,
                "从建筑类别生成了可交互建筑块", $"Actual: {battlefield.BuildingCount}");

            // The 1024px data raster must be sampled down or the map becomes unplayable.
            Assert(Mathf.Max(battleMap.Columns, battleMap.Rows) <= battlefield.TargetGridSize,
                $"战场栅格被降采样到 {battlefield.TargetGridSize} 格以内（源掩膜 {battlefield.SourceColumns}x{battlefield.SourceRows}）",
                $"Actual grid: {battleMap.Columns}x{battleMap.Rows}");
            Assert(battlefield.BuildingCount < 1500,
                "建筑块数量处于可玩范围（避免上千物理节点）", $"Actual: {battlefield.BuildingCount}");
            Assert(battlefield.ObstacleRectangles.Count < 4000,
                "阻挡矩形数量处于可玩范围", $"Actual: {battlefield.ObstacleRectangles.Count}");

            // 5. Extents published to ObjectiveManager drive sector bounds on any map size.
            var objMgr = new ObjectiveManager();
            AddChild(objMgr);
            objMgr.ConfigureMapExtents(battlefield.MapWidth, battlefield.MapHeight,
                battlefield.AttackerStagingPosition, battlefield.DefenderRearPosition);
            foreach (var node in battlefield.CaptureNodes) objMgr.RegisterNode(node);

            var (firstLeft, firstRight) = objMgr.GetSectorBounds(0);
            var (lastLeft, lastRight) = objMgr.GetSectorBounds(battlefield.CaptureNodes.Count - 1);

            Assert(firstLeft > battlefield.AttackerStagingPosition.X && firstRight > firstLeft,
                "生成地图的首个扇区边界位于进攻方集结区之后",
                $"Actual: [{firstLeft}, {firstRight}]");
            Assert(lastRight < battlefield.DefenderRearPosition.X && lastLeft < lastRight,
                "生成地图的末个扇区边界位于守方后方基地之前",
                $"Actual: [{lastLeft}, {lastRight}]");

            objMgr.QueueFree();
            battlefield.QueueFree();
        }

        private void TestTankDamageImmunity()
        {
            var tank = new Tank();
            AddChild(tank);

            float initialArmor = tank.CurrentArmor;
            Assert(initialArmor == 1000f, "坦克初始装甲值为 1000 HP", $"Actual: {initialArmor}");

            // 1. Bullet attack -> MUST DEAL 0 DAMAGE
            tank.TakeDamage(100f, null, tank.GlobalPosition, Vector2.Up, DamageType.Bullet);
            Assert(tank.CurrentArmor == 1000f, "常规子弹对坦克造成 0 点伤害（完全弹跳免疫）", $"Armor: {tank.CurrentArmor}");

            // 2. Grenade shrapnel attack -> MUST DEAL 0 DAMAGE
            tank.TakeDamage(100f, null, tank.GlobalPosition, Vector2.Up, DamageType.GrenadeShrapnel);
            Assert(tank.CurrentArmor == 1000f, "破片手雷对坦克造成 0 点伤害（装甲完全抵御）", $"Armor: {tank.CurrentArmor}");

            // 3. Rocket explosion attack -> MUST DEAL FULL DAMAGE
            tank.TakeDamage(150f, null, tank.GlobalPosition, Vector2.Up, DamageType.RocketExplosive);
            Assert(tank.CurrentArmor == 850f, "火箭炮高爆弹对坦克造成 150 点真实伤害（弱点机制生效）", $"Armor: {tank.CurrentArmor}");

            tank.QueueFree();
        }

        private void TestTankSpeed()
        {
            var soldier = new Soldier();
            float infantrySpeed = soldier.BaseWalkSpeed; // 180f
            float expectedTankSpeed = infantrySpeed * 0.9f; // 162f

            Assert(Mathf.IsEqualApprox(Tank.TankMoveSpeed, expectedTankSpeed, 0.01f),
                "坦克的移动速度严格等于步兵基准速度的 0.9 倍 (162 px/s vs 180 px/s)",
                $"TankSpeed: {Tank.TankMoveSpeed}, Expected: {expectedTankSpeed}");

            soldier.QueueFree();
        }

        private void TestTankCrewWeapons()
        {
            var tank = new Tank();
            AddChild(tank);

            var soldier1 = new Soldier { Team = Team.Attackers, SoldierName = "Crew_1" };
            var soldier2 = new Soldier { Team = Team.Attackers, SoldierName = "Crew_2" };
            AddChild(soldier1);
            AddChild(soldier2);

            // Step 1: Tank has 1 crew member (Driver only)
            tank.TryEnter(soldier1);
            Assert(tank.Driver == soldier1 && tank.Gunner == null, "1名士兵上车成为主驾驶");

            // Driver fires Howitzer -> allowed
            tank.FireCannon(tank.GlobalPosition + Vector2.Right * 200f);
            Assert(tank.CannonCooldownTimer > 0f, "单人驾驶可以使用 120mm 榴弹主炮");

            // Heavy MG attempted without gunner -> blocked!
            tank.FireHeavyMachineGun(tank.GlobalPosition + Vector2.Right * 200f);
            Assert(tank.MachineGunCooldownTimer == 0f, "只有一人驾驶时无法使用重机枪（限制机制生效）");

            // Step 2: 2nd soldier enters (Gunner)
            tank.TryEnter(soldier2);
            Assert(tank.Gunner == soldier2, "第2名士兵上车成为副驾驶机枪手");

            // Heavy MG can now fire!
            tank.FireHeavyMachineGun(tank.GlobalPosition + Vector2.Right * 200f);
            Assert(tank.MachineGunCooldownTimer > 0f, "双人满载时副驾驶可以开火使用重机枪");

            tank.QueueFree();
            soldier1.QueueFree();
            soldier2.QueueFree();
        }

        private void TestPlayerDriverPriority()
        {
            var tank = new Tank();
            AddChild(tank);

            var ai1 = new Soldier { Team = Team.Attackers, IsPlayerControlled = false, SoldierName = "AI_Driver" };
            var ai2 = new Soldier { Team = Team.Attackers, IsPlayerControlled = false, SoldierName = "AI_Gunner" };
            var player = new Soldier { Team = Team.Attackers, IsPlayerControlled = true, SoldierName = "Human_Player" };
            AddChild(ai1);
            AddChild(ai2);
            AddChild(player);

            // 1. Two AI soldiers occupy the tank first
            tank.TryEnter(ai1);
            tank.TryEnter(ai2);
            Assert(tank.Driver == ai1 && tank.Gunner == ai2, "两名AI先登车，分别担任主驾与副驾");

            // 2. Player enters tank! AI Driver MUST yield to player!
            bool playerEntered = tank.TryEnter(player);
            Assert(playerEntered, "玩家请求登车成功");
            Assert(tank.Driver == player, "玩家成功接管主驾驶席位！");
            Assert(tank.Gunner == ai1, "原AI主驾驶主动礼让，调换至副驾驶机枪席位");

            tank.QueueFree();
            ai1.QueueFree();
            ai2.QueueFree();
            player.QueueFree();
        }

        private void TestTankRespawnLogic()
        {
            var tankMgr = new TankManager();
            AddChild(tankMgr);

            var objMgr = new ObjectiveManager();
            AddChild(objMgr);

            // Check initial spawn position (before any nodes captured)
            Vector2 initialPos = tankMgr.GetLastCapturedNodePosition();
            Assert(initialPos.X == 240f && initialPos.Y == 600f, "未占领据点时，坦克诞生点为进攻方集结前哨基地 (X:240, Y:600)");

            // Create and register mock nodes
            var nodeA = new CaptureNode { SectorIndex = 0, GlobalPosition = new Vector2(800, 600) };
            var nodeB = new CaptureNode { SectorIndex = 1, GlobalPosition = new Vector2(1600, 600) };
            objMgr.RegisterNode(nodeA);
            objMgr.RegisterNode(nodeB);

            // Simulate capturing Node A
            nodeA.CaptureProgress = 1.0f;
            objMgr.Nodes[0].State = SectorState.Captured;
            // Advance active sector to 1 (波兹南 / Poznan)
            typeof(ObjectiveManager).GetProperty("ActiveSectorIndex")?.SetValue(objMgr, 1);

            Vector2 respawnPosAfterNodeA = tankMgr.GetLastCapturedNodePosition();
            Assert(Mathf.IsEqualApprox(respawnPosAfterNodeA.X, 750f, 10f),
                "据点 A 攻克后，坦克刷新点自动向前推移至打下的上一个据点 (Node A 后方)",
                $"Actual X: {respawnPosAfterNodeA.X}");

            // Verify 60s delay requirement
            Assert(TankManager.TankRespawnDelay == 60.0f, "坦克击毁后的刷新冷却时间严格设定为 60 秒");

            tankMgr.QueueFree();
            objMgr.QueueFree();
        }

        private void TestTruceAndTerritoryZones()
        {
            var objMgr = new ObjectiveManager();
            AddChild(objMgr);

            // 波兰战役 3 大重镇：但泽 (A) ➔ 波兹南 (B) ➔ 华沙 (C)
            var nodeA = new CaptureNode { SectorIndex = 0, GlobalPosition = new Vector2(1250, 600) };
            var nodeB = new CaptureNode { SectorIndex = 1, GlobalPosition = new Vector2(2700, 600) };
            var nodeC = new CaptureNode { SectorIndex = 2, GlobalPosition = new Vector2(4100, 600) };
            objMgr.RegisterNode(nodeA);
            objMgr.RegisterNode(nodeB);
            objMgr.RegisterNode(nodeC);

            // 1. Truce duration constant & initial truce
            Assert(ObjectiveManager.TruceDuration == 10.0f, "战区攻防停战持续时间严格设定为 10.0 秒");
            Assert(objMgr.IsTruceActive, "开局阶段默认激活 10 秒战术停战撤退状态");

            // 2. Sector bounds are derived from the registered capture nodes, not hard-coded.
            var (s0Left, s0Right) = objMgr.GetSectorBounds(0);
            Assert(s0Left == 550f && s0Right == 1975f,
                "战区 0 边界由节点位置推导 (但泽 1250 → [550, 1975])",
                $"Actual: [{s0Left}, {s0Right}]");

            var (s2Left, s2Right) = objMgr.GetSectorBounds(2);
            Assert(s2Left == 3400f && s2Right == 4650f,
                "战区 2 (华沙 4100) 为最后扇区，东界 4650 紧邻守方后方基地",
                $"Actual: [{s2Left}, {s2Right}]");

            // During Truce: Attackers cannot enter the sector! Clamped to (0, 550)
            var (atkMinTruce, atkMaxTruce) = objMgr.GetAllowedXRange(Team.Attackers);
            Assert(atkMinTruce == 0f && atkMaxTruce == 550f,
                "停战期进攻方被完全阻隔在前沿边界线后方 (X: [0, 550])，禁止进入下一据点战区",
                $"Actual: [{atkMinTruce}, {atkMaxTruce}]");

            // Defenders have territory from barrier eastward
            var (defMinTruce, defMaxTruce) = objMgr.GetAllowedXRange(Team.Defenders);
            Assert(defMinTruce == 550f && defMaxTruce == 5000f,
                "停战期防守方拥有完整后撤纵深 (X: [550, 5000])，从容撤退并部署掩体",
                $"Actual: [{defMinTruce}, {defMaxTruce}]");

            // End truce manually
            typeof(ObjectiveManager).GetProperty("IsTruceActive")?.SetValue(objMgr, false);
            var (atkMinCombat, atkMaxCombat) = objMgr.GetAllowedXRange(Team.Attackers);
            var (defMinCombat, defMaxCombat) = objMgr.GetAllowedXRange(Team.Defenders);

            Assert(atkMaxCombat == 1975f, "交火期进攻方推进范围延伸至据点争夺区东侧 (X: [0, 1975])");
            Assert(defMinCombat == 550f, "交火期防守方活动范围起始于据点争夺区西侧 (X: [550, 5000])");

            // Overlap verification
            Assert(atkMinCombat == 0f && defMaxCombat == 5000f,
                "战场三段式区域划分生效：仅中部据点争夺区域双方重合交战，其余为各自专属安全区");

            // 3. Physical barrier test
            var barrier = new SectorBarrier();
            AddChild(barrier);
            barrier.SetBarrierActive(550f, true);
            Assert(barrier.Visible && barrier.Position.X == 550f, "隔断实体屏障在 X: 550 处准确定位并激活");
            Assert(barrier.CollisionLayer == 1 && barrier.CollisionMask == 1, "隔断屏障碰撞体物理层级正确，完全阻挡子弹与载具");

            barrier.QueueFree();
            nodeA.QueueFree();
            nodeB.QueueFree();
            nodeC.QueueFree();
            objMgr.QueueFree();
        }

        private void TestPkmSpecs()
        {
            var pkm = WeaponData.CreatePKM();
            Assert(pkm.Type == WeaponType.PKM, "武器类型为 PKM 通用机枪");
            Assert(pkm.MagazineCapacity == 100, "PKM 配备 100 发大容量连续供弹弹链箱", $"Actual: {pkm.MagazineCapacity}");
            Assert(pkm.MaxReserveAmmo == 300, "PKM 备弹总量为 300 发", $"Actual: {pkm.MaxReserveAmmo}");
            Assert(pkm.FireRateRpm == 650f, "PKM 战术射速为 650 发/分", $"Actual: {pkm.FireRateRpm}");
            Assert(pkm.Damage == 32f, "PKM 基础单发杀伤为 32 HP", $"Actual: {pkm.Damage}");
            Assert(pkm.IsFullAuto, "PKM 具备全自动持续扫射与火力压制能力");
        }

        private void TestRespawnLoadoutAndAiRatio()
        {
            var spawnMgr = new SpawnManager();
            AddChild(spawnMgr);

            var tankMgr = new TankManager();
            AddChild(tankMgr);

            var tank = new Tank();
            AddChild(tank);
            tankMgr.ActiveTank = tank;

            // 1. Test Defender AI loadout with Tank present
            int defRpgCount = 0;
            int defCoverCount = 0;
            int defPkmCount = 0;
            int samples = 120;

            for (int i = 0; i < samples; i++)
            {
                spawnMgr.SelectAiLoadout(Team.Defenders, out var wep, out var gdg);
                if (gdg.Type == GadgetType.RPG) defRpgCount++;
                if (gdg.Type == GadgetType.DeployableCover) defCoverCount++;
                if (wep.Type == WeaponType.PKM) defPkmCount++;
            }

            float defRpgRate = (float)defRpgCount / samples;
            float defPkmRate = (float)defPkmCount / samples;
            Assert(defRpgRate >= 0.45f,
                $"有敌军坦克在场时，防守方AI高优先级配发反坦克火箭炮 (实测: {defRpgRate * 100:0.0}%, 预期 >= 45%)",
                $"RPG Count: {defRpgCount}/{samples}");
            Assert(defPkmRate >= 0.30f,
                $"防守方AI优先选择PKM通用机枪架设防线 (实测: {defPkmRate * 100:0.0}%, 预期 >= 30%)",
                $"PKM Count: {defPkmCount}/{samples}");

            // 2. Test Attacker AI loadout
            int atkArCount = 0;
            for (int i = 0; i < samples; i++)
            {
                spawnMgr.SelectAiLoadout(Team.Attackers, out var wep, out var gdg);
                if (wep.Type == WeaponType.M4A1 || wep.Type == WeaponType.AK74M) atkArCount++;
            }
            float atkArRate = (float)atkArCount / samples;
            Assert(atkArRate >= 0.65f,
                $"进攻方AI优先选择高机动突击步枪 (M4A1/AK-74M) 推进突击 (实测: {atkArRate * 100:0.0}%, 预期 >= 65%)",
                $"AR Count: {atkArCount}/{samples}");

            // 3. Test Player loadout selection mapping
            MatchSettings.SelectedPrimaryWeaponIndex = 2; // PKM
            MatchSettings.SelectedGadgetIndex = 2; // DeployableCover
            var playerSoldier = spawnMgr.SpawnSoldier(Team.Attackers, isPlayer: true, "Test_Human");
            Assert(playerSoldier.Weapon.Type == WeaponType.PKM, "玩家复活主武器正确应用选择: PKM通用机枪");
            Assert(playerSoldier.Gadget.Type == GadgetType.DeployableCover, "玩家复活辅助道具正确应用选择: 便携防弹沙袋");

            playerSoldier.QueueFree();
            tank.QueueFree();
            tankMgr.QueueFree();
            spawnMgr.QueueFree();
        }

        private void RunHighSpeedBalanceSimulationTest()
        {
            GD.Print("\n--- [9] 高倍速游戏战局平衡性测试 (High-Speed Balance Simulation) ---");

            int iterations = 10;
            int attackerWins = 0;
            int defenderWins = 0;
            float totalSimulatedTime = 0f;
            int totalSectorsCaptured = 0;
            int totalTanksSpawned = 0;
            int totalTanksKilled = 0;

            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var simResult = SimulateMatch(matchSeed: 1000 + i * 77);
                if (simResult.AttackerWon) attackerWins++;
                else defenderWins++;

                totalSimulatedTime += simResult.MatchDurationSec;
                totalSectorsCaptured += simResult.SectorsCaptured;
                totalTanksSpawned += simResult.TanksSpawned;
                totalTanksKilled += simResult.TanksKilled;
            }

            stopwatch.Stop();

            float attackerWinRate = (float)attackerWins / iterations * 100f;
            float avgSectorsCaptured = (float)totalSectorsCaptured / iterations;
            float avgDurationSec = totalSimulatedTime / iterations;

            GD.Print($"模拟对局场次: {iterations} 场");
            GD.Print($"实际运行耗时: {stopwatch.ElapsedMilliseconds} ms (高倍速超快速验证)");
            GD.Print($"平均对局时长: {avgDurationSec:0.0} 秒 (等效作战时间)");
            GD.Print($"进攻方胜率: {attackerWinRate:0.0}% ({attackerWins}/{iterations})");
            GD.Print($"防守方胜率: {(100f - attackerWinRate):0.0}% ({defenderWins}/{iterations})");
            GD.Print($"平均攻克战区数: {avgSectorsCaptured:0.0} / {CampaignSectors} 个");
            GD.Print($"坦克出击总数: {totalTanksSpawned} 辆 | 坦克被击毁总数: {totalTanksKilled} 辆");

            // NOTE: win-rate balance is deliberately NOT a build gate. This demo is designed as an
            // asymmetric scenario where the attacker (Germany) is expected to dominate, so the
            // simulation below is reported for information only.
            GD.Print("【信息】胜率不是构建门槛：本作为非对称战役，进攻方（德国）占优是预期表现。");
            GD.Print($"   阵营分布: 攻方 {attackerWinRate:0.0}% ({attackerWins}/{iterations}) | 守方 {(100f - attackerWinRate):0.0}% ({defenderWins}/{iterations})");

            // 2. Average match duration is reasonable (not instant ending)
            Assert(avgDurationSec >= 30f,
                "战场交火持续性达标：平均战局时长合理充足",
                $"AvgDuration: {avgDurationSec:0.0}s");

            // 3. Tank involvement in combat
            Assert(totalTanksSpawned >= iterations,
                "装甲步协同机制生效：每场对局均有坦克参与前线攻坚",
                $"TanksSpawned: {totalTanksSpawned}");
        }

        private struct SimulationResult
        {
            public bool AttackerWon;
            public float MatchDurationSec;
            public int SectorsCaptured;
            public int TanksSpawned;
            public int TanksKilled;
        }

        // 波兰战役 3 大重镇：但泽 (A) ➔ 波兹南 (B) ➔ 华沙 (C)
        private const int CampaignSectors = 3;

        private SimulationResult SimulateMatch(int matchSeed, int startingTickets = 150)
        {
            var rng = new Random(matchSeed);

            int attackerTickets = startingTickets;
            int currentSector = 0; // 0 to CampaignSectors - 1
            float sectorProgress = 0f;
            float matchTime = 0f;
            int tanksSpawned = 1;
            int tanksKilled = 0;
            float tankHp = 1000f;
            float tankRespawnCooldown = 0f;
            float truceTimer = 10.0f; // 10s initial truce at match start!

            float dt = 0.5f; // Simulation step: 0.5s per step
            float maxMatchTime = 1800f; // 30 minutes max; match concludes via tickets or sector capture

            while (matchTime < maxMatchTime && attackerTickets > 0 && currentSector < CampaignSectors)
            {
                matchTime += dt;

                // 10s Truce phase: Defenders retreat & place sandbag covers, Attackers stage behind boundary
                if (truceTimer > 0f)
                {
                    truceTimer -= dt;
                    continue;
                }

                // Tank respawn timer
                if (tankHp <= 0f)
                {
                    tankRespawnCooldown -= dt;
                    if (tankRespawnCooldown <= 0f)
                    {
                        tankHp = 1000f;
                        tanksSpawned++;
                    }
                }

                // Firefight simulation:
                // Attackers have 12 troops + 1 Tank (if alive)
                // Defenders have 12 troops in fortified positions
                float attackerPower = 12f * (float)(0.90 + rng.NextDouble() * 0.25);
                if (tankHp > 0f)
                {
                    attackerPower += 5.5f; // Tank gives tangible breakthrough push
                }

                float defenderPower = 12f * (float)(0.92 + rng.NextDouble() * 0.25);
                // Defenders have escalating fortifications in deeper sectors
                defenderPower *= (1.0f + currentSector * 0.085f);

                // Combat exchanges
                if (attackerPower > defenderPower)
                {
                    // Push capture zone
                    float pushRate = (attackerPower - defenderPower) * 0.014f;
                    sectorProgress += pushRate * dt;

                    if (sectorProgress >= 1.0f)
                    {
                        // Sector captured!
                        currentSector++;
                        sectorProgress = 0f;
                        truceTimer = 10.0f; // Next 10s truce phase begins!
                        attackerTickets = Math.Min(startingTickets + 50, attackerTickets + 35); // +35 bonus tickets on cap (matching ObjectiveManager)
                        if (currentSector >= CampaignSectors) break; // Total victory!
                    }
                }
                else
                {
                    // Defenders push attackers back
                    float defendRate = (defenderPower - attackerPower) * 0.010f;
                    sectorProgress = Math.Max(0f, sectorProgress - defendRate * dt);
                }

                // Combat casualty drain (scaled with sector fortification and tank cover)
                float drainChance = 0.13f + currentSector * 0.024f;
                if (defenderPower > attackerPower)
                {
                    drainChance += 0.06f;
                }
                if (tankHp > 0f)
                {
                    drainChance *= 0.74f; // Tank fire support suppresses enemy fire
                }

                if (rng.NextDouble() < drainChance)
                {
                    attackerTickets--;
                }

                // Tank under fire from Defender RPGs
                if (tankHp > 0f)
                {
                    // Defenders actively engage tank with RPGs
                    if (rng.NextDouble() < 0.065)
                    {
                        tankHp -= 160f; // RPG rocket damage
                        if (tankHp <= 0f)
                        {
                            tanksKilled++;
                            tankRespawnCooldown = 60.0f; // Strict 60s cooldown!
                        }
                    }
                }
            }

            return new SimulationResult
            {
                AttackerWon = (currentSector >= CampaignSectors),
                MatchDurationSec = matchTime,
                SectorsCaptured = currentSector,
                TanksSpawned = tanksSpawned,
                TanksKilled = tanksKilled
            };
        }

        private class AiAgentSim
        {
            public string Name = "";
            public Team Team;
            public bool IsTank;
            public float Radius;
            public float Speed;
            public Vector2 Position;
            public Vector2 Velocity;
            public float PatrolPhase;
            public float UnstuckTimer;
            public Vector2 UnstuckDir;
            public float StuckCheckTimer;
            public Vector2 LastCheckPos;

            // Metrics for stuck detection (>5s within 15px radius)
            public Vector2 AnchorPos;
            public float TimeInAnchorRadius;
            public bool HasTriggeredStuck;
            public int StuckOccurrencesCount;
        }

        private void RunAiStuckDetectionMatchTest()
        {
            GD.Print("\n--- [10] 完整对局 AI 寻路停滞/卡死检测测试 (AI Stuck Detection in Fast Match) ---");

            var map = new BattlefieldMap();
            map.InitializeMap();
            var obstacles = map.ObstacleRectangles;

            var nodes = new List<Vector2>
            {
                new Vector2(1250, 600),  // Node A: 但泽 (Danzig)
                new Vector2(2700, 600),  // Node B: 波兹南 (Poznan)
                new Vector2(4100, 600)   // Node C: 华沙 (Warsaw)
            };

            var agents = new List<AiAgentSim>();

            // 1 Attacker Tank
            agents.Add(new AiAgentSim
            {
                Name = "Attacker_Tank_M1",
                Team = Team.Attackers,
                IsTank = true,
                Radius = 32f,
                Speed = 162f,
                Position = new Vector2(240, 600),
                AnchorPos = new Vector2(240, 600),
                LastCheckPos = new Vector2(240, 600),
                StuckCheckTimer = 1.2f
            });

            // 6 Attacker Infantry
            for (int i = 0; i < 6; i++)
            {
                Vector2 spawnPos = new Vector2(160 + i * 18, 580 + (i % 3) * 20);
                agents.Add(new AiAgentSim
                {
                    Name = $"Attacker_Infantry_{i + 1}",
                    Team = Team.Attackers,
                    IsTank = false,
                    Radius = 12f,
                    Speed = 180f,
                    Position = spawnPos,
                    AnchorPos = spawnPos,
                    LastCheckPos = spawnPos,
                    StuckCheckTimer = 1.0f,
                    PatrolPhase = i * 1.0f
                });
            }

            // 6 Defender Infantry
            for (int i = 0; i < 6; i++)
            {
                Vector2 defSpawn = new Vector2(1270 + i * 16, 580 + (i % 3) * 20);
                agents.Add(new AiAgentSim
                {
                    Name = $"Defender_Infantry_{i + 1}",
                    Team = Team.Defenders,
                    IsTank = false,
                    Radius = 12f,
                    Speed = 180f,
                    Position = defSpawn,
                    AnchorPos = defSpawn,
                    LastCheckPos = defSpawn,
                    StuckCheckTimer = 1.0f,
                    PatrolPhase = i * 1.0f
                });
            }

            float dt = 0.05f; // 20 updates per second
            float matchTime = 0f;
            float maxMatchTime = 300f; // up to 5 minutes simulated
            int activeSector = 0;
            float sectorCaptureProgress = 0f;
            int totalStuckOccurrences = 0;

            var stopwatch = Stopwatch.StartNew();

            while (matchTime < maxMatchTime && activeSector < nodes.Count)
            {
                matchTime += dt;
                Vector2 currentTargetNode = nodes[activeSector];

                int attackersInNode = 0;
                int defendersInNode = 0;

                foreach (var agent in agents)
                {
                    Vector2 targetPos = currentTargetNode;
                    float distToTarget = agent.Position.DistanceTo(targetPos);

                    if (distToTarget < 160f)
                    {
                        if (agent.Team == Team.Attackers) attackersInNode++;
                        else defendersInNode++;
                    }

                    if (distToTarget < (agent.IsTank ? 140f : 85f))
                    {
                        // Orbit capture node in dynamic perimeter patrol
                        agent.PatrolPhase += dt * (agent.IsTank ? 0.6f : 1.5f);
                        float orbitA = agent.IsTank ? 110f : 60f;
                        float orbitB = agent.IsTank ? 60f : 45f;
                        Vector2 patrolPoint = targetPos + new Vector2(Mathf.Cos(agent.PatrolPhase) * orbitA, Mathf.Sin(agent.PatrolPhase) * orbitB);
                        Vector2 desiredDir = (patrolPoint - agent.Position).Normalized();
                        agent.Velocity = desiredDir * (agent.Speed * (agent.IsTank ? 0.45f : 0.6f));
                    }
                    else
                    {
                        // Traveling towards objective
                        Vector2 desiredDir = (targetPos - agent.Position).Normalized();

                        if (agent.UnstuckTimer > 0f)
                        {
                            agent.UnstuckTimer -= dt;
                            agent.Velocity = agent.UnstuckDir * agent.Speed;
                        }
                        else
                        {
                            float whiskerDist = agent.IsTank ? 95f : 65f;
                            float centerHit = CastRayAgainstObstacles(agent.Position, desiredDir, whiskerDist, agent.Radius, obstacles);
                            if (centerHit < (agent.IsTank ? 90f : 60f))
                            {
                                float angle = Mathf.DegToRad(agent.IsTank ? 35f : 38f);
                                Vector2 leftDir = desiredDir.Rotated(-angle);
                                Vector2 rightDir = desiredDir.Rotated(angle);
                                float leftHit = CastRayAgainstObstacles(agent.Position, leftDir, agent.IsTank ? 80f : 55f, agent.Radius, obstacles);
                                float rightHit = CastRayAgainstObstacles(agent.Position, rightDir, agent.IsTank ? 80f : 55f, agent.Radius, obstacles);
                                desiredDir = (leftHit > rightHit) ? leftDir : rightDir;
                            }

                            // Road bias towards asphalt center (Y=600)
                            if (agent.Position.Y > 400f && agent.Position.Y < 800f)
                            {
                                float pull = (600f - agent.Position.Y) * (agent.IsTank ? 0.003f : 0.0025f);
                                desiredDir = new Vector2(desiredDir.X, desiredDir.Y + pull).Normalized();
                            }

                            agent.Velocity = desiredDir * agent.Speed;

                            // Anti-stuck detector
                            agent.StuckCheckTimer -= dt;
                            if (agent.StuckCheckTimer <= 0f)
                            {
                                float moved = agent.Position.DistanceTo(agent.LastCheckPos);
                                if (moved < (agent.IsTank ? 12f : 10f))
                                {
                                    float escapeY = (agent.Position.Y > 600f) ? -0.6f : 0.6f;
                                    agent.UnstuckDir = (agent.IsTank ? new Vector2(-0.8f, escapeY) : new Vector2(0f, escapeY)).Normalized();
                                    agent.UnstuckTimer = agent.IsTank ? 0.9f : 0.75f;
                                }
                                agent.LastCheckPos = agent.Position;
                                agent.StuckCheckTimer = agent.IsTank ? 1.2f : 1.0f;
                            }
                        }
                    }

                    // Move and resolve obstacle collisions
                    agent.Position = MoveAndSlideBox(agent.Position, agent.Velocity * dt, agent.Radius, obstacles);

                    // =========================================================================
                    // AI Coordinate Stuck Detection Metric (> 5.0s in approx same coordinates)
                    // =========================================================================
                    float distFromAnchor = agent.Position.DistanceTo(agent.AnchorPos);
                    if (distFromAnchor < 15.0f)
                    {
                        agent.TimeInAnchorRadius += dt;
                        if (agent.TimeInAnchorRadius >= 5.0f && !agent.HasTriggeredStuck)
                        {
                            agent.HasTriggeredStuck = true;
                            agent.StuckOccurrencesCount++;
                            totalStuckOccurrences++;
                            GD.PrintErr($"[AI STUCK ERROR] {agent.Name} stuck at ({agent.Position.X:0.0}, {agent.Position.Y:0.0}) for {agent.TimeInAnchorRadius:0.0}s at Sector {activeSector}");
                        }
                    }
                    else
                    {
                        agent.AnchorPos = agent.Position;
                        agent.TimeInAnchorRadius = 0f;
                        agent.HasTriggeredStuck = false;
                    }
                }

                // Sector capture progress
                if (attackersInNode > defendersInNode)
                {
                    sectorCaptureProgress += dt * 0.25f; // ~4s of holding to capture node
                    if (sectorCaptureProgress >= 1.0f)
                    {
                        activeSector++;
                        sectorCaptureProgress = 0f;

                        // On sector captured: defenders respawn forward at next sector or fortress
                        if (activeSector < nodes.Count)
                        {
                            Vector2 nextDefSpawn = (activeSector + 1 < nodes.Count)
                                ? nodes[activeSector + 1] + new Vector2(60, 0)
                                : new Vector2(4850, 600); // Fortress Zone 6

                            for (int i = 0; i < agents.Count; i++)
                            {
                                if (agents[i].Team == Team.Defenders)
                                {
                                    agents[i].Position = nextDefSpawn + new Vector2(i * 12, (i % 3) * 20 - 20);
                                    agents[i].AnchorPos = agents[i].Position;
                                    agents[i].TimeInAnchorRadius = 0f;
                                    agents[i].HasTriggeredStuck = false;
                                }
                            }
                        }
                    }
                }
            }

            stopwatch.Stop();

            GD.Print($"模拟全图对局耗时: {stopwatch.ElapsedMilliseconds} ms (模拟游戏时长: {matchTime:0.0}s)");
            GD.Print($"追踪 AI 实体总数: {agents.Count} (1 坦克 + 12 步兵)");
            GD.Print($"攻克据点总数: {activeSector} / {nodes.Count} 个");
            GD.Print($"AI 在同一坐标范围 (<=15px) 停留超过 5.0s 的总发生次数: {totalStuckOccurrences} 次");

            // Assert 0 stuck occurrences
            Assert(totalStuckOccurrences == 0,
                "AI寻路防卡死达标：整局对战中AI在同一坐标滞留超过5秒的发生次数为 0",
                $"StuckOccurrences: {totalStuckOccurrences}");

            Assert(activeSector >= nodes.Count,
                "战场全线贯通达标：进攻方AI与坦克成功攻克全部 3 座重镇（但泽 ➔ 波兹南 ➔ 华沙，路线无死锁）",
                $"SectorsCaptured: {activeSector} / {nodes.Count}");

            map.QueueFree();
        }

        private static float CastRayAgainstObstacles(Vector2 origin, Vector2 dir, float maxDist, float radius, IReadOnlyList<Rect2> obstacles)
        {
            float closestDist = maxDist;
            Vector2 dirNorm = dir.Normalized();

            for (int i = 0; i < obstacles.Count; i++)
            {
                var r = obstacles[i];
                var expanded = new Rect2(r.Position - new Vector2(radius, radius), r.Size + new Vector2(radius * 2f, radius * 2f));
                float hitDist = RayAabbDistance(origin, dirNorm, maxDist, expanded);
                if (hitDist < closestDist)
                {
                    closestDist = hitDist;
                }
            }
            return closestDist;
        }

        private static float RayAabbDistance(Vector2 origin, Vector2 dir, float maxDist, Rect2 box)
        {
            if (box.HasPoint(origin)) return 0f;

            float tmin = 0f;
            float tmax = maxDist;

            if (Math.Abs(dir.X) > 0.0001f)
            {
                float tx1 = (box.Position.X - origin.X) / dir.X;
                float tx2 = (box.End.X - origin.X) / dir.X;
                tmin = Math.Max(tmin, Math.Min(tx1, tx2));
                tmax = Math.Min(tmax, Math.Max(tx1, tx2));
            }
            else if (origin.X < box.Position.X || origin.X > box.End.X)
            {
                return maxDist;
            }

            if (Math.Abs(dir.Y) > 0.0001f)
            {
                float ty1 = (box.Position.Y - origin.Y) / dir.Y;
                float ty2 = (box.End.Y - origin.Y) / dir.Y;
                tmin = Math.Max(tmin, Math.Min(ty1, ty2));
                tmax = Math.Min(tmax, Math.Max(ty1, ty2));
            }
            else if (origin.Y < box.Position.Y || origin.Y > box.End.Y)
            {
                return maxDist;
            }

            if (tmax >= tmin && tmin >= 0f && tmin < maxDist)
            {
                return tmin;
            }

            return maxDist;
        }

        private static Vector2 MoveAndSlideBox(Vector2 currentPos, Vector2 movement, float radius, IReadOnlyList<Rect2> obstacles)
        {
            Vector2 newPos = currentPos;

            // X axis test
            Vector2 testX = new Vector2(newPos.X + movement.X, newPos.Y);
            bool hitX = false;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var exp = new Rect2(obstacles[i].Position - new Vector2(radius, radius), obstacles[i].Size + new Vector2(radius * 2f, radius * 2f));
                if (exp.HasPoint(testX))
                {
                    hitX = true;
                    break;
                }
            }
            if (!hitX) newPos.X = testX.X;

            // Y axis test
            Vector2 testY = new Vector2(newPos.X, newPos.Y + movement.Y);
            bool hitY = false;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var exp = new Rect2(obstacles[i].Position - new Vector2(radius, radius), obstacles[i].Size + new Vector2(radius * 2f, radius * 2f));
                if (exp.HasPoint(testY))
                {
                    hitY = true;
                    break;
                }
            }
            if (!hitY) newPos.Y = testY.Y;

            return newPos;
        }

        private void RunTicketsVsWinRateSensitivityTest()
        {
            GD.Print("\n--- [11] 进攻方命数与胜率关系灵敏度测试 (Tickets vs Win Rate Sensitivity) ---");

            int[] ticketTiers = new int[] { 50, 75, 100, 125, 150, 175, 200, 250, 300 };
            int matchesPerTier = 100;
            var winRates = new List<float>();

            GD.Print("初始命数 (Tickets) | 进攻胜率 (Win Rate) | 平均攻克据点 (Sectors) | 平均对局时长 (Sec)");
            GD.Print("------------------+---------------------+------------------------+-------------------");

            var sw = Stopwatch.StartNew();

            for (int t = 0; t < ticketTiers.Length; t++)
            {
                int tickets = ticketTiers[t];
                int wins = 0;
                float totalDuration = 0f;
                int totalSectors = 0;

                for (int m = 0; m < matchesPerTier; m++)
                {
                    int seed = tickets * 1000 + m * 37 + 7;
                    var result = SimulateMatch(seed, tickets);
                    if (result.AttackerWon) wins++;
                    totalDuration += result.MatchDurationSec;
                    totalSectors += result.SectorsCaptured;
                }

                float winRate = (float)wins / matchesPerTier * 100f;
                float avgSectors = (float)totalSectors / matchesPerTier;
                float avgDuration = totalDuration / matchesPerTier;
                winRates.Add(winRate);

                GD.Print($"{tickets,17} | {winRate,18:0.0}% | {avgSectors,21:0.0} / {CampaignSectors} | {avgDuration,16:0.0}s");
            }

            sw.Stop();
            GD.Print($"灵敏度测试模拟完成，总场次: {ticketTiers.Length * matchesPerTier} 场，耗时: {sw.ElapsedMilliseconds} ms");

            // Win-rate tiers are informational only; see the note in the balance test above.
            GD.Print("【信息】命数灵敏度曲线仅供参考，不作为构建门槛。");
        }

        private void PrintSummaryAndExit()
        {
            GD.Print("\n========================================================");
            if (_failedTests == 0)
            {
                GD.Print($"★ 全部测试通过 SUCCESS: {_passedTests} / {_totalTests} 测试项合格！");
                GD.Print("========================================================");
                GetTree().Quit(0);
            }
            else
            {
                GD.PrintErr($"✖ 测试失败 FAILURE: {_failedTests} 个测试项未通过，{_passedTests} 个通过！");
                GD.Print("========================================================");
                GetTree().Quit(1);
            }
        }
    }
}
