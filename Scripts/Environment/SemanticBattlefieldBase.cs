using System;
using System.Collections.Generic;
using Godot;
using KingWar.Objectives;

namespace KingWar.Environment
{
    /// <summary>
    /// Shared machinery for battlefields derived from one or more pipeline
    /// <c>semantic_mask.png</c> rasters: ground texture, terrain collision, destructible
    /// buildings, terrain-based movement rules and capture-node helpers.
    /// </summary>
    public abstract partial class SemanticBattlefieldBase : BattlefieldBase
    {
        public static readonly Color[] Palette =
        {
            new(92 / 255f, 112 / 255f, 74 / 255f),   // 0 default
            new(42 / 255f, 122 / 255f, 190 / 255f),  // 1 river
            new(111 / 255f, 91 / 255f, 70 / 255f),   // 2 mountain
            new(232 / 255f, 239 / 255f, 245 / 255f), // 3 snow
            new(188 / 255f, 142 / 255f, 91 / 255f),  // 4 buildings (destructible)
            new(102 / 255f, 74 / 255f, 58 / 255f)    // 5 buildings (indestructible)
        };

        [Export] public float CellSize { get; set; } = 32f;

        /// <summary>
        /// Masks are ~1024px data rasters. Sampling them down keeps collision shapes, building
        /// nodes and physics work at a sane count; without it a map becomes unplayable.
        /// </summary>
        [Export] public int TargetGridSize { get; set; } = 160;

        [Export] public float BuildingHealth { get; set; } = 250f;
        [Export] public bool DrawBuildings { get; set; } = true;

        /// <summary>
        /// Largest building block, in cells. The greedy merge would otherwise fuse a whole row of
        /// city fabric into a single kilometre-long strip, so one burst would delete an entire wall.
        /// </summary>
        [Export] public int MaxBuildingCellsPerBlock { get; set; } = 6;

        protected SemanticMap? MapData;

        private readonly List<CaptureNode> _nodes = new();
        private readonly List<Rect2> _obstacles = new();
        private readonly List<SemanticBuilding> _buildings = new();
        private ImageTexture? _groundTexture;
        private bool _initialized;
        private Vector2 _attackerStaging;
        private Vector2 _defenderRear;

        public override float MapWidth => MapData?.WorldWidth ?? 0f;
        public override float MapHeight => MapData?.WorldHeight ?? 0f;
        public override IReadOnlyList<CaptureNode> CaptureNodes => _nodes;
        public override IReadOnlyList<Rect2> ObstacleRectangles => _obstacles;
        public override Vector2 AttackerStagingPosition => _attackerStaging;
        public override Vector2 DefenderRearPosition => _defenderRear;

        public SemanticMap? Map => MapData;
        public int BuildingCount => _buildings.Count;
        public int SourceColumns { get; protected set; }
        public int SourceRows { get; protected set; }

        /// <summary>Builds the full terrain raster for this battlefield.</summary>
        protected abstract SemanticMap BuildSemanticMap();

        /// <summary>Drops capture nodes onto the finished raster.</summary>
        protected abstract void PlaceCaptureNodes();

        public override void InitializeMap()
        {
            if (_initialized) return;
            _initialized = true;

            MapData = BuildSemanticMap();

            BuildGroundTexture();
            BuildTerrainCollision();
            BuildBuildings();
            PlaceCaptureNodes();

            GD.Print($"   [地形] 建筑块 {_buildings.Count} | 阻挡矩形 {_obstacles.Count} | " +
                     $"据点 {_nodes.Count} | 世界 {MapWidth:0}x{MapHeight:0}");
            QueueRedraw();
        }

        // ---------------------------------------------------------------- terrain queries

        public override Texture2D? GroundTexture => _groundTexture;

