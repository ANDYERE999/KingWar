using System;
using Godot;
using KingWar.Common;
using KingWar.Environment;
using KingWar.Objectives;
using KingWar.Visuals;
using KingWar.Weapons;

namespace KingWar.Entities
{
    public partial class Tank : VehicleBase
    {
        public Soldier? Gunner { get; private set; }

        public const float TankMaxArmor = 1000f;
        public const float InfantryBaseSpeed = 180f;
        public const float TankSpeedRatio = 0.9f;
        public const float TankMoveSpeed = InfantryBaseSpeed * TankSpeedRatio; // 162f

        public float TurretRotation { get; private set; } = 0f;
        public float CannonCooldownTimer { get; private set; } = 0f;
        public float MachineGunCooldownTimer { get; private set; } = 0f;

        private const float CannonCooldown = 1.8f;
        private const float MachineGunCooldown = 0.14f; // ~430 RPM

        // AI Gunning & Driving state
        private float _aiThinkTimer = 0f;
        private float _aiMgBurstTimer = 0f;
        private int _aiMgBurstRemaining = 0;
        private Soldier? _aiTarget = null;
        private Vector2 _tankLastPos = Vector2.Zero;
        private float _tankStuckTimer = 0f;
        private float _tankUnstuckTimer = 0f;
        private Vector2 _tankUnstuckDir = Vector2.Zero;
        private float _tankPatrolPhase = 0f;

        // Visual animation
        private float _treadAnim = 0f;
        private float _recoilKick = 0f;

        public override void _Ready()
        {
            VehicleName = "M1A2 战术主战坦克";
            MaxArmor = TankMaxArmor;
            CurrentArmor = TankMaxArmor;
            Team = Team.Attackers;
            MaxPassengers = 2;

            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null)
            {
                var shape = new CollisionShape2D
                {
                    Shape = new RectangleShape2D { Size = new Vector2(72, 44) }
                };
                AddChild(shape);
            }

            // Vehicles collide with the mountain layer (bit 3) on top of the normal world layer,
            // so ridge lines stop tanks while infantry can still climb through on foot.
            CollisionLayer = 1;
            CollisionMask = 1 | BattlefieldBase.MountainCollisionLayer;

            AddToGroup("Tanks");
            AddToGroup("Vehicles");
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (CannonCooldownTimer > 0f) CannonCooldownTimer -= dt;
            if (MachineGunCooldownTimer > 0f) MachineGunCooldownTimer -= dt;
            _recoilKick = Mathf.MoveToward(_recoilKick, 0f, 30f * dt);

            if (IsDead) return;

            // Handle AI actions if Driver or Gunner is AI
            HandleAiCrewActions(dt);

            MoveAndSlide();
            QueueRedraw();
        }

        public override bool CanEnter(Soldier soldier)
        {
            if (IsDead) return false;
            if (soldier.Team != Team) return false; // Friendly only

            // If empty seat available
            if (Driver == null || Gunner == null) return true;

            // If tank is full, but a player wants to enter and Driver is an AI, player can take over!
            if (soldier.IsPlayerControlled && Driver != null && !Driver.IsPlayerControlled)
            {
                return true;
            }

            return false;
        }

        public override bool TryEnter(Soldier soldier)
        {
            if (!CanEnter(soldier)) return false;

            if (soldier.IsPlayerControlled)
            {
                // Player boarding priority: AI driver gives up driver seat
                if (Driver != null && !Driver.IsPlayerControlled)
                {
                    var oldDriver = Driver;

                    // If Gunner was already occupied by an AI, eject that gunner to make room
                    if (Gunner != null && !Gunner.IsPlayerControlled)
                    {
                        var oldGunner = Gunner;
                        TryExit(oldGunner);
                    }

                    // Move former AI driver to Gunner
                    Gunner = oldDriver;
                    Driver = soldier;
                }
                else if (Driver == null)
                {
                    Driver = soldier;
                }
                else if (Gunner == null)
                {
                    Gunner = soldier;
                }
            }
            else
            {
                // AI soldier entering
                if (Driver == null)
                {
                    Driver = soldier;
                }
                else if (Gunner == null)
                {
                    Gunner = soldier;
                }
                else
                {
                    return false;
                }
            }

            soldier.CurrentVehicle = this;
            soldier.Visible = false;
            soldier.ProcessMode = ProcessModeEnum.Disabled;
            return true;
        }

