using System.Collections.Generic;
using Godot;
using KingWar.Objectives;

namespace KingWar.Environment
{
    /// <summary>
    /// Common contract for a playable battlefield so the game can run on either the
    /// hand-authored demo strip (<see cref="BattlefieldMap"/>) or a map generated from a
    /// pipeline <c>semantic_mask.png</c> (<see cref="MaskBattlefieldMap"/>).
    /// </summary>
    public abstract partial class BattlefieldBase : Node2D
    {
        /// <summary>Speed multiplier while crossing a river.</summary>
        public const float RiverSpeedMultiplier = 0.60f;

        /// <summary>Speed multiplier while crossing mountain terrain (infantry only).</summary>
        public const float MountainSpeedMultiplier = 0.50f;

        /// <summary>Physics layer reserved for mountain collision, which only vehicles collide with.</summary>
        public const uint MountainCollisionLayer = 4; // bit 3

        /// <summary>The battlefield currently in the scene tree, if any.</summary>
        public static BattlefieldBase? Current { get; private set; }

        public abstract float MapWidth { get; }
        public abstract float MapHeight { get; }
        public abstract IReadOnlyList<CaptureNode> CaptureNodes { get; }
        public abstract IReadOnlyList<Rect2> ObstacleRectangles { get; }
        public abstract Vector2 AttackerStagingPosition { get; }
        public abstract Vector2 DefenderRearPosition { get; }

        /// <summary>Terrain class under a world-space point. Defaults to plain ground.</summary>
        public virtual TerrainClass GetTerrainAt(Vector2 worldPosition) => TerrainClass.Default;

        /// <summary>Ground raster used by the minimap, when the battlefield has one.</summary>
        public virtual Texture2D? GroundTexture => null;

        /// <summary>
        /// Resolves a spawn point at the given advance-axis position, snapped to the nearest
        /// walkable ground so units never appear inside water, rock or a building.
        /// </summary>
        public virtual Vector2 FindSpawnPoint(float x, float preferredY) => new(x, preferredY);

        /// <summary>
        /// Movement speed multiplier from terrain. Rivers and mountains slow everything down;
        /// mountains additionally block vehicles outright via collision layers.
        /// </summary>
        public virtual float GetSpeedMultiplier(Vector2 worldPosition, bool isVehicle) =>
            SpeedMultiplierFor(GetTerrainAt(worldPosition));

        /// <summary>True when vehicles are physically blocked at this point.</summary>
        public virtual bool BlocksVehicles(Vector2 worldPosition) =>
            TerrainBlocksVehicles(GetTerrainAt(worldPosition));

        /// <summary>Pure terrain → speed rule, so the mapping is verifiable without a live map.</summary>
        public static float SpeedMultiplierFor(TerrainClass terrain) => terrain switch
        {
            TerrainClass.River => RiverSpeedMultiplier,
            TerrainClass.Mountain => MountainSpeedMultiplier,
            _ => 1f
        };

        /// <summary>Pure terrain → vehicle blocking rule. Only mountains stop vehicles.</summary>
        public static bool TerrainBlocksVehicles(TerrainClass terrain) => terrain == TerrainClass.Mountain;

        /// <summary>Builds terrain, collision and capture nodes. Safe to call more than once.</summary>
        public abstract void InitializeMap();

        private SectorBarrier? _truceBarrier;

        public override void _Ready()
        {
            Current = this;
            InitializeMap();

            _truceBarrier = new SectorBarrier();
            _truceBarrier.Configure(MapHeight);
            AddChild(_truceBarrier);

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnTruceStarted += HandleTruceStarted;
                ObjectiveManager.Instance.OnTruceEnded += HandleTruceEnded;
                if (ObjectiveManager.Instance.IsTruceActive)
                {
                    HandleTruceStarted();
                }
            }
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
        }

        private void HandleTruceStarted()
        {
            if (ObjectiveManager.Instance == null || _truceBarrier == null) return;
            var (left, _) = ObjectiveManager.Instance.GetSectorBounds(ObjectiveManager.Instance.ActiveSectorIndex);
            _truceBarrier.SetBarrierActive(left, true);
        }

        private void HandleTruceEnded()
        {
            _truceBarrier?.SetBarrierActive(0f, false);
        }
    }
}
