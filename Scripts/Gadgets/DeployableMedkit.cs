using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Gadgets
{
    public partial class DeployableMedkit : Node2D, IDamageable
    {
        public Team Team { get; set; } = Team.None;
        public bool IsDead => _isDead;
        private bool _isDead = false;

        private float _lifetime = 18f;
        private float _healTimer = 0.5f;
        private const float HealRadius = 140f;
        private const float HealPerTick = 12f;

        public override void _Ready()
        {
            AudioManager.Instance?.PlaySpatial("deploy", GlobalPosition, -2f);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0f)
            {
                QueueFree();
                return;
            }

            _healTimer -= dt;
            if (_healTimer <= 0f)
            {
                _healTimer = 0.5f;
                PerformHeal();
            }

            QueueRedraw();
        }

        private void PerformHeal()
        {
            var spaceState = GetWorld2D().DirectSpaceState;
            var shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = new CircleShape2D { Radius = HealRadius },
                Transform = new Transform2D(0f, GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = true
            };

            var results = spaceState.IntersectShape(shapeQuery, 16);
            bool healedAnyone = false;

            foreach (var r in results)
            {
                var collider = (Node)r["collider"].AsGodotObject();
                if (collider is Soldier soldier && soldier.Team == Team && !soldier.IsDead && soldier.CurrentHealth < soldier.MaxHealth)
                {
                    soldier.Heal(HealPerTick);
                    ParticleFactory.Instance?.SpawnHealEffect(soldier.GlobalPosition);
                    healedAnyone = true;
                }
            }

            if (healedAnyone)
            {
                AudioManager.Instance?.PlaySpatial("heal", GlobalPosition, -6f);
            }
        }

        public void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            _lifetime -= amount * 0.1f;
            if (_lifetime <= 0f)
            {
                _isDead = true;
                QueueFree();
            }
        }

        public override void _Draw()
        {
            // Subtle aura circle
            Color aura = new Color(0.2f, 0.9f, 0.3f, 0.08f + Mathf.Sin((float)Time.GetTicksMsec() * 0.005f) * 0.04f);
            DrawCircle(Vector2.Zero, HealRadius, aura);
            DrawArc(Vector2.Zero, HealRadius, 0f, Mathf.Tau, 32, new Color(0.2f, 0.9f, 0.3f, 0.25f), 1.5f);

            // Crate body
            DrawRect(new Rect2(-14, -10, 28, 20), new Color(0.2f, 0.35f, 0.2f));
            DrawRect(new Rect2(-12, -8, 24, 16), new Color(0.28f, 0.45f, 0.28f));

            // White square & Red cross
            DrawRect(new Rect2(-6, -6, 12, 12), Colors.White);
            DrawRect(new Rect2(-1.5f, -4.5f, 3f, 9f), new Color(0.85f, 0.15f, 0.15f));
            DrawRect(new Rect2(-4.5f, -1.5f, 9f, 3f), new Color(0.85f, 0.15f, 0.15f));
        }
    }
}
