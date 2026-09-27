using System;
using Godot;
using KingWar.Common;

namespace KingWar.Entities
{
    public interface IVehicle : IDamageable
    {
        string VehicleName { get; }
        int MaxPassengers { get; }
        Soldier? Driver { get; }
        bool CanEnter(Soldier soldier);
        bool TryEnter(Soldier soldier);
        bool TryExit(Soldier soldier);
        void Drive(Vector2 inputDirection, Vector2 aimPosition, bool fireMain, bool fireSecondary, double delta);
    }

    /// <summary>
    /// Base class for future ground vehicles (Tanks, APCs, Jeeps) or stationary turrets.
    /// Infantry can enter/exit via interact key (E).
    /// </summary>
    public abstract partial class VehicleBase : CharacterBody2D, IVehicle
    {
        [Export] public string VehicleName { get; set; } = "Light Tactical Vehicle";
        [Export] public int MaxPassengers { get; set; } = 2;
        [Export] public float MaxArmor { get; set; } = 500f;
        [Export] public float CurrentArmor { get; protected set; } = 500f;
        [Export] public Team Team { get; set; } = Team.None;

        public Soldier? Driver { get; protected set; }
        public bool IsDead => CurrentArmor <= 0f;

        public event Action<float, Node2D?>? OnArmorDamaged;
        public event Action? OnDestroyed;

        public virtual bool CanEnter(Soldier soldier)
        {
            if (IsDead) return false;
            return Driver == null || Driver == soldier;
        }

        public virtual bool TryEnter(Soldier soldier)
        {
            if (!CanEnter(soldier)) return false;
            Driver = soldier;
            soldier.Visible = false;
            soldier.ProcessMode = ProcessModeEnum.Disabled;
            return true;
        }

        public virtual bool TryExit(Soldier soldier)
        {
            if (Driver != soldier) return false;
            Driver = null;
            soldier.Visible = true;
            soldier.ProcessMode = ProcessModeEnum.Inherit;
            soldier.GlobalPosition = GlobalPosition + new Vector2(0, 40).Rotated(Rotation);
            return true;
        }

        public abstract void Drive(Vector2 inputDirection, Vector2 aimPosition, bool fireMain, bool fireSecondary, double delta);

        public virtual void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet)
        {
            if (IsDead) return;
            CurrentArmor = Mathf.Max(0f, CurrentArmor - amount);
            OnArmorDamaged?.Invoke(amount, attacker);
            if (IsDead)
            {
                OnDestroyed?.Invoke();
                if (Driver != null)
                {
                    var d = Driver;
                    TryExit(d);
                    d.TakeDamage(100f, attacker);
                }
            }
        }
    }
}
