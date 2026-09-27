using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Visuals;

namespace KingWar.Weapons
{
    public partial class Projectile : Node2D
    {
        public Team Team { get; set; } = Team.None;
        public Node2D? Shooter { get; set; }
        public float Damage { get; set; } = 25f;
        public float Speed { get; set; } = 2500f;
        public float MaxRange { get; set; } = 1800f;
        public int PenetrationPower { get; set; } = 0;
        public Vector2 Velocity { get; set; }

        private float _distanceTraveled = 0f;
        private Vector2 _prevPos;

        public override void _Ready()
        {
            _prevPos = GlobalPosition;
        }

        public override void _PhysicsProcess(double delta)
        {
            float stepDistance = Speed * (float)delta;
            Vector2 nextPos = GlobalPosition + Velocity.Normalized() * stepDistance;

            if (KingWar.Objectives.ObjectiveManager.Instance != null && KingWar.Objectives.ObjectiveManager.Instance.IsTruceActive)
            {
                var (truceMin, truceMax) = KingWar.Objectives.ObjectiveManager.Instance.GetAllowedXRange(Team);
                if (nextPos.X < truceMin || nextPos.X > truceMax)
                {
                    ParticleFactory.Instance?.SpawnSparks(GlobalPosition, -Velocity.Normalized());
                    QueueFree();
                    return;
                }
            }

            var spaceState = GetWorld2D().DirectSpaceState;
            var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, nextPos);
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            // Exclude shooter
            if (Shooter != null)
            {
                query.Exclude = new Godot.Collections.Array<Rid> { ((CollisionObject2D)Shooter).GetRid() };
            }

            var hit = spaceState.IntersectRay(query);
            if (hit.Count > 0)
            {
                var collider = (Node)hit["collider"].AsGodotObject();
                var hitPoint = (Vector2)hit["position"];
                var hitNormal = (Vector2)hit["normal"];

                bool pierce = false;

                if (collider is IDamageable damageable)
                {
                    if (damageable.Team != Team && !damageable.IsDead)
                    {
                        damageable.TakeDamage(Damage, Shooter, hitPoint, hitNormal, DamageType.Bullet);
                        ParticleFactory.Instance?.SpawnBlood(hitPoint, Velocity.Normalized());

                        // Hitmarker if player fired
                        if (Shooter is Soldier s && s.IsPlayerControlled)
                        {
                            AudioManager.Instance?.PlaySound("hitmarker", -2f, 1.1f);
                        }

                        if (PenetrationPower > 0)
                        {
                            PenetrationPower--;
                            pierce = true;
                            GlobalPosition = hitPoint + Velocity.Normalized() * 8f;
                        }
                        else
                        {
                            QueueFree();
                            return;
                        }
                    }
                    else
                    {
                        // Friendly unit, ignore and pass through
                        GlobalPosition = nextPos;
                        _distanceTraveled += stepDistance;
                        _prevPos = GlobalPosition;
                        QueueRedraw();
                        return;
                    }
                }
                else
                {
                    // Wall or non-damageable obstacle
                    ParticleFactory.Instance?.SpawnSparks(hitPoint, hitNormal);

                    if (PenetrationPower > 0)
                    {
                        PenetrationPower--;
                        pierce = true;
                        GlobalPosition = hitPoint + Velocity.Normalized() * 12f;
                    }
                    else
                    {
                        QueueFree();
                        return;
                    }
                }

                if (!pierce)
                {
                    QueueFree();
                    return;
                }
            }

            GlobalPosition = nextPos;
            _distanceTraveled += stepDistance;
            _prevPos = GlobalPosition;
            QueueRedraw();

            if (_distanceTraveled >= MaxRange)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            // Draw bright bullet tracer line
            Vector2 tail = -Velocity.Normalized() * 24f;
            Color coreColor = new Color(1f, 0.95f, 0.6f, 0.95f);
            Color glowColor = new Color(1f, 0.6f, 0.1f, 0.4f);

            DrawLine(Vector2.Zero, tail, glowColor, 4f);
            DrawLine(Vector2.Zero, tail * 0.7f, coreColor, 1.8f);
        }
    }
}
