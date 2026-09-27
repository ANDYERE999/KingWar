using System;
using Godot;
using KingWar.Common;
using KingWar.Environment;
using KingWar.Gadgets;
using KingWar.Objectives;
using KingWar.Visuals;
using KingWar.Weapons;

namespace KingWar.Entities
{
    public partial class Soldier : CharacterBody2D, IDamageable
    {
        [Export] public Team Team { get; set; } = Team.Attackers;
        [Export] public bool IsPlayerControlled { get; set; } = false;
        [Export] public string SoldierName { get; set; } = "Soldier";

        public float MaxHealth { get; set; } = 100f;
        public float CurrentHealth { get; set; } = 100f;
        public bool IsDead => CurrentHealth <= 0f;

        public float MaxStamina { get; set; } = 100f;
        public float CurrentStamina { get; set; } = 100f;

        public float BaseWalkSpeed { get; set; } = 180f;
        public float SprintSpeed { get; set; } = 280f;
        public float AimWalkSpeed { get; set; } = 110f;

        // Weapons & Ammo
        public WeaponData Weapon { get; set; } = WeaponData.CreateM4A1();
        public int AmmoInMag { get; set; } = 30;
        public int ReserveAmmo { get; set; } = 150;
        public bool IsReloading { get; private set; } = false;
        public float ReloadTimer { get; private set; } = 0f;
        public float ShotCooldownTimer { get; private set; } = 0f;

        // Dynamic Spread & Aiming
        public float CurrentSpreadDeg { get; set; } = 1.5f;
        public bool IsAiming { get; set; } = false;
        public bool IsSprinting { get; set; } = false;
        public Vector2 AimTargetPosition { get; set; } = Vector2.Zero;

        // Vehicle interaction
        public IVehicle? CurrentVehicle { get; set; } = null;

        // Gadgets
        public GadgetData Gadget { get; set; } = GadgetData.CreateMedkit();
        public int GadgetCharges { get; set; } = 2;
        public float GadgetCooldownTimer { get; private set; } = 0f;

        // Visual / Animation
        private float _walkCycle = 0f;
        private float _recoilOffset = 0f;
        private float _hitFlashTimer = 0f;

        // Events
        public event Action<Soldier, Node2D?>? OnKilled;
        public event Action? OnHealthChanged;
        public event Action? OnAmmoChanged;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            CurrentStamina = MaxStamina;
            AmmoInMag = Weapon.MagazineCapacity;
            ReserveAmmo = Weapon.MaxReserveAmmo;
            GadgetCharges = Gadget.MaxCharges;

            // Setup collision shape if not present
            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null)
            {
                var shape = new CollisionShape2D
                {
                    Name = "CollisionShape2D",
                    Shape = new CircleShape2D { Radius = 16f }
                };
                AddChild(shape);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;

            // Timers
            if (ShotCooldownTimer > 0f) ShotCooldownTimer -= dt;
            if (GadgetCooldownTimer > 0f) GadgetCooldownTimer -= dt;
            if (_hitFlashTimer > 0f) _hitFlashTimer -= dt;

            // Recoil kickback recovery
            _recoilOffset = Mathf.MoveToward(_recoilOffset, 0f, 40f * dt);

            // Reload logic
            if (IsReloading)
            {
                ReloadTimer -= dt;
                if (ReloadTimer <= 0f)
                {
                    FinishReload();
                }
            }

            // Spread recovery
            float targetSpread = IsAiming ? Weapon.BaseSpreadDeg * 0.45f : Weapon.BaseSpreadDeg;
            if (Velocity.Length() > 20f)
            {
                targetSpread += 2.0f;
            }
            if (IsSprinting)
            {
                targetSpread += 3.5f;
            }

            CurrentSpreadDeg = Mathf.MoveToward(CurrentSpreadDeg, targetSpread, Weapon.SpreadRecoveryRate * dt);
            CurrentSpreadDeg = Mathf.Clamp(CurrentSpreadDeg, 0.1f, Weapon.MaxSpreadDeg);

            // Stamina regen or drain
            if (IsSprinting && Velocity.Length() > 20f)
            {
                CurrentStamina = Mathf.Max(0f, CurrentStamina - 28f * dt);
                if (CurrentStamina <= 0f)
                {
                    IsSprinting = false;
                }
            }
            else
            {
                CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + 22f * dt);
            }

