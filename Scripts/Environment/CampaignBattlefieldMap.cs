using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace KingWar.Environment
{
    /// <summary>
    /// A campaign battlefield that stitches several city masks into one continuous corridor.
    /// Each city contributes exactly one capture node; the ground between cities is filled with
    /// plain terrain so the advance routes read as open countryside rather than a hard seam.
    /// </summary>
    public partial class CampaignBattlefieldMap : SemanticBattlefieldBase
    {
        /// <summary>City masks stitched left to right along the advance axis, in campaign order.</summary>
        [Export]
        public string[] CityMaskPaths { get; set; } =
        {
            "res://aseets/assets_generation_pipeline/generated_maps/Poland_Danzig/semantic_mask.png",
            "res://aseets/assets_generation_pipeline/generated_maps/Poland_Poznan/semantic_mask.png",
            "res://aseets/assets_generation_pipeline/generated_maps/Poland_Warsaw/semantic_mask.png"
        };

        /// <summary>Plain-terrain corridor between two cities, in cells.</summary>
        [Export] public int GapCells { get; set; } = 40;

        [Export] public float CampaignCaptureRadiusMin { get; set; } = 240f;
        [Export] public float CampaignCaptureRadiusMax { get; set; } = 700f;

        private readonly List<Rect2I> _cityRanges = new();
        private readonly List<string> _cityLabels = new();

        public IReadOnlyList<string> CityLabels => _cityLabels;

        /// <summary>Column/row rectangle each stitched city occupies, in stitched-grid cells.</summary>
        public IReadOnlyList<Rect2I> CityRanges => _cityRanges;

        /// <summary>"res://.../Poland_Danzig/semantic_mask.png" -> "Danzig".</summary>
        public static string CityLabel(string maskPath)
        {
            string directory = maskPath.GetBaseDir();
            string stem = directory.GetFile();
            int underscore = stem.LastIndexOf('_');
            return underscore >= 0 && underscore + 1 < stem.Length ? stem[(underscore + 1)..] : stem;
        }

        protected override SemanticMap BuildSemanticMap()
        {
            var cities = new List<SemanticMap>();
            _cityRanges.Clear();
            _cityLabels.Clear();

            foreach (string path in CityMaskPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !FileAccess.FileExists(path))
                {
                    GD.PushWarning($"[CampaignBattlefieldMap] 跳过缺失的城市地图: {path}");
                    continue;
                }

                var source = SemanticMap.Load(path, CellSize);
                SourceColumns = Mathf.Max(SourceColumns, source.Columns);
                SourceRows = Mathf.Max(SourceRows, source.Rows);
                cities.Add(source.Downsample(TargetGridSize));
                _cityLabels.Add(CityLabel(path));
            }

            if (cities.Count == 0)
            {
                throw new InvalidOperationException(
                    "CampaignBattlefieldMap has no readable city masks. " +
                    "Run aseets/assets_generation_pipeline/build_maps.py first.");
            }

            int gap = Mathf.Max(0, GapCells);
            int columns = cities.Sum(city => city.Columns) + gap * (cities.Count - 1);
            int rows = cities.Max(city => city.Rows);

            // Plain terrain everywhere by default: that is what joins the cities together.
            var raster = new byte[columns * rows];

            int offsetColumns = 0;
            for (int index = 0; index < cities.Count; index++)
            {
                var city = cities[index];
                int top = (rows - city.Rows) / 2;

                for (int row = 0; row < city.Rows; row++)
                {
                    int destination = (top + row) * columns + offsetColumns;
                    for (int column = 0; column < city.Columns; column++)
                    {
                        raster[destination + column] = city.RawIdAt(column, row);
                    }
                }

                _cityRanges.Add(new Rect2I(offsetColumns, top, city.Columns, city.Rows));
                offsetColumns += city.Columns + gap;
            }

            GD.Print($"[CampaignBattlefieldMap] {cities.Count} 座城市拼接: {string.Join(" ➔ ", _cityLabels)}" +
                     $" | 城市间平原走廊 {gap} 格 | 总栅格 {columns}x{rows} " +
                     $"(world {columns * CellSize}x{rows * CellSize})");
            return new SemanticMap(columns, rows, CellSize, raster);
        }

        protected override void PlaceCaptureNodes()
        {
            var integral = BuildIntegralImage(MapData!);

            SetStagingPositions(FindEdgePosition(fromLeft: true), FindEdgePosition(fromLeft: false));

            for (int index = 0; index < _cityRanges.Count; index++)
            {
                var range = _cityRanges[index];
                Vector2 position = FindDensestPassableCell(integral, range.Position.X, range.Position.X + range.Size.X);

                // Objective size follows each city's own built-up footprint.
                var bounds = BuiltUpBounds(range.Position.X, range.Position.X + range.Size.X);
                float radius = SuggestCaptureRadius(bounds, CampaignCaptureRadiusMin, CampaignCaptureRadiusMax);

                AddCaptureNode(index, _cityLabels[index], LetterFor(index), position, radius);
                GD.Print($"   据点 {LetterFor(index)} {_cityLabels[index]}: 中心 ({position.X:0}, {position.Y:0}) | " +
                         $"占领半径 {radius:0}px | 城区包围盒 {bounds.Size.X}x{bounds.Size.Y} 格");
            }
        }
    }
}
