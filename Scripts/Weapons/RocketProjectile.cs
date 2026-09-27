using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Weapons
{
    public partial class RocketProjectile : Node2D
    {
        public Team Team { get; set; } = Team.None;
        public Node2D? Shooter { get; set; }
        public float DirectDamage { get; set; } = 150f;
        public float SplashDamage { get; set; } = 130f;
        public float BlastRadius { get; set; } = 180f;
        public float Speed { get; set; } = 400f;
        public float MaxSpeed { get; set; } = 1200f;
        public float Acceleration { get; set; } = 1200f;
        public Vector2 Velocity { get; set; }

        private float _aliveTime = 0f;
        private float _smokeTimer = 0f;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _aliveTime += dt;

            // Accelerate
            Speed = Mathf.Min(MaxSpeed, Speed + Acceleration * dt);
            Vector2 step = Velocity.Normalized() * (Speed * dt);
            Vector2 nextPos = GlobalPosition + step;

            // Spawn smoke trail
            _smokeTimer -= dt;
            if (_smokeTimer <= 0f)
            {
                _smokeTimer = 0.04f;
                var smoke = new Spark
                {
                    GlobalPosition = GlobalPosition - Velocity.Normalized() * 8f,
                    Velocity = -Velocity.Normalized() * (float)GD.RandRange(20f, 60f),
                    Life = 0.25f
                };
                GetParent()?.AddChild(smoke);
            }

            var spaceState = GetWorld2D().DirectSpaceState;
            var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, nextPos);
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            if (Shooter != null)
            {
                query.Exclude = new Godot.Collections.Array<Rid> { ((CollisionObject2D)Shooter).GetRid() };
            }

            var hit = spaceState.IntersectRay(query);
            if (hit.Count > 0)
            {
                var hitPoint = (Vector2)hit["position"];
                Detonate(hitPoint);
                return;
            }

            GlobalPosition = nextPos;
            QueueRedraw();

            if (_aliveTime > 4.0f)
            {
                Detonate(GlobalPosition);
            }
        }

        private void Detonate(Vector2 point)
        {
            ParticleFactory.Instance?.SpawnExplosion(point, BlastRadius);

            // Blast damage in radius
            var spaceState = GetWorld2D().DirectSpaceState;
            var shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = new CircleShape2D { Radius = BlastRadius },
                Transform = new Transform2D(0f, point),
                CollideWithAreas = true,
                CollideWithBodies = true
            };

            var results = spaceState.IntersectShape(shapeQuery, 32);
            foreach (var r in results)
            {
                var collider = (Node)r["collider"].AsGodotObject();
                if (collider is IDamageable target && !target.IsDead)
                {
                    float dist = point.DistanceTo(target.GlobalPosition);
                    float falloff = Mathf.Clamp(1.0f - (dist / BlastRadius), 0.2f, 1.0f);
                    float finalDmg = (dist < 40f ? DirectDamage : SplashDamage * falloff);

                    // Reduced damage to self / teammates, full damage to enemies
                    if (target.Team == Team)
                    {
                        finalDmg *= 0.35f; // Friendly blast reduction
                    }

                    target.TakeDamage(finalDmg, Shooter, point, (target.GlobalPosition - point).Normalized(), DamageType.RocketExplosive);

                    if (Shooter is Soldier s && s.IsPlayerControlled && target.Team != Team)
                    {
                        AudioManager.Instance?.PlaySound("hitmarker", 0f, 0.9f);
                    }
                }
            }

            QueueFree();
        }

        public override void _Draw()
        {
            // Rocket body
            Vector2 dir = Velocity.Normalized();
            float angle = dir.Angle();
            DrawSetTransform(Vector2.Zero, angle);

            // Warhead & body
            DrawRect(new Rect2(-10, -3, 16, 6), new Color(0.28f, 0.35f, 0.24f));
            DrawColoredPolygon(new[] { new Vector2(6, -3), new Vector2(13, 0), new Vector2(6, 3) }, new Color(0.2f, 0.25f, 0.18f));

            // Fins
            DrawLine(new Vector2(-10, -5), new Vector2(-6, -3), new Color(0.15f, 0.15f, 0.15f), 2f);
            DrawLine(new Vector2(-10, 5), new Vector2(-6, 3), new Color(0.15f, 0.15f, 0.15f), 2f);

            // Jet flame
            DrawCircle(new Vector2(-11, 0), 4f, new Color(1f, 0.7f, 0.2f, 0.9f));
        }
    }
}
