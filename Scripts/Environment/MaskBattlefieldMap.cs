using System;
using System.Collections.Generic;
using Godot;

namespace KingWar.Environment
{
    /// <summary>
    /// Battlefield built from a single asset-pipeline <c>semantic_mask.png</c>.
    /// The map's passable extent is split along the advance axis (+X) into
    /// <see cref="SectorCount"/> sectors, each getting one capture node.
    /// </summary>
    public partial class MaskBattlefieldMap : SemanticBattlefieldBase
    {
        public const string DefaultMapRoot = "res://aseets/assets_generation_pipeline/generated_maps";
        public const string MaskFileName = "semantic_mask.png";

        [Export] public string MaskPath { get; set; } = "";
        [Export] public int SectorCount { get; set; } = 3;

        /// <summary>Lists every generated map that has a semantic mask, sorted by name.</summary>
        public static List<string> FindAvailableMaps(string root = DefaultMapRoot)
        {
            var found = new List<string>();
            using var dir = DirAccess.Open(root);
            if (dir == null) return found;

            foreach (var entry in dir.GetDirectories())
            {
                string candidate = $"{root}/{entry}/{MaskFileName}";
                if (FileAccess.FileExists(candidate))
                {
                    found.Add(candidate);
                }
            }
            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        /// <summary>Turns ".../generated_maps/Poland_Warsaw/semantic_mask.png" into "Poland_Warsaw".</summary>
        public static string DisplayName(string maskPath)
        {
            string directory = maskPath.GetBaseDir();
            string name = directory.GetFile();
            return string.IsNullOrEmpty(name) ? maskPath : name;
        }

        public string ResolvedMaskPath { get; private set; } = "";

        protected override SemanticMap BuildSemanticMap()
        {
            ResolvedMaskPath = ResolveMaskPath();
            var source = SemanticMap.Load(ResolvedMaskPath, CellSize);

            SourceColumns = source.Columns;
            SourceRows = source.Rows;
            var sampled = source.Downsample(TargetGridSize);

            GD.Print($"[MaskBattlefieldMap] {DisplayName(ResolvedMaskPath)}: 源掩膜 {SourceColumns}x{SourceRows} " +
                     $"→ 战场栅格 {sampled.Columns}x{sampled.Rows} (cell {CellSize}px, " +
                     $"world {sampled.WorldWidth}x{sampled.WorldHeight})");
            return sampled;
        }

        private string ResolveMaskPath()
        {
            if (!string.IsNullOrWhiteSpace(MaskPath) && FileAccess.FileExists(MaskPath))
            {
                return MaskPath;
            }

            string configured = Common.MatchSettings.SelectedMapPath;
            if (!string.IsNullOrWhiteSpace(configured) && FileAccess.FileExists(configured))
            {
                return configured;
            }

            var available = FindAvailableMaps();
            if (available.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No '{MaskFileName}' found under '{DefaultMapRoot}'. " +
                    "Run aseets/assets_generation_pipeline/build_maps.py first.");
            }
            return available[0];
        }

        protected override void PlaceCaptureNodes()
        {
            var map = MapData!;
            int sectorCount = Mathf.Max(1, SectorCount);

            int minColumn = int.MaxValue, maxColumn = int.MinValue;
            for (int column = 0; column < map.Columns; column++)
            {
                for (int row = 0; row < map.Rows; row++)
                {
                    if (!map.IsPassable(column, row)) continue;
                    minColumn = Mathf.Min(minColumn, column);
                    maxColumn = Mathf.Max(maxColumn, column);
                    break;
                }
            }

            if (minColumn == int.MaxValue)
            {
                throw new InvalidOperationException("Semantic map has no passable terrain to place capture nodes on.");
            }

            SetStagingPositions(FindEdgePosition(fromLeft: true), FindEdgePosition(fromLeft: false));

            var integral = BuildIntegralImage(map);
            int span = Mathf.Max(1, maxColumn - minColumn);
            string city = DisplayName(ResolvedMaskPath);
            var bounds = BuiltUpBounds(minColumn, maxColumn + 1);
            float radius = SuggestCaptureRadius(bounds);

            for (int index = 0; index < sectorCount; index++)
            {
                int bandStart = minColumn + span * index / sectorCount;
                int bandEnd = minColumn + span * (index + 1) / sectorCount;
                Vector2 position = FindDensestPassableCell(integral, bandStart, bandEnd);
                AddCaptureNode(index, $"{city} {index + 1}", LetterFor(index), position, radius);
            }
        }
    }
}