        public override bool TryExit(Soldier soldier)
        {
            if (Driver != soldier && Gunner != soldier) return false;

            if (Driver == soldier)
            {
                Driver = null;
                // If Gunner is an AI, promote AI Gunner to Driver
                if (Gunner != null && !Gunner.IsPlayerControlled)
                {
                    Driver = Gunner;
                    Gunner = null;
                }
            }
            else if (Gunner == soldier)
            {
                Gunner = null;
            }

            soldier.CurrentVehicle = null;
            soldier.Visible = true;
            soldier.ProcessMode = ProcessModeEnum.Inherit;
            soldier.GlobalPosition = GlobalPosition + new Vector2(0, 45).Rotated(Rotation);
            return true;
        }

        public override void Drive(Vector2 inputDirection, Vector2 aimPosition, bool fireMain, bool fireSecondary, double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;

            // Turret rotates towards aim position
            Vector2 toAim = (aimPosition - GlobalPosition);
            if (toAim.LengthSquared() > 4f)
            {
                TurretRotation = Mathf.LerpAngle(TurretRotation, toAim.Angle(), 12f * dt);
            }

            // Chassis rotation & movement
            if (inputDirection.LengthSquared() > 0.01f)
            {
                float targetChassisAngle = inputDirection.Angle();
                Rotation = Mathf.LerpAngle(Rotation, targetChassisAngle, 5f * dt);

                // Terrain drag: rivers slow the tank down. Mountains are blocked outright by
                // the dedicated collision layer this vehicle's mask includes.
                float terrainMultiplier = BattlefieldBase.Current?.GetSpeedMultiplier(GlobalPosition, isVehicle: true) ?? 1f;
                Velocity = Vector2.Right.Rotated(Rotation) * TankMoveSpeed * terrainMultiplier;
                _treadAnim += Velocity.Length() * dt * 0.1f;
            }
            else
            {
                Velocity = Velocity.MoveToward(Vector2.Zero, 400f * dt);
            }

            // Driver fires Main Cannon / Howitzer
            if (fireMain && CannonCooldownTimer <= 0f)
            {
                FireCannon(aimPosition);
            }

            // Gunner fires Heavy Machine Gun (ONLY if Gunner is present!)
            if (Gunner != null && fireSecondary && MachineGunCooldownTimer <= 0f)
            {
                FireHeavyMachineGun(aimPosition);
            }
        }

        public void FireCannon(Vector2 targetPoint)
        {
            if (CannonCooldownTimer > 0f || IsDead) return;

            CannonCooldownTimer = CannonCooldown;
            _recoilKick = 12f;

            AudioManager.Instance?.PlaySpatial("cannon_fire", GlobalPosition, 3f);

            Vector2 turretFwd = Vector2.Right.Rotated(TurretRotation);
            Vector2 muzzlePos = GlobalPosition + turretFwd * 50f;

            var shell = new TankShellProjectile
            {
                GlobalPosition = muzzlePos,
                Velocity = turretFwd * 1600f,
                TargetPoint = targetPoint,
                Shooter = this,
                Team = Team
            };
            GetParent()?.AddChild(shell);

            ParticleFactory.Instance?.SpawnMuzzleFlash(muzzlePos, TurretRotation);
            ParticleFactory.Instance?.SpawnSparks(muzzlePos, turretFwd);
        }

        public void FireHeavyMachineGun(Vector2 targetPoint)
        {
            // Only available when 2nd crew member (Gunner) is onboard!
            if (Gunner == null || MachineGunCooldownTimer > 0f || IsDead) return;

            MachineGunCooldownTimer = MachineGunCooldown;

            AudioManager.Instance?.PlaySpatial("heavy_mg", GlobalPosition, 1f, (float)GD.RandRange(0.95f, 1.05f));

            Vector2 turretFwd = Vector2.Right.Rotated(TurretRotation);
            Vector2 mgMuzzlePos = GlobalPosition + turretFwd * 30f + Vector2.Down.Rotated(TurretRotation) * 10f;

            float spreadRad = Mathf.DegToRad((float)GD.RandRange(-2.5f, 2.5f));
            Vector2 bulletDir = turretFwd.Rotated(spreadRad);

            var bullet = new Projectile
            {
                GlobalPosition = mgMuzzlePos,
                Velocity = bulletDir * 2800f,
                Damage = 32f,
                Speed = 2800f,
                MaxRange = 1600f,
                PenetrationPower = 1,
                Team = Team,
                Shooter = this
            };
            GetParent()?.AddChild(bullet);

            ParticleFactory.Instance?.SpawnMuzzleFlash(mgMuzzlePos, TurretRotation);
            ParticleFactory.Instance?.SpawnShellCasing(GlobalPosition, -turretFwd);
        }