        /// <summary>
        /// Looks for walkable ground in a narrow window around the requested position, preferring
        /// to stay close to the requested column and the requested row.
        /// </summary>
        public override Vector2 FindSpawnPoint(float x, float preferredY)
        {
            if (MapData == null) return new Vector2(x, preferredY);

            const int columnWindow = 8;
            int centreColumn = Mathf.Clamp(Mathf.FloorToInt(x / CellSize), 0, MapData.Columns - 1);
            int preferredRow = Mathf.Clamp(Mathf.FloorToInt(preferredY / CellSize), 0, MapData.Rows - 1);

            int firstColumn = Mathf.Max(0, centreColumn - columnWindow);
            int lastColumn = Mathf.Min(MapData.Columns - 1, centreColumn + columnWindow);

            int bestColumn = -1;
            int bestRow = -1;
            long bestScore = long.MaxValue;

            for (int column = firstColumn; column <= lastColumn; column++)
            {
                for (int row = 0; row < MapData.Rows; row++)
                {
                    if (!MapData.IsPassable(column, row)) continue;

                    // Weight the column distance higher so units stay on the intended line.
                    long score = Math.Abs(column - centreColumn) * 4L + Math.Abs(row - preferredRow);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestColumn = column;
                        bestRow = row;
                    }
                }
            }

            if (bestColumn < 0) return new Vector2(x, preferredY);
            return MapData.CellCenterToWorld(bestColumn, bestRow);
        }

        public override TerrainClass GetTerrainAt(Vector2 worldPosition)
        {
            if (MapData == null) return TerrainClass.Default;
            var cell = MapData.WorldToCell(worldPosition);
            return MapData.ClassAt(cell.X, cell.Y);
        }

        public override float GetSpeedMultiplier(Vector2 worldPosition, bool isVehicle)
        {
            return SpeedMultiplierFor(GetTerrainAt(worldPosition));
        }

        public override bool BlocksVehicles(Vector2 worldPosition) =>
            TerrainBlocksVehicles(GetTerrainAt(worldPosition));

        // ---------------------------------------------------------------- visuals

        private void BuildGroundTexture()
        {
            var map = MapData!;
            var image = Image.CreateEmpty(map.Columns, map.Rows, false, Image.Format.Rgb8);
            for (int row = 0; row < map.Rows; row++)
            {
                for (int column = 0; column < map.Columns; column++)
                {
                    image.SetPixel(column, row, Palette[map.RawIdAt(column, row)]);
                }
            }
            _groundTexture = ImageTexture.CreateFromImage(image);
        }

        public override void _Draw()
        {
            if (MapData == null || _groundTexture == null) return;

            var rect = new Rect2(0f, 0f, MapData.WorldWidth, MapData.WorldHeight);
            DrawTextureRect(_groundTexture, rect, false);
            DrawRect(rect, new Color(0.05f, 0.05f, 0.05f, 0.9f), false, 3f);
        }

        // ---------------------------------------------------------------- collision

        private void BuildTerrainCollision()
        {
            var map = MapData!;
            var body = new StaticBody2D { Name = "TerrainCollision" };

            // Mountains block vehicles only: dedicated layer + tank collision mask.
            foreach (var run in MergeRects((c, r) => map.ClassAt(c, r) == TerrainClass.Mountain, map.Columns, map.Rows))
            {
                var world = CellRectToWorld(run);
                body.AddChild(new CollisionShape2D
                {
                    Position = world.Position + world.Size * 0.5f,
                    Shape = new RectangleShape2D { Size = world.Size }
                });
                _obstacles.Add(world);
            }

            if (body.GetChildCount() > 0)
            {
                body.CollisionLayer = MountainCollisionLayer;
                body.CollisionMask = 0;
                AddChild(body);
            }
            else
            {
                body.QueueFree();
            }

            // NOTE: rivers carry no collision on purpose - they are passable but slow.
        }

        private void BuildBuildings()
        {
            var map = MapData!;
            int cap = Mathf.Max(1, MaxBuildingCellsPerBlock);

            foreach (var run in MergeRects(
                         (c, r) => map.ClassAt(c, r) == TerrainClass.BuildingsCanDestroy,
                         map.Columns, map.Rows, cap, cap))
            {
                SpawnBuilding(CellRectToWorld(run), true);
            }

            foreach (var run in MergeRects(
                         (c, r) => map.ClassAt(c, r) == TerrainClass.BuildingsNoDestroy,
                         map.Columns, map.Rows, cap, cap))
            {
                SpawnBuilding(CellRectToWorld(run), false);
            }
        }

