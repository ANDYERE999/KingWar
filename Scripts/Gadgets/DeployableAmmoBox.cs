using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;

namespace KingWar.Gadgets
{
    public partial class DeployableAmmoBox : Node2D, IDamageable
    {
        public Team Team { get; set; } = Team.None;
        public bool IsDead => _isDead;
        private bool _isDead = false;

        private float _lifetime = 22f;
        private float _resupplyTimer = 1.2f;
        private const float Radius = 140f;

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

            _resupplyTimer -= dt;
            if (_resupplyTimer <= 0f)
            {
                _resupplyTimer = 1.5f;
                PerformResupply();
            }

            QueueRedraw();
        }

        private void PerformResupply()
        {
            var spaceState = GetWorld2D().DirectSpaceState;
            var shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = new CircleShape2D { Radius = Radius },
                Transform = new Transform2D(0f, GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = true
            };

            var results = spaceState.IntersectShape(shapeQuery, 16);
            foreach (var r in results)
            {
                var collider = (Node)r["collider"].AsGodotObject();
                if (collider is Soldier soldier && soldier.Team == Team && !soldier.IsDead)
                {
                    soldier.ResupplyAmmoAndGadgets();
                }
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
            Color aura = new Color(0.95f, 0.8f, 0.2f, 0.08f + Mathf.Sin((float)Time.GetTicksMsec() * 0.005f) * 0.04f);
            DrawCircle(Vector2.Zero, Radius, aura);
            DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 32, new Color(0.95f, 0.8f, 0.2f, 0.25f), 1.5f);

            // Metal ammo tin
            DrawRect(new Rect2(-15, -9, 30, 18), new Color(0.3f, 0.35f, 0.25f));
            DrawRect(new Rect2(-13, -7, 26, 14), new Color(0.42f, 0.46f, 0.32f));

            // Yellow ammo stencil bullets
            DrawRect(new Rect2(-8, -4, 4, 8), new Color(0.95f, 0.85f, 0.3f));
            DrawRect(new Rect2(-2, -4, 4, 8), new Color(0.95f, 0.85f, 0.3f));
            DrawRect(new Rect2(4, -4, 4, 8), new Color(0.95f, 0.85f, 0.3f));
        }
    }
}
