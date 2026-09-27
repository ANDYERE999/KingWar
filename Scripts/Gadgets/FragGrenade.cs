using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Gadgets
{
    public partial class FragGrenade : CharacterBody2D
    {
        public Team Team { get; set; } = Team.None;
        public Node2D? Thrower { get; set; }
        public float Damage { get; set; } = 120f;
        public float BlastRadius { get; set; } = 140f;
        public float FuseTime { get; set; } = 2.2f;

        private float _fuseTimer = 0f;
        private float _bounceDamping = 0.55f;

        public override void _Ready()
        {
            _fuseTimer = FuseTime;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _fuseTimer -= dt;

            // Apply friction
            Velocity *= Mathf.Pow(0.15f, dt);

            var collision = MoveAndCollide(Velocity * dt);
            if (collision != null)
            {
                Velocity = Velocity.Bounce(collision.GetNormal()) * _bounceDamping;
                if (Velocity.Length() > 40f)
                {
                    AudioManager.Instance?.PlaySpatial("grenade_bounce", GlobalPosition, -8f);
                }
            }

            QueueRedraw();

            if (_fuseTimer <= 0f)
            {
                Explode();
            }
        }

        private void Explode()
        {
            ParticleFactory.Instance?.SpawnExplosion(GlobalPosition, BlastRadius);

            var spaceState = GetWorld2D().DirectSpaceState;
            var shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = new CircleShape2D { Radius = BlastRadius },
                Transform = new Transform2D(0f, GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = true
            };

            var results = spaceState.IntersectShape(shapeQuery, 24);
            foreach (var r in results)
            {
                var collider = (Node)r["collider"].AsGodotObject();
                if (collider is IDamageable target && !target.IsDead)
                {
                    float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
                    float falloff = Mathf.Clamp(1.0f - (dist / BlastRadius), 0.25f, 1.0f);
                    float dmg = Damage * falloff;

                    if (target.Team == Team) dmg *= 0.4f;

                    target.TakeDamage(dmg, Thrower, GlobalPosition, (target.GlobalPosition - GlobalPosition).Normalized(), DamageType.GrenadeShrapnel);
                }
            }

            QueueFree();
        }

        public override void _Draw()
        {
            // Pineapple grenade body
            DrawCircle(Vector2.Zero, 5.5f, new Color(0.25f, 0.35f, 0.2f));
            DrawCircle(Vector2.Zero, 4.0f, new Color(0.35f, 0.45f, 0.28f));

            // Pin/spoon
            DrawRect(new Rect2(-1.5f, -7.5f, 3f, 3f), new Color(0.7f, 0.7f, 0.7f));

            // Fuse blinker
            float blink = Mathf.Sin((FuseTime - _fuseTimer) * 16f);
            if (blink > 0f)
            {
                DrawCircle(new Vector2(0, -6f), 2f, Colors.Red);
            }
        }
    }
}
