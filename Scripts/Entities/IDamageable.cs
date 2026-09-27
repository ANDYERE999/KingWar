using Godot;
using KingWar.Common;

namespace KingWar.Entities
{
    public enum DamageType
    {
        Bullet = 0,
        RocketExplosive = 1,
        GrenadeShrapnel = 2,
        Environmental = 3
    }

    public interface IDamageable
    {
        Team Team { get; }
        Vector2 GlobalPosition { get; }
        bool IsDead { get; }
        void TakeDamage(float amount, Node2D? attacker = null, Vector2? hitPoint = null, Vector2? hitNormal = null, DamageType damageType = DamageType.Bullet);
    }
}