        private void SpawnBuilding(Rect2 worldRect, bool canDestroy)
        {
            var building = new SemanticBuilding
            {
                Name = canDestroy ? "Building_Destructible" : "Building_Solid",
                Footprint = worldRect,
                CanDestroy = canDestroy,
                MaxHealth = BuildingHealth,
                Visible = DrawBuildings
            };

            _obstacles.Add(worldRect);
            building.Destroyed += OnBuildingDestroyed;
            _buildings.Add(building);
            AddChild(building);
        }

        private void OnBuildingDestroyed(SemanticBuilding building)
        {
            _obstacles.Remove(building.Footprint);
            _buildings.Remove(building);
        }

        // ---------------------------------------------------------------- node helpers

        protected void AddCaptureNode(int index, string name, string letter, Vector2 position, float radius)
        {
            var node = new CaptureNode
            {
                SectorIndex = index,
                SectorName = name,
                SectorLetter = letter,
                GlobalPosition = position,
                CaptureRadius = Mathf.Max(96f, radius)
            };
            AddChild(node);
            _nodes.Add(node);
        }

        protected void SetStagingPositions(Vector2 attackerStaging, Vector2 defenderRear)
        {
            _attackerStaging = attackerStaging;
            _defenderRear = defenderRear;
        }

        /// <summary>
        /// Objective size scaled to a city's built-up footprint: big enough that the attacker must
        /// actually hold the urban area, small enough that it never swallows the whole sheet.
        /// </summary>
        protected float SuggestCaptureRadius(Rect2I builtUpCells, float min = 240f, float max = 700f) =>
            Mathf.Clamp(0.12f * Mathf.Min(builtUpCells.Size.X, builtUpCells.Size.Y) * CellSize, min, max);

        /// <summary>Bounding box (in cells) of every non-default cell inside a column range.</summary>
        protected Rect2I BuiltUpBounds(int columnStart, int columnEnd)
        {
            var map = MapData!;
            int start = Mathf.Clamp(columnStart, 0, map.Columns - 1);
            int end = Mathf.Clamp(columnEnd, start + 1, map.Columns);

            int minC = int.MaxValue, minR = int.MaxValue, maxC = int.MinValue, maxR = int.MinValue;
            for (int column = start; column < end; column++)
            {
                for (int row = 0; row < map.Rows; row++)
                {
                    if (map.ClassAt(column, row) == TerrainClass.Default) continue;
                    minC = Mathf.Min(minC, column);
                    maxC = Mathf.Max(maxC, column);
                    minR = Mathf.Min(minR, row);
                    maxR = Mathf.Max(maxR, row);
                }
            }

            if (minC == int.MaxValue) return new Rect2I(start, 0, end - start, map.Rows);
            return new Rect2I(minC, minR, maxC - minC + 1, maxR - minR + 1);
        }

        // ---------------------------------------------------------------- raster helpers

