using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Weapons
{
    public partial class TankShellProjectile : Node2D
    {
        public Team Team { get; set; } = Team.Attackers;
        public Node2D? Shooter { get; set; }
        public float DirectDamage { get; set; } = 150f;
        public float SplashDamage { get; set; } = 140f;
        public float BlastRadius { get; set; } = 140f;
        public float Speed { get; set; } = 1600f;
        public Vector2 Velocity { get; set; }
        public Vector2 TargetPoint { get; set; }

        private float _aliveTime = 0f;
        private float _smokeTimer = 0f;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _aliveTime += dt;

            Vector2 step = Velocity.Normalized() * (Speed * dt);
            Vector2 nextPos = GlobalPosition + step;

            // Smoke puff
            _smokeTimer -= dt;
            if (_smokeTimer <= 0f)
            {
                _smokeTimer = 0.035f;
                var smoke = new Spark
                {
                    GlobalPosition = GlobalPosition - Velocity.Normalized() * 10f,
                    Velocity = -Velocity.Normalized() * 30f,
                    Life = 0.2f
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

            if (_aliveTime > 2.5f || (TargetPoint != Vector2.Zero && GlobalPosition.DistanceTo(TargetPoint) < 15f))
            {
                Detonate(GlobalPosition);
            }
        }

        private void Detonate(Vector2 point)
        {
            ParticleFactory.Instance?.SpawnExplosion(point, BlastRadius);

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
                    float falloff = Mathf.Clamp(1.0f - (dist / BlastRadius), 0.25f, 1.0f);
                    float finalDmg = (dist < 45f ? DirectDamage : SplashDamage * falloff);

                    if (target.Team == Team)
                    {
                        finalDmg *= 0.35f; // Friendly blast reduction
                    }

                    target.TakeDamage(finalDmg, Shooter, point, (target.GlobalPosition - point).Normalized(), DamageType.RocketExplosive);

                    if (Shooter is Tank t && t.Driver != null && t.Driver.IsPlayerControlled && target.Team != Team)
                    {
                        AudioManager.Instance?.PlaySound("hitmarker", 0f, 0.95f);
                    }
                }
            }

            QueueFree();
        }

        public override void _Draw()
        {
            Vector2 dir = Velocity.Normalized();
            float angle = dir.Angle();
            DrawSetTransform(Vector2.Zero, angle);

            // Glowing shell tracer
            DrawLine(new Vector2(-16, 0), new Vector2(4, 0), new Color(1f, 0.8f, 0.2f, 0.5f), 5f);
            DrawLine(new Vector2(-12, 0), new Vector2(4, 0), new Color(1f, 0.95f, 0.7f), 2.5f);
        }
    }
}
