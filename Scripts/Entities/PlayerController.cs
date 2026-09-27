using System;
using Godot;
using KingWar.Common;
using KingWar.Weapons;

namespace KingWar.Entities
{
    public partial class PlayerController : Node2D
    {
        [Export] public Soldier? ControlledSoldier { get; set; }
        [Export] public Camera2D? Camera { get; set; }

        private float _screenShakeIntensity = 0f;
        private Vector2 _shakeOffset = Vector2.Zero;
        private int _weaponIndex = 0;

        private readonly WeaponData[] _arsenal = new[]
        {
            WeaponData.CreateM4A1(),
            WeaponData.CreateAK74M(),
            WeaponData.CreatePKM(),
            WeaponData.CreateSVD(),
            WeaponData.CreateMP5()
        };

        public override void _Ready()
        {
            if (ControlledSoldier != null)
            {
                ControlledSoldier.IsPlayerControlled = true;
                _weaponIndex = Mathf.Clamp(MatchSettings.SelectedPrimaryWeaponIndex, 0, _arsenal.Length - 1);
                ControlledSoldier.Weapon = _arsenal[_weaponIndex];
                ControlledSoldier.AmmoInMag = ControlledSoldier.Weapon.MagazineCapacity;
                ControlledSoldier.ReserveAmmo = ControlledSoldier.Weapon.MaxReserveAmmo;
            }
        }

        public override void _Process(double delta)
        {
            if (ControlledSoldier == null || ControlledSoldier.IsDead) return;

            float dt = (float)delta;
            Vector2 mouseWorld = GetGlobalMousePosition();

            // Vehicle driving mode
            if (ControlledSoldier.CurrentVehicle is Tank tank)
            {
                bool fireLeft = Input.IsMouseButtonPressed(MouseButton.Left);
                bool fireRight = Input.IsMouseButtonPressed(MouseButton.Right);

                bool isDriver = (tank.Driver == ControlledSoldier);
                bool isGunner = (tank.Gunner == ControlledSoldier);

                // Get WASD steering input
                Vector2 tankInput = Vector2.Zero;
                if (Input.IsKeyPressed(Key.W)) tankInput.Y -= 1f;
                if (Input.IsKeyPressed(Key.S)) tankInput.Y += 1f;
                if (Input.IsKeyPressed(Key.A)) tankInput.X -= 1f;
                if (Input.IsKeyPressed(Key.D)) tankInput.X += 1f;

                if (isDriver)
                {
                    tank.Drive(tankInput, mouseWorld, fireLeft, fireRight, dt);
                }
                else if (isGunner)
                {
                    // Gunner operates heavy machine gun
                    if (fireLeft)
                    {
                        tank.FireHeavyMachineGun(mouseWorld);
                    }
                }

                // Camera follow tank
                if (Camera != null)
                {
                    Vector2 toMouse = mouseWorld - tank.GlobalPosition;
                    Vector2 lookahead = toMouse.Normalized() * Mathf.Min(toMouse.Length() * 0.4f, 220f);
                    Vector2 targetCamPos = tank.GlobalPosition + lookahead + _shakeOffset;
                    Camera.GlobalPosition = Camera.GlobalPosition.Lerp(targetCamPos, 14f * dt);
                    Camera.Zoom = Camera.Zoom.Lerp(new Vector2(0.9f, 0.9f), 10f * dt);
                }
                return;
            }

            // Normal Infantry Mode
            ControlledSoldier.AimTargetPosition = mouseWorld;

            // Weapon switching with keys 1, 2, 3, 4, 5
            if (Input.IsKeyPressed(Key.Key1)) SwitchWeapon(0);
            else if (Input.IsKeyPressed(Key.Key2)) SwitchWeapon(1);
            else if (Input.IsKeyPressed(Key.Key3)) SwitchWeapon(2);
            else if (Input.IsKeyPressed(Key.Key4)) SwitchWeapon(3);
            else if (Input.IsKeyPressed(Key.Key5)) SwitchWeapon(4);

            // Right Click ADS / Aiming
            bool isAiming = Input.IsMouseButtonPressed(MouseButton.Right);
            ControlledSoldier.IsAiming = isAiming;

            // Sprint
            ControlledSoldier.IsSprinting = Input.IsKeyPressed(Key.Shift);

            // Firing: Left mouse button
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                if (ControlledSoldier.Weapon.IsFullAuto)
                {
                    if (ControlledSoldier.TryFire())
                    {
                        AddScreenShake(ControlledSoldier.Weapon.RecoilKick * 1.5f);
                    }
                }
            }