        private void HandleAiCrewActions(float dt)
        {
            _aiThinkTimer -= dt;
            if (_aiThinkTimer <= 0f)
            {
                _aiThinkTimer = 0.25f;
                _aiTarget = FindNearestEnemy();
            }

            // AI Driver driving towards active objective
            if (Driver != null && !Driver.IsPlayerControlled)
            {
                var activeNode = ObjectiveManager.Instance?.CurrentActiveNode;
                Vector2 targetPos = activeNode?.GlobalPosition ?? (GlobalPosition + Vector2.Right * 100f);

                if (_aiTarget != null)
                {
                    // Aim turret at enemy
                    TurretRotation = Mathf.LerpAngle(TurretRotation, (_aiTarget.GlobalPosition - GlobalPosition).Angle(), 8f * dt);

                    // Fire cannon at enemy if in range
                    if (GlobalPosition.DistanceTo(_aiTarget.GlobalPosition) < 700f && CannonCooldownTimer <= 0f)
                    {
                        FireCannon(_aiTarget.GlobalPosition);
                    }
                }
                else
                {
                    TurretRotation = Mathf.LerpAngle(TurretRotation, (targetPos - GlobalPosition).Angle(), 6f * dt);
                }

                // Anti-stuck detection for Tank
                if (_tankUnstuckTimer > 0f)
                {
                    _tankUnstuckTimer -= dt;
                    // Drive in reverse unstuck direction
                    Drive(_tankUnstuckDir, _aiTarget?.GlobalPosition ?? targetPos, false, false, dt);
                }
                else
                {
                    _tankStuckTimer += dt;
                    if (_tankStuckTimer >= 1.2f)
                    {
                        _tankStuckTimer = 0f;
                        float distMoved = GlobalPosition.DistanceTo(_tankLastPos);
                        if (distMoved < 12f)
                        {
                            // Tank stuck! Trigger 0.9s reverse maneuver
                            float currentY = GlobalPosition.Y;
                            float escapeY = (currentY > 600f) ? -0.5f : 0.5f; // steer back towards road center
                            _tankUnstuckDir = new Vector2(-0.8f, escapeY).Normalized();
                            _tankUnstuckTimer = 0.9f;
                        }
                        _tankLastPos = GlobalPosition;
                    }

                    // Driving with whisker obstacle clearance
                    float distToObj = GlobalPosition.DistanceTo(targetPos);
                    if (distToObj > 140f)
                    {
                        Vector2 desiredDir = (targetPos - GlobalPosition).Normalized();
                        Vector2 steerDir = CalculateTankSteerWithWhiskers(desiredDir);
                        Drive(steerDir, _aiTarget?.GlobalPosition ?? targetPos, false, false, dt);
                    }
                    else
                    {
                        // Active perimeter sweep instead of stopping dead:
                        // Tank patrols a dynamic elliptical orbit around the capture point
                        _tankPatrolPhase += dt * 0.6f;
                        Vector2 patrolPoint = targetPos + new Vector2(Mathf.Cos(_tankPatrolPhase) * 110f, Mathf.Sin(_tankPatrolPhase) * 60f);
                        Vector2 sweepDir = (patrolPoint - GlobalPosition).Normalized();
                        Drive(sweepDir * 0.45f, _aiTarget?.GlobalPosition ?? targetPos, false, false, dt);
                    }
                }
            }

            // AI Gunner operating Heavy Machine Gun
            if (Gunner != null && !Gunner.IsPlayerControlled && _aiTarget != null && !_aiTarget.IsDead)
            {
                float dist = GlobalPosition.DistanceTo(_aiTarget.GlobalPosition);
                if (dist < 650f)
                {
                    _aiMgBurstTimer -= dt;
                    if (_aiMgBurstTimer <= 0f)
                    {
                        if (_aiMgBurstRemaining <= 0)
                        {
                            _aiMgBurstRemaining = (int)GD.RandRange(4, 7);
                            _aiMgBurstTimer = (float)GD.RandRange(0.3f, 0.6f);
                        }
                        else
                        {
                            _aiMgBurstRemaining--;
                            FireHeavyMachineGun(_aiTarget.GlobalPosition);
                            _aiMgBurstTimer = MachineGunCooldown + 0.02f;
                        }
                    }
                }
            }
        }

