using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Environment
{
    /// <summary>
    /// A solid block of city fabric produced from a merged run of <c>BuildingsCanDestroy</c> or
    /// <c>BuildingsNoDestroy</c> cells.
    /// <para>
    /// It IS the physics body (like <see cref="CoverObject"/>), not a wrapper around one: bullet
    /// raycasts and explosion shape queries return the collider directly, and those systems only
    /// deal damage when the collider itself implements <see cref="IDamageable"/>. Wrapping the
    /// collision in a child body would silently make buildings immune to every weapon.
    /// </para>
    /// </summary>
    public partial class SemanticBuilding : StaticBody2D, IDamageable
    {
        /// <summary>World-space footprint. Must be set before the node enters the tree.</summary>
        public Rect2 Footprint { get; set; }

        public bool CanDestroy { get; set; } = true;
        public float MaxHealth { get; set; } = 250f;

        public Team Team => Team.None;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        /// <summary>Raised just before the node is freed so the map can drop its obstacle rectangle.</summary>
        public event Action<SemanticBuilding>? Destroyed;

        // Colours match the ground raster palette so intact blocks blend into the city texture and
        // only visible change is progressive darkening as the structure takes damage.
        private static readonly Color DestructibleFill = new(188 / 255f, 142 / 255f, 91 / 255f);
        private static readonly Color IndestructibleFill = new(102 / 255f, 74 / 255f, 58 / 255f);
        private static readonly Color Outline = new(0.10f, 0.09f, 0.08f, 0.55f);

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            Position = Footprint.Position + Footprint.Size * 0.5f;

            // Direct child shape on this body, so queries resolve straight to this node.
            AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = Footprint.Size }
            });
        }

        public void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            if (!CanDestroy || IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;

            CurrentHealth -= amount;
            if (hitPoint.HasValue && hitNormal.HasValue)
            {
                ParticleFactory.Instance?.SpawnSparks(hitPoint.Value, hitNormal.Value);
            }

            if (CurrentHealth <= 0f)
            {
                ParticleFactory.Instance?.SpawnExplosion(GlobalPosition, Mathf.Max(Footprint.Size.X, Footprint.Size.Y) * 0.5f);
                Destroyed?.Invoke(this);
                QueueFree();
            }
            else
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            Rect2 local = new(-Footprint.Size * 0.5f, Footprint.Size);
            Color fill = CanDestroy ? DestructibleFill : IndestructibleFill;

            // Damage darkens the block so players can read structural wear.
            if (CanDestroy && CurrentHealth < MaxHealth)
            {
                fill = fill.Lerp(Colors.Black, 0.45f * (1f - CurrentHealth / MaxHealth));
            }

            DrawRect(local, fill);
            DrawRect(local, Outline, false, 1.5f);
        }
    }
}
