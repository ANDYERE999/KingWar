using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Gadgets
{
    public partial class DeployableCover : StaticBody2D, IDamageable
    {
        public Team Team { get; set; } = Team.None;
        public float MaxHealth { get; set; } = 300f;
        public float CurrentHealth { get; private set; } = 300f;
        public bool IsDead => CurrentHealth <= 0f;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;

            var col = new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(70, 24) }
            };
            AddChild(col);

            AudioManager.Instance?.PlaySpatial("deploy", GlobalPosition, 0f);
        }

        public void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            if (IsDead) return;
            CurrentHealth -= amount;
            QueueRedraw();

            if (hitPoint.HasValue && hitNormal.HasValue)
            {
                ParticleFactory.Instance?.SpawnSparks(hitPoint.Value, hitNormal.Value);
            }

            if (CurrentHealth <= 0f)
            {
                ParticleFactory.Instance?.SpawnExplosion(GlobalPosition, 60f);
                QueueFree();
            }
        }

        public override void _Draw()
        {
            // Outer shadow / border
            DrawRect(new Rect2(-36, -13, 72, 26), new Color(0.15f, 0.12f, 0.08f));

            // Sandbag fill
            Color bagColor = new Color(0.62f, 0.52f, 0.38f);
            Color highlight = new Color(0.72f, 0.62f, 0.46f);
            DrawRect(new Rect2(-34, -11, 68, 22), bagColor);

            // Three segmented sandbag rows
            DrawLine(new Vector2(-12, -11), new Vector2(-12, 11), new Color(0.35f, 0.28f, 0.18f), 2f);
            DrawLine(new Vector2(12, -11), new Vector2(12, 11), new Color(0.35f, 0.28f, 0.18f), 2f);
            DrawLine(new Vector2(-34, 0), new Vector2(34, 0), new Color(0.45f, 0.38f, 0.25f), 1.5f);

            // Health bar over cover if damaged
            if (CurrentHealth < MaxHealth)
            {
                float pct = CurrentHealth / MaxHealth;
                DrawRect(new Rect2(-30, -18, 60, 4), new Color(0.2f, 0.2f, 0.2f, 0.8f));
                DrawRect(new Rect2(-30, -18, 60 * pct, 4), new Color(0.3f, 0.85f, 0.4f, 0.9f));
            }
        }
    }
}