        private Vector2 CalculateTankSteerWithWhiskers(Vector2 desiredDir)
        {
            var space = GetWorld2D().DirectSpaceState;
            Rid myRid = GetRid();

            // Center whisker 95px, Left whisker 35° 80px, Right whisker 35° 80px
            float centerDist = CastWhiskerDist(space, GlobalPosition, desiredDir, 95f, myRid);
            if (centerDist < 90f)
            {
                Vector2 leftDir = desiredDir.Rotated(-Mathf.DegToRad(35f));
                Vector2 rightDir = desiredDir.Rotated(Mathf.DegToRad(35f));

                float leftDist = CastWhiskerDist(space, GlobalPosition, leftDir, 80f, myRid);
                float rightDist = CastWhiskerDist(space, GlobalPosition, rightDir, 80f, myRid);

                return (leftDist > rightDist) ? leftDir : rightDir;
            }

            // Road pull towards center asphalt (Y=600)
            float curY = GlobalPosition.Y;
            if (curY > 440f && curY < 760f)
            {
                float roadBias = (600f - curY) * 0.003f;
                return new Vector2(desiredDir.X, desiredDir.Y + roadBias).Normalized();
            }

            return desiredDir;
        }

        private static float CastWhiskerDist(PhysicsDirectSpaceState2D space, Vector2 origin, Vector2 dir, float maxDist, Rid ignoreRid)
        {
            var ray = PhysicsRayQueryParameters2D.Create(origin, origin + dir.Normalized() * maxDist);
            if (ignoreRid.Id != 0)
            {
                ray.Exclude = new Godot.Collections.Array<Rid> { ignoreRid };
            }
            var hit = space.IntersectRay(ray);
            if (hit.Count > 0)
            {
                Vector2 hitPoint = (Vector2)hit["position"];
                return origin.DistanceTo(hitPoint);
            }
            return maxDist;
        }

        private Soldier? FindNearestEnemy()
        {
            Soldier? bestTarget = null;
            float closestDist = 750f;

            var soldiers = GetTree().GetNodesInGroup("Soldiers");
            foreach (var node in soldiers)
            {
                if (node is Soldier s && s.Team != Team && !s.IsDead)
                {
                    float dist = GlobalPosition.DistanceTo(s.GlobalPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = s;
                    }
                }
            }

            return bestTarget;
        }

        public override void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            // CRITICAL REQUIREMENT: "只能被火箭炮伤害" (Can ONLY be damaged by rocket launchers!)
            if (damageType != DamageType.RocketExplosive)
            {
                // Small arms and grenade deflections
                if (hitPoint.HasValue && hitNormal.HasValue)
                {
                    ParticleFactory.Instance?.SpawnSparks(hitPoint.Value, hitNormal.Value);
                }
                AudioManager.Instance?.PlaySpatial("metal_ricochet", GlobalPosition, -2f);
                return;
            }

            base.TakeDamage(amount, attacker, hitPoint, hitNormal, damageType);