            // Screen shake dampening
            if (_screenShakeIntensity > 0f)
            {
                _screenShakeIntensity = Mathf.MoveToward(_screenShakeIntensity, 0f, 18f * dt);
                _shakeOffset = new Vector2(
                    (float)GD.RandRange(-_screenShakeIntensity, _screenShakeIntensity),
                    (float)GD.RandRange(-_screenShakeIntensity, _screenShakeIntensity)
                );
            }
            else
            {
                _shakeOffset = Vector2.Zero;
            }

            // Camera follow with lookahead towards cursor (Duckov style)
            if (Camera != null)
            {
                Vector2 toMouse = mouseWorld - ControlledSoldier.GlobalPosition;
                float maxLookahead = isAiming ? 360f : 200f;
                Vector2 lookahead = toMouse.Normalized() * Mathf.Min(toMouse.Length() * 0.45f, maxLookahead);

                Vector2 targetCamPos = ControlledSoldier.GlobalPosition + lookahead + _shakeOffset;
                Camera.GlobalPosition = Camera.GlobalPosition.Lerp(targetCamPos, 14f * dt);

                // Dynamic zoom
                float targetZoom = isAiming ? (1.0f / ControlledSoldier.Weapon.ZoomFactor) : 1.0f;
                Camera.Zoom = Camera.Zoom.Lerp(new Vector2(targetZoom, targetZoom), 10f * dt);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (ControlledSoldier == null || ControlledSoldier.IsDead) return;
            if (ControlledSoldier.CurrentVehicle != null) return; // Handled in vehicle

            // WASD Movement
            Vector2 inputDir = Vector2.Zero;
            if (Input.IsKeyPressed(Key.W)) inputDir.Y -= 1f;
            if (Input.IsKeyPressed(Key.S)) inputDir.Y += 1f;
            if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1f;
            if (Input.IsKeyPressed(Key.D)) inputDir.X += 1f;

            ControlledSoldier.MoveInDirection(inputDir);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (ControlledSoldier == null || ControlledSoldier.IsDead) return;

            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left && !ControlledSoldier.Weapon.IsFullAuto)
                {
                    // Semi-auto fire
                    if (ControlledSoldier.TryFire())
                    {
                        AddScreenShake(ControlledSoldier.Weapon.RecoilKick * 2.0f);
                    }
                }
                else if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    SwitchWeapon((_weaponIndex + 1) % _arsenal.Length);
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    SwitchWeapon((_weaponIndex - 1 + _arsenal.Length) % _arsenal.Length);
                }
            }
            else if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
            {
                if (ek.Keycode == Key.R)
                {
                    ControlledSoldier.StartReload();
                }
                else if (ek.Keycode == Key.G || ek.Keycode == Key.Q)
                {
                    ControlledSoldier.TryUseGadget();
                }
                else if (ek.Keycode == Key.E)
                {
                    // Try vehicle interaction
                    TryInteractWithVehicle();
                }
            }
        }

        private void SwitchWeapon(int index)
        {
            if (ControlledSoldier == null || index == _weaponIndex) return;
            if (ControlledSoldier.IsReloading) return;

            _weaponIndex = index;
            ControlledSoldier.Weapon = _arsenal[_weaponIndex];
            ControlledSoldier.AmmoInMag = ControlledSoldier.Weapon.MagazineCapacity;
            ControlledSoldier.ReserveAmmo = ControlledSoldier.Weapon.MaxReserveAmmo;
            AudioManager.Instance?.PlaySound("deploy", -3f, 1.2f);
        }

        private void TryInteractWithVehicle()
        {
            if (ControlledSoldier == null) return;

            // If already in a vehicle, exit!
            if (ControlledSoldier.CurrentVehicle != null)
            {
                ControlledSoldier.CurrentVehicle.TryExit(ControlledSoldier);
                return;
            }

            // Search for nearby vehicle
            var space = GetWorld2D().DirectSpaceState;
            var shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = new CircleShape2D { Radius = 90f },
                Transform = new Transform2D(0f, ControlledSoldier.GlobalPosition),
                CollideWithBodies = true
            };

            var hits = space.IntersectShape(shapeQuery);
            foreach (var h in hits)
            {
                var col = (Node)h["collider"].AsGodotObject();
                if (col is IVehicle vehicle && vehicle.CanEnter(ControlledSoldier))
                {
                    vehicle.TryEnter(ControlledSoldier);
                    return;
                }
            }
        }

        public void AddScreenShake(float intensity)
        {
            _screenShakeIntensity = Mathf.Min(25f, _screenShakeIntensity + intensity);
        }
    }
}
