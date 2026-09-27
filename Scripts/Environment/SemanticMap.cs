using System;
using System.Collections.Generic;
using Godot;

namespace KingWar.Environment
{
    /// <summary>
    /// The six element classes emitted by the asset pipeline. Pixel values in
    /// <c>semantic_mask.png</c> are the numeric IDs.
    /// </summary>
    public enum TerrainClass
    {
        Default = 0,
        River = 1,
        Mountain = 2,
        Snow = 3,
        BuildingsCanDestroy = 4,
        BuildingsNoDestroy = 5
    }

    /// <summary>
    /// Immutable view over a pipeline semantic mask: one byte per cell with the
    /// <see cref="TerrainClass"/> id. Provides cell/world conversion and passability queries.
    /// </summary>
    public sealed class SemanticMap
    {
        public const int MaxClassId = 5;

        public int Columns { get; }
        public int Rows { get; }
        public float CellSize { get; }

        public float WorldWidth => Columns * CellSize;
        public float WorldHeight => Rows * CellSize;

        private readonly byte[] _cells;

        public SemanticMap(int columns, int rows, float cellSize, byte[] cells)
        {
            if (columns <= 0 || rows <= 0)
            {
                throw new ArgumentException($"Semantic map must have a positive size, got {columns}x{rows}.");
            }
            if (cells.Length != columns * rows)
            {
                throw new ArgumentException($"Expected {columns * rows} cells but received {cells.Length}.");
            }
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            }

            Columns = columns;
            Rows = rows;
            CellSize = cellSize;
            _cells = cells;
        }

        public bool InBounds(int column, int row) =>
            column >= 0 && row >= 0 && column < Columns && row < Rows;

        public TerrainClass ClassAt(int column, int row) =>
            InBounds(column, row) ? (TerrainClass)_cells[row * Columns + column] : TerrainClass.Default;

        public byte RawIdAt(int column, int row) =>
            InBounds(column, row) ? _cells[row * Columns + column] : (byte)0;

        /// <summary>Open ground and snow can be walked on; water, rock and buildings block.</summary>
        public bool IsPassable(int column, int row)
        {
            var cls = ClassAt(column, row);
            return cls is TerrainClass.Default or TerrainClass.Snow;
        }

        public bool BlocksMovement(int column, int row) => !IsPassable(column, row);

        public bool IsBuilding(int column, int row)
        {
            var cls = ClassAt(column, row);
            return cls is TerrainClass.BuildingsCanDestroy or TerrainClass.BuildingsNoDestroy;
        }

        public Vector2 CellCenterToWorld(int column, int row) =>
            new((column + 0.5f) * CellSize, (row + 0.5f) * CellSize);

        public Vector2I WorldToCell(Vector2 worldPosition) =>
            new(Mathf.FloorToInt(worldPosition.X / CellSize), Mathf.FloorToInt(worldPosition.Y / CellSize));

        public int CountClass(TerrainClass cls)
        {
            int count = 0;
            foreach (var cell in _cells)
            {
                if (cell == (byte)cls) count++;
            }
            return count;
        }

        public IReadOnlyDictionary<TerrainClass, int> ClassHistogram()
        {
            var histogram = new Dictionary<TerrainClass, int>();
            foreach (TerrainClass cls in Enum.GetValues<TerrainClass>())
            {
                histogram[cls] = 0;
            }
            foreach (var cell in _cells)
            {
                var cls = (TerrainClass)cell;
                histogram[cls] = histogram[cls] + 1;
            }
            return histogram;
        }

        /// <summary>
        /// Majority-vote downsample to a game-sized grid. Ties resolve to the lowest class id,
        /// matching the pipeline's grid sampling so both consumers agree.
        /// </summary>
        public SemanticMap Downsample(int targetLongestSide)
        {
            if (targetLongestSide <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetLongestSide));
            }

            int longest = Mathf.Max(Columns, Rows);
            if (longest <= targetLongestSide) return this;

            float scale = (float)targetLongestSide / longest;
            int newColumns = Mathf.Max(1, Mathf.RoundToInt(Columns * scale));
            int newRows = Mathf.Max(1, Mathf.RoundToInt(Rows * scale));
            var reduced = new byte[newColumns * newRows];
            Span<int> counts = stackalloc int[MaxClassId + 1];

            for (int row = 0; row < newRows; row++)
            {
                int top = row * Rows / newRows;
                int bottom = Mathf.Max(top + 1, (row + 1) * Rows / newRows);

                for (int column = 0; column < newColumns; column++)
                {
                    int left = column * Columns / newColumns;
                    int right = Mathf.Max(left + 1, (column + 1) * Columns / newColumns);

                    counts.Clear();
                    for (int y = top; y < bottom; y++)
                    {
                        int rowOffset = y * Columns;
                        for (int x = left; x < right; x++)
                        {
                            counts[_cells[rowOffset + x]]++;
                        }
                    }

                    int best = 0;
                    for (int id = 1; id <= MaxClassId; id++)
                    {
                        // Strict comparison keeps the lowest id when counts tie.
                        if (counts[id] > counts[best]) best = id;
                    }
                    reduced[row * newColumns + column] = (byte)best;
                }
            }

            return new SemanticMap(newColumns, newRows, CellSize, reduced);
        }

        /// <summary>Decodes raw PNG bytes. Every pixel must be an exact class id (0..5).</summary>
        public static SemanticMap FromPngBytes(byte[] pngBytes, float cellSize)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                throw new ArgumentException("Semantic mask bytes are empty.", nameof(pngBytes));
            }

            var image = new Image();
            var error = image.LoadPngFromBuffer(pngBytes);
            if (error != Error.Ok)
            {
                throw new InvalidOperationException($"Semantic mask is not a readable PNG ({error}).");
            }

            // Normalise to 8-bit single channel so ids survive byte-for-byte.
            if (image.GetFormat() != Image.Format.L8)
            {
                image.Convert(Image.Format.L8);
            }

            int columns = image.GetWidth();
            int rows = image.GetHeight();
            byte[] cells = image.GetData();

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] > MaxClassId)
                {
                    throw new InvalidOperationException(
                        $"Semantic mask pixel {i} has id {cells[i]}; only 0..{MaxClassId} are valid. " +
                        "Pass the single-channel semantic_mask.png, not the colour preview.png.");
                }
            }

            return new SemanticMap(columns, rows, cellSize, cells);
        }

        /// <summary>Loads a <c>semantic_mask.png</c> straight from disk, bypassing Godot's texture import.</summary>
        public static SemanticMap Load(string resPath, float cellSize)
        {
            using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                throw new InvalidOperationException(
                    $"Cannot open semantic mask '{resPath}' ({FileAccess.GetOpenError()}).");
            }

            return FromPngBytes(file.GetBuffer((long)file.GetLength()), cellSize);
        }
    }
}