            if (IsDead)
            {
                // Explode tank
                ParticleFactory.Instance?.SpawnExplosion(GlobalPosition, 160f);

                // Eject or kill crew
                if (Driver != null)
                {
                    var d = Driver;
                    TryExit(d);
                    d.TakeDamage(120f, attacker, null, null, DamageType.RocketExplosive);
                }
                if (Gunner != null)
                {
                    var g = Gunner;
                    TryExit(g);
                    g.TakeDamage(120f, attacker, null, null, DamageType.RocketExplosive);
                }

                QueueFree();
            }
        }

        public override void _Draw()
        {
            // Draw Tank Hull (Chassis)
            Color treadColor = new Color(0.15f, 0.15f, 0.15f);
            Color armorColor = new Color(0.24f, 0.38f, 0.26f); // NATO olive tank camo
            Color detailColor = new Color(0.18f, 0.28f, 0.18f);

            if (IsDead)
            {
                armorColor = Colors.DarkSlateGray;
            }

            // Left and right track treads
            DrawRect(new Rect2(-36, -22, 72, 10), treadColor);
            DrawRect(new Rect2(-36, 12, 72, 10), treadColor);

            // Tread wheels & grooves
            float treadOffset = (_treadAnim % 8f);
            for (float x = -32 + treadOffset; x < 32; x += 8f)
            {
                DrawLine(new Vector2(x, -22), new Vector2(x, -12), new Color(0.3f, 0.3f, 0.3f), 1.5f);
                DrawLine(new Vector2(x, 12), new Vector2(x, 22), new Color(0.3f, 0.3f, 0.3f), 1.5f);
            }

            // Main armored chassis body
            DrawRect(new Rect2(-30, -14, 60, 28), armorColor);
            DrawRect(new Rect2(-28, -12, 56, 24), detailColor);

            // Front sloping armor
            DrawColoredPolygon(new[]
            {
                new Vector2(30, -14),
                new Vector2(38, -10),
                new Vector2(38, 10),
                new Vector2(30, 14)
            }, armorColor);

            // Blue team emblem / chevron on rear
            DrawLine(new Vector2(-22, -6), new Vector2(-16, 0), GameColors.AttackerBlue, 3f);
            DrawLine(new Vector2(-16, 0), new Vector2(-22, 6), GameColors.AttackerBlue, 3f);

            // --- TURRET (Rotates independently) ---
            float localTurretAngle = TurretRotation - Rotation;
            Vector2 turretOffset = new Vector2(-2f, 0f);

            Vector2 barrelDir = Vector2.Right.Rotated(localTurretAngle);
            Vector2 barrelNormal = Vector2.Down.Rotated(localTurretAngle);

            // Main Cannon Barrel with recoil kick
            float barrelLen = 42f - _recoilKick;
            Vector2 barrelStart = turretOffset + barrelDir * 10f;
            Vector2 barrelEnd = turretOffset + barrelDir * barrelLen;
            DrawLine(barrelStart, barrelEnd, new Color(0.12f, 0.12f, 0.12f), 5f);
            // Muzzle brake
            DrawLine(barrelEnd, barrelEnd + barrelDir * 4f, new Color(0.2f, 0.2f, 0.2f), 7f);

            // Heavy Machine Gun mount (coaxial / roof mount)
            if (Gunner != null)
            {
                Vector2 mgStart = turretOffset + barrelNormal * 9f;
                Vector2 mgEnd = mgStart + barrelDir * 22f;
                DrawLine(mgStart, mgEnd, new Color(0.18f, 0.18f, 0.22f), 2.5f);
            }

            // Turret body
            DrawCircle(turretOffset, 16f, armorColor);
            DrawCircle(turretOffset, 13f, detailColor);

            // Commander & Gunner hatches
            DrawCircle(turretOffset + new Vector2(-4, -6), 4.5f, new Color(0.15f, 0.15f, 0.15f));
            if (Gunner != null)
            {
                DrawCircle(turretOffset + new Vector2(-4, 6), 4.5f, GameColors.AttackerBlue);
            }
            else
            {
                DrawCircle(turretOffset + new Vector2(-4, 6), 4.5f, new Color(0.15f, 0.15f, 0.15f));
            }

            // Armor Bar & Crew indicator over tank
            float hpPct = CurrentArmor / MaxArmor;
            DrawRect(new Rect2(-30, -32, 60, 5), new Color(0.1f, 0.1f, 0.1f, 0.85f));
            DrawRect(new Rect2(-30, -32, 60 * hpPct, 5), GameColors.AttackerBlue);

            // Crew dots (Seat 1: Driver, Seat 2: Gunner)
            DrawCircle(new Vector2(-8, -38), 3f, Driver != null ? (Driver.IsPlayerControlled ? Colors.Yellow : Colors.White) : Colors.DimGray);
            DrawCircle(new Vector2(8, -38), 3f, Gunner != null ? (Gunner.IsPlayerControlled ? Colors.Yellow : Colors.White) : Colors.DimGray);
        }
    }
}
