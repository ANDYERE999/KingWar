using Godot;
using KingWar.Common;

namespace KingWar.Environment
{
    public partial class SectorBarrier : StaticBody2D
    {
        private CollisionShape2D _collisionShape = null!;
        private RectangleShape2D _rectShape = null!;
        private float _pulseTimer = 0f;
        private float _worldHeight = 1200f;
        public bool IsBarrierActive { get; private set; } = false;

        /// <summary>Matches the barrier's span and draw geometry to the battlefield height.</summary>
        public void Configure(float worldHeight)
        {
            if (worldHeight > 0f) _worldHeight = worldHeight;
            ApplyHeight();
        }

        private void ApplyHeight()
        {
            if (_rectShape == null || _collisionShape == null) return;
            _rectShape.Size = new Vector2(32f, _worldHeight);
            _collisionShape.Position = Vector2.Zero;
            Position = new Vector2(Position.X, _worldHeight * 0.5f);
            QueueRedraw();
        }

        public override void _Ready()
        {
            _rectShape = new RectangleShape2D { Size = new Vector2(32f, _worldHeight) };
            _collisionShape = new CollisionShape2D { Shape = _rectShape };
            AddChild(_collisionShape);
            Position = new Vector2(-500f, _worldHeight * 0.5f);
            SetCollisionDisabled(true);
        }

        public override void _Process(double delta)
        {
            if (!IsBarrierActive) return;
            _pulseTimer += (float)delta * 4f;
            QueueRedraw();
        }

        public void SetBarrierActive(float xPosition, bool active)
        {
            IsBarrierActive = active;
            Position = new Vector2(xPosition, _worldHeight * 0.5f);
            SetCollisionDisabled(!active);
            Visible = active;
            QueueRedraw();
        }

        private void SetCollisionDisabled(bool disabled)
        {
            if (_collisionShape != null)
            {
                _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, disabled);
            }
        }

        public override void _Draw()
        {
            if (!IsBarrierActive) return;

            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            Color energyColor = new Color(0.18f, 0.65f, 1.0f, 0.45f + pulse * 0.25f);
            Color beamColor = new Color(0.85f, 0.95f, 1.0f, 0.90f);

            // Wide energy fence glow (covers full battlefield height)
            float half = _worldHeight * 0.5f;
            DrawRect(new Rect2(-16f, -half, 32f, _worldHeight), energyColor);
            // Core laser line
            DrawLine(new Vector2(0f, -half), new Vector2(0f, half), beamColor, 3f);

            // Warning diagonal hazard stripes
            for (float y = -half + 20f; y < half; y += 75f)
            {
                DrawLine(new Vector2(-12f, y), new Vector2(12f, y + 24f), Colors.Yellow, 2.5f);
            }
        }
    }
}
