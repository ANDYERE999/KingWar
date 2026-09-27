using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Environment
{
    public enum CoverType
    {
        Sandbag,
        Crate,
        ConcreteBarrier,
        MetalContainer,
        Tree
    }

    public partial class CoverObject : StaticBody2D, IDamageable
    {
        [Export] public CoverType Type { get; set; } = CoverType.Sandbag;
        [Export] public Vector2 Size { get; set; } = new Vector2(48, 24);
        [Export] public bool IsDestructible { get; set; } = false;
        [Export] public float MaxHealth { get; set; } = 200f;

        public Team Team => Team.None;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;

            var shape = new CollisionShape2D();
            if (Type == CoverType.Tree)
            {
                shape.Shape = new CircleShape2D { Radius = Size.X * 0.35f };
            }
            else
            {
                shape.Shape = new RectangleShape2D { Size = Size };
            }
            AddChild(shape);
        }

        public void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            if (!IsDestructible || IsDead) return;

            CurrentHealth -= amount;
            if (hitPoint.HasValue && hitNormal.HasValue)
            {
                ParticleFactory.Instance?.SpawnSparks(hitPoint.Value, hitNormal.Value);
            }

            if (CurrentHealth <= 0f)
            {
                ParticleFactory.Instance?.SpawnExplosion(GlobalPosition, 50f);
                QueueFree();
            }
            else
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            Rect2 rect = new Rect2(-Size.X * 0.5f, -Size.Y * 0.5f, Size.X, Size.Y);

            switch (Type)
            {
                case CoverType.Sandbag:
                    DrawRect(rect, new Color(0.62f, 0.52f, 0.38f));
                    DrawRect(rect.Grow(-2f), new Color(0.72f, 0.62f, 0.46f));
                    DrawLine(new Vector2(-Size.X * 0.5f, 0), new Vector2(Size.X * 0.5f, 0), new Color(0.4f, 0.3f, 0.2f), 1.5f);
                    break;

                case CoverType.Crate:
                    DrawRect(rect, new Color(0.48f, 0.36f, 0.22f));
                    DrawRect(rect.Grow(-3f), new Color(0.58f, 0.44f, 0.28f));
                    DrawLine(rect.Position, rect.End, new Color(0.35f, 0.25f, 0.15f), 2f);
                    DrawLine(new Vector2(rect.Position.X, rect.End.Y), new Vector2(rect.End.X, rect.Position.Y), new Color(0.35f, 0.25f, 0.15f), 2f);
                    break;

                case CoverType.ConcreteBarrier:
                    DrawRect(rect, new Color(0.42f, 0.44f, 0.42f));
                    DrawRect(rect.Grow(-2f), new Color(0.55f, 0.57f, 0.55f));
                    // Caution stripes
                    DrawLine(new Vector2(-Size.X * 0.3f, -Size.Y * 0.3f), new Vector2(-Size.X * 0.15f, Size.Y * 0.3f), Colors.Yellow, 3f);
                    DrawLine(new Vector2(Size.X * 0.15f, -Size.Y * 0.3f), new Vector2(Size.X * 0.3f, Size.Y * 0.3f), Colors.Yellow, 3f);
                    break;

                case CoverType.MetalContainer:
                    DrawRect(rect, new Color(0.2f, 0.35f, 0.45f));
                    DrawRect(rect.Grow(-2f), new Color(0.28f, 0.48f, 0.6f));
                    for (float x = -Size.X * 0.4f; x < Size.X * 0.45f; x += 12f)
                    {
                        DrawLine(new Vector2(x, -Size.Y * 0.4f), new Vector2(x, Size.Y * 0.4f), new Color(0.18f, 0.3f, 0.4f), 2f);
                    }
                    break;

                case CoverType.Tree:
                    // Canopy top-down
                    DrawCircle(Vector2.Zero, Size.X * 0.5f, new Color(0.18f, 0.38f, 0.18f, 0.85f));
                    DrawCircle(new Vector2(-4, -4), Size.X * 0.4f, new Color(0.25f, 0.48f, 0.22f, 0.9f));
                    DrawCircle(Vector2.Zero, Size.X * 0.2f, new Color(0.38f, 0.25f, 0.15f));
                    break;
            }
        }
    }
}