        /// <summary>
        /// Greedy rectangle decomposition of a class mask (horizontal then vertical runs).
        /// Optional per-axis caps keep generated blocks within a playable size.
        /// </summary>
        protected static List<Rect2I> MergeRects(
            Func<int, int, bool> inClass, int columns, int rows,
            int maxWidth = int.MaxValue, int maxHeight = int.MaxValue)
        {
            var used = new bool[columns * rows];
            var result = new List<Rect2I>();

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (used[row * columns + column] || !inClass(column, row)) continue;

                    int width = 1;
                    while (column + width < columns
                           && width < maxWidth
                           && !used[row * columns + column + width]
                           && inClass(column + width, row))
                    {
                        width++;
                    }

                    int height = 1;
                    while (row + height < rows && height < maxHeight)
                    {
                        bool full = true;
                        for (int k = 0; k < width; k++)
                        {
                            if (used[(row + height) * columns + column + k] || !inClass(column + k, row + height))
                            {
                                full = false;
                                break;
                            }
                        }
                        if (!full) break;
                        height++;
                    }

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            used[(row + y) * columns + column + x] = true;
                        }
                    }

                    result.Add(new Rect2I(column, row, width, height));
                }
            }

            return result;
        }

        protected Rect2 CellRectToWorld(Rect2I run) =>
            new(run.Position.X * CellSize, run.Position.Y * CellSize, run.Size.X * CellSize, run.Size.Y * CellSize);

        protected static int[] BuildIntegralImage(SemanticMap map)
        {
            int width = map.Columns + 1;
            var integral = new int[width * (map.Rows + 1)];

            for (int row = 0; row < map.Rows; row++)
            {
                int rowSum = 0;
                for (int column = 0; column < map.Columns; column++)
                {
                    if (map.IsBuilding(column, row)) rowSum++;
                    integral[(row + 1) * width + column + 1] = integral[row * width + column + 1] + rowSum;
                }
            }

            return integral;
        }

        protected static int WindowSum(int[] integral, SemanticMap map, int left, int top, int right, int bottom)
        {
            int width = map.Columns + 1;
            int x0 = Mathf.Clamp(left, 0, map.Columns);
            int y0 = Mathf.Clamp(top, 0, map.Rows);
            int x1 = Mathf.Clamp(right, 0, map.Columns);
            int y1 = Mathf.Clamp(bottom, 0, map.Rows);

            return integral[y1 * width + x1]
                   - integral[y0 * width + x1]
                   - integral[y1 * width + x0]
                   + integral[y0 * width + x0];
        }

        /// <summary>
        /// Picks the walkable cell in a column band with the densest surrounding building mass,
        /// biased towards the band centre and the map's centre line.
        /// </summary>
        protected Vector2 FindDensestPassableCell(int[] integral, int bandStart, int bandEnd)
        {
            var map = MapData!;
            int start = Mathf.Clamp(Mathf.Min(bandStart, bandEnd), 0, map.Columns - 1);
            int end = Mathf.Clamp(Mathf.Max(bandStart, bandEnd), start + 1, map.Columns);

            int windowColumns = Mathf.Max(1, (end - start) / 3);
            int windowRows = Mathf.Max(1, map.Rows / 8);
            int bandCentre = (start + end) / 2;

            int bestColumn = -1, bestRow = -1, bestScore = int.MinValue;

            for (int column = start; column < end; column++)
            {
                for (int row = 0; row < map.Rows; row++)
                {
                    if (!map.IsPassable(column, row)) continue;

                    int score = WindowSum(integral, map, column - windowColumns, row - windowRows,
                                          column + windowColumns, row + windowRows);
                    int centrePenalty = Math.Abs(column - bandCentre) + Math.Abs(row - map.Rows / 2) / 2;
                    int biased = score * 1000 - centrePenalty;

                    if (biased > bestScore)
                    {
                        bestScore = biased;
                        bestColumn = column;
                        bestRow = row;
                    }
                }
            }

            if (bestColumn < 0)
            {
                bestColumn = bandCentre;
                bestRow = map.Rows / 2;
                GD.PushWarning($"Column band [{start},{end}) has no walkable cell; node placed at band centre.");
            }

            return map.CellCenterToWorld(bestColumn, bestRow);
        }

        /// <summary>Finds a walkable column near one edge, used for staging / rear bases.</summary>
        protected Vector2 FindEdgePosition(bool fromLeft)
        {
            var map = MapData!;

            int minColumn = -1, maxColumn = -1;
            for (int column = 0; column < map.Columns; column++)
            {
                for (int row = 0; row < map.Rows; row++)
                {
                    if (map.IsPassable(column, row))
                    {
                        if (minColumn < 0) minColumn = column;
                        maxColumn = column;
                        break;
                    }
                }
            }

            if (minColumn < 0)
            {
                throw new InvalidOperationException("Semantic map has no passable terrain to place units on.");
            }

            int span = Mathf.Max(1, maxColumn - minColumn);
            int inset = Mathf.Max(1, span / 12);
            int target = fromLeft ? Mathf.Min(minColumn + inset, maxColumn) : Mathf.Max(maxColumn - inset, minColumn);

            for (int step = 0; step <= span; step++)
            {
                int column = fromLeft
                    ? Mathf.Min(target + step, maxColumn)
                    : Mathf.Max(target - step, minColumn);

                var rows = new List<int>();
                for (int row = 0; row < map.Rows; row++)
                {
                    if (map.IsPassable(column, row)) rows.Add(row);
                }

                if (rows.Count > 0)
                {
                    return map.CellCenterToWorld(column, rows[rows.Count / 2]);
                }
            }

            return map.CellCenterToWorld(fromLeft ? minColumn : maxColumn, map.Rows / 2);
        }

        protected static string LetterFor(int index) => ((char)('A' + index)).ToString();
    }
}