            // Walk bob animation
            if (Velocity.Length() > 20f)
            {
                _walkCycle += Velocity.Length() * 0.045f * dt;
            }

            // Aim rotation
            Vector2 aimDir = (AimTargetPosition - GlobalPosition);
            if (aimDir.LengthSquared() > 1f)
            {
                Rotation = Mathf.LerpAngle(Rotation, aimDir.Angle(), 22f * dt);
            }

            MoveAndSlide();

            // Enforce combat zone boundaries (1/3 overlap and faction safe zone restrictions)
            if (ObjectiveManager.Instance != null && !IsDead)
            {
                var (minX, maxX) = ObjectiveManager.Instance.GetAllowedXRange(Team);
                if (GlobalPosition.X < minX || GlobalPosition.X > maxX)
                {
                    float clampedX = Mathf.Clamp(GlobalPosition.X, minX + 5f, maxX - 5f);
                    GlobalPosition = new Vector2(clampedX, GlobalPosition.Y);
                    Velocity = new Vector2(0f, Velocity.Y);
                }
            }

            QueueRedraw();
        }

        public void MoveInDirection(Vector2 moveInput)
        {
            if (IsDead)
            {
                Velocity = Vector2.Zero;
                return;
            }

            if (moveInput.LengthSquared() > 1f) moveInput = moveInput.Normalized();

            float speed = BaseWalkSpeed;
            if (IsSprinting && CurrentStamina > 0f && !IsAiming && !IsReloading)
            {
                speed = SprintSpeed;
            }
            else if (IsAiming)
            {
                speed = AimWalkSpeed;
            }

            // Terrain drag: wading a river or climbing mountain slopes slows infantry down.
            float terrainMultiplier = BattlefieldBase.Current?.GetSpeedMultiplier(GlobalPosition, isVehicle: false) ?? 1f;
            Velocity = moveInput * speed * terrainMultiplier;
        }

        public bool TryFire()
        {
            if (IsDead || IsReloading || IsSprinting) return false;
            if (ShotCooldownTimer > 0f) return false;

            if (AmmoInMag <= 0)
            {
                StartReload();
                return false;
            }

            // Consume ammo
            AmmoInMag--;
            ShotCooldownTimer = Weapon.ShotCooldown;
            CurrentSpreadDeg = Mathf.Min(Weapon.MaxSpreadDeg, CurrentSpreadDeg + Weapon.SpreadPerShotDeg);
            _recoilOffset = 5f;

            // Audio & spatial sound
            AudioManager.Instance?.PlaySpatial(Weapon.SoundName, GlobalPosition, 1f, (float)GD.RandRange(0.96f, 1.04f));

            // Bullet spawn point (weapon muzzle)
            Vector2 forward = Vector2.Right.Rotated(Rotation);
            Vector2 right = Vector2.Down.Rotated(Rotation);
            Vector2 muzzlePos = GlobalPosition + forward * 30f + right * 7f;

            // Calculate trajectory with spread
            float spreadRad = Mathf.DegToRad((float)GD.RandRange(-CurrentSpreadDeg, CurrentSpreadDeg));
            Vector2 bulletDir = forward.Rotated(spreadRad);

            // Spawn Projectile
            var bullet = new Projectile
            {
                GlobalPosition = muzzlePos,
                Velocity = bulletDir * Weapon.BulletSpeed,
                Damage = Weapon.Damage,
                Speed = Weapon.BulletSpeed,
                MaxRange = Weapon.MaxRange,
                PenetrationPower = Weapon.PenetrationPower,
                Team = Team,
                Shooter = this
            };
            GetParent()?.AddChild(bullet);

            // Muzzle flash & shell casing
            ParticleFactory.Instance?.SpawnMuzzleFlash(muzzlePos, Rotation);
            ParticleFactory.Instance?.SpawnShellCasing(GlobalPosition + forward * 14f + right * 8f, -right + forward * 0.3f);

            OnAmmoChanged?.Invoke();
            return true;
        }

        public bool TryUseGadget()
        {
            if (IsDead || GadgetCharges <= 0 || GadgetCooldownTimer > 0f) return false;

            GadgetCharges--;
            GadgetCooldownTimer = Gadget.Cooldown;

            Vector2 forward = Vector2.Right.Rotated(Rotation);

            switch (Gadget.Type)
            {
                case GadgetType.Medkit:
                    var medkit = new DeployableMedkit
                    {
                        GlobalPosition = GlobalPosition + forward * 28f,
                        Team = Team
                    };
                    GetParent()?.AddChild(medkit);
                    break;

                case GadgetType.RPG:
                    AudioManager.Instance?.PlaySpatial("rocket_fire", GlobalPosition, 2f);
                    var rocket = new RocketProjectile
                    {
                        GlobalPosition = GlobalPosition + forward * 32f,
                        Velocity = forward * 400f,
                        Speed = 400f,
                        Shooter = this,
                        Team = Team
                    };
                    GetParent()?.AddChild(rocket);
                    ParticleFactory.Instance?.SpawnMuzzleFlash(GlobalPosition + forward * 32f, Rotation);
                    break;

                case GadgetType.AmmoBox:
                    var ammoBox = new DeployableAmmoBox
                    {
                        GlobalPosition = GlobalPosition + forward * 28f,
                        Team = Team
                    };
                    GetParent()?.AddChild(ammoBox);
                    break;

                case GadgetType.FragGrenade:
                    var grenade = new FragGrenade
                    {
                        GlobalPosition = GlobalPosition + forward * 24f,
                        Velocity = forward * 550f,
                        Thrower = this,
                        Team = Team
                    };
                    GetParent()?.AddChild(grenade);
                    AudioManager.Instance?.PlaySpatial("deploy", GlobalPosition, -4f);
                    break;

                case GadgetType.DeployableCover:
                    var cover = new DeployableCover
                    {
                        GlobalPosition = GlobalPosition + forward * 45f,
                        Rotation = Rotation + Mathf.Pi * 0.5f,
                        Team = Team
                    };
                    GetParent()?.AddChild(cover);
                    break;
            }

            return true;
        }

        public void StartReload()
        {
            if (IsReloading || AmmoInMag >= Weapon.MagazineCapacity || ReserveAmmo <= 0) return;

            IsReloading = true;
            ReloadTimer = Weapon.ReloadDuration;
            AudioManager.Instance?.PlaySpatial("reload", GlobalPosition, -2f);
        }

        private void FinishReload()
        {
            IsReloading = false;
            int needed = Weapon.MagazineCapacity - AmmoInMag;
            int toLoad = Mathf.Min(needed, ReserveAmmo);
            AmmoInMag += toLoad;
            ReserveAmmo -= toLoad;
            OnAmmoChanged?.Invoke();
        }

        public void ResupplyAmmoAndGadgets()
        {
            bool resupplied = false;
            if (ReserveAmmo < Weapon.MaxReserveAmmo)
            {
                ReserveAmmo = Mathf.Min(Weapon.MaxReserveAmmo, ReserveAmmo + Weapon.MagazineCapacity * 2);
                resupplied = true;
            }
            if (GadgetCharges < Gadget.MaxCharges)
            {
                GadgetCharges = Mathf.Min(Gadget.MaxCharges, GadgetCharges + 1);
                resupplied = true;
            }

            if (resupplied)
            {
                AudioManager.Instance?.PlaySpatial("deploy", GlobalPosition, -6f);
                OnAmmoChanged?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke();
        }

        public void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _hitFlashTimer = 0.08f;
            AudioManager.Instance?.PlaySpatial("hurt", GlobalPosition, -2f, (float)GD.RandRange(0.9f, 1.1f));
            OnHealthChanged?.Invoke();

            if (IsDead)
            {
                Die(attacker);
            }
        }

        private void Die(Node2D? attacker)
        {
            Velocity = Vector2.Zero;
            OnKilled?.Invoke(this, attacker);

            // Spawn death blood stain
            ParticleFactory.Instance?.SpawnBlood(GlobalPosition, Vector2.Right.Rotated((float)GD.RandRange(0, Mathf.Tau)));

            // Disable collision so corpses don't block
            var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (col != null) col.Disabled = true;

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (IsDead)
            {
                // Corpse on ground
                DrawCircle(Vector2.Zero, 12f, new Color(0.18f, 0.18f, 0.18f, 0.75f));
                DrawLine(new Vector2(-10, -6), new Vector2(10, 6), new Color(0.4f, 0.05f, 0.05f, 0.8f), 3f);
                return;
            }

            Color teamColor = (Team == Team.Attackers) ? GameColors.AttackerBlue : GameColors.DefenderRed;
            Color bodyColor = IsPlayerControlled ? new Color(0.12f, 0.22f, 0.38f) : new Color(0.24f, 0.28f, 0.22f); // Player wears navy blue spec-ops uniform
            Color vestColor = IsPlayerControlled ? new Color(0.08f, 0.12f, 0.20f) : new Color(0.16f, 0.18f, 0.15f);
            Color helmetColor = IsPlayerControlled ? Colors.Gold : teamColor; // Player wears a distinctive gold helmet

            if (_hitFlashTimer > 0f)
            {
                teamColor = Colors.White;
                bodyColor = Colors.White;
                vestColor = Colors.White;
                helmetColor = Colors.White;
            }

            // Player tactical identification ring on ground
            if (IsPlayerControlled)
            {
                DrawCircle(Vector2.Zero, 18f, new Color(1.0f, 0.84f, 0.0f, 0.15f));
                DrawArc(Vector2.Zero, 18f, 0, Mathf.Tau, 24, new Color(1.0f, 0.84f, 0.0f, 0.75f), 2.2f);
            }

            // Bob offset
            float bob = Mathf.Sin(_walkCycle) * 1.5f;

            // Hands holding weapon
            Vector2 gunOffset = new Vector2(16f - _recoilOffset, 6f);
            DrawLine(new Vector2(0, -6), gunOffset + new Vector2(-4, -2), bodyColor, 4f);
            DrawLine(new Vector2(0, 6), gunOffset + new Vector2(4, 0), bodyColor, 4f);

            // Weapon sprite
            Color wCol = Weapon.WeaponColor;
            DrawRect(new Rect2(gunOffset.X - 6, gunOffset.Y - 2, 22, 4), wCol);
            DrawRect(new Rect2(gunOffset.X + 8, gunOffset.Y - 1, 10, 2), new Color(0.1f, 0.1f, 0.1f)); // Barrel

            // Shoulders & Torso
            DrawCircle(new Vector2(0, -7 + bob), 6f, bodyColor);
            DrawCircle(new Vector2(0, 7 - bob), 6f, bodyColor);
            DrawRect(new Rect2(-7, -8, 14, 16), vestColor);

            // Helmet / Head
            DrawCircle(new Vector2(2, 0), 8f, helmetColor);
            // Goggles/Visor pointing forward (cyan for player, dark for AI)
            Color visorCol = IsPlayerControlled ? Colors.Cyan : new Color(0.1f, 0.15f, 0.2f);
            DrawLine(new Vector2(7, -3), new Vector2(7, 3), visorCol, 2.5f);

            // Reloading icon indicator above head
            if (IsReloading)
            {
                float reloadPct = 1f - (ReloadTimer / Weapon.ReloadDuration);
                DrawArc(new Vector2(0, -20), 8f, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + Mathf.Tau * reloadPct, 16, Colors.Yellow, 2.5f);
            }

            // Health bar above head (for player or damaged bots)
            if (CurrentHealth < MaxHealth || IsPlayerControlled)
            {
                float hpPct = CurrentHealth / MaxHealth;
                DrawRect(new Rect2(-16, -26, 32, 4), new Color(0.15f, 0.15f, 0.15f, 0.85f));
                Color hpCol = (hpPct > 0.5f) ? GameColors.HealthGreen : (hpPct > 0.25f ? Colors.Orange : Colors.Red);
                DrawRect(new Rect2(-16, -26, 32 * hpPct, 4), hpCol);
            }

            // Player overhead commander chevron indicator
            if (IsPlayerControlled)
            {
                Vector2[] chevron = new Vector2[]
                {
                    new Vector2(-6, -35),
                    new Vector2(6, -35),
                    new Vector2(0, -29)
                };
                DrawColoredPolygon(chevron, Colors.Gold);
            }
        }
    }
}
