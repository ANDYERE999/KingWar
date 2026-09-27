using System;
using Godot;
using KingWar.Common;

namespace KingWar.Visuals
{
    public partial class ParticleFactory : Node2D
    {
        public static ParticleFactory Instance { get; private set; } = null!;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public void SpawnBlood(Vector2 pos, Vector2 direction)
        {
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                var p = new BloodDrop
                {
                    GlobalPosition = pos,
                    Velocity = direction.Rotated((float)(GD.Randf() - 0.5f) * 1.2f) * (float)GD.RandRange(80f, 220f),
                    Life = (float)GD.RandRange(0.2f, 0.45f)
                };
                AddChild(p);
            }
        }

        public void SpawnSparks(Vector2 pos, Vector2 normal)
        {
            int count = 5;
            for (int i = 0; i < count; i++)
            {
                var s = new Spark
                {
                    GlobalPosition = pos,
                    Velocity = normal.Rotated((float)(GD.Randf() - 0.5f) * 1.5f) * (float)GD.RandRange(120f, 320f),
                    Life = (float)GD.RandRange(0.1f, 0.25f)
                };
                AddChild(s);
            }
        }

        public void SpawnMuzzleFlash(Vector2 pos, float rot)
        {
            var f = new MuzzleFlash
            {
                GlobalPosition = pos,
                Rotation = rot
            };
            AddChild(f);
        }

        public void SpawnShellCasing(Vector2 pos, Vector2 ejectDir)
        {
            var c = new ShellCasing
            {
                GlobalPosition = pos,
                Velocity = ejectDir.Rotated((float)(GD.Randf() - 0.5f) * 0.5f) * (float)GD.RandRange(90f, 160f)
            };
            AddChild(c);
        }

        public void SpawnExplosion(Vector2 pos, float radius = 120f)
        {
            var exp = new ExplosionEffect(radius)
            {
                GlobalPosition = pos
            };
            AddChild(exp);
        }

        public void SpawnHealEffect(Vector2 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                var h = new HealParticle
                {
                    GlobalPosition = pos + new Vector2((float)GD.RandRange(-16f, 16f), (float)GD.RandRange(-16f, 16f))
                };
                AddChild(h);
            }
        }
    }

    public partial class BloodDrop : Node2D
    {
        public Vector2 Velocity;
        public float Life = 0.3f;
        private float _maxLife;
        private float _size = 3.5f;

        public override void _Ready()
        {
            _maxLife = Life;
            _size = (float)GD.RandRange(2.5f, 4.5f);
        }

        public override void _Process(double delta)
        {
            GlobalPosition += Velocity * (float)delta;
            Velocity *= 0.88f;
            Life -= (float)delta;
            QueueRedraw();
            if (Life <= 0)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            float alpha = Mathf.Clamp(Life / _maxLife, 0f, 1f);
            DrawCircle(Vector2.Zero, _size * alpha, new Color(0.7f, 0.05f, 0.05f, alpha * 0.9f));
        }
    }

    public partial class Spark : Node2D
    {
        public Vector2 Velocity;
        public float Life = 0.2f;
        private float _maxLife;

        public override void _Ready()
        {
            _maxLife = Life;
        }

        public override void _Process(double delta)
        {
            GlobalPosition += Velocity * (float)delta;
            Life -= (float)delta;
            QueueRedraw();
            if (Life <= 0) QueueFree();
        }

        public override void _Draw()
        {
            float alpha = Life / _maxLife;
            DrawLine(Vector2.Zero, -Velocity.Normalized() * 5f, new Color(1.0f, 0.85f, 0.3f, alpha), 1.5f);
        }
    }

    public partial class MuzzleFlash : Node2D
    {
        private float _timer = 0.05f;

        public override void _Process(double delta)
        {
            _timer -= (float)delta;
            QueueRedraw();
            if (_timer <= 0) QueueFree();
        }

        public override void _Draw()
        {
            // Flash polygon
            Vector2[] points = new[]
            {
                new Vector2(0, -3),
                new Vector2(16, -1),
                new Vector2(24, 0),
                new Vector2(16, 1),
                new Vector2(0, 3)
            };
            DrawColoredPolygon(points, new Color(1.0f, 0.9f, 0.4f, 0.9f));
            DrawCircle(Vector2.Zero, 6f, new Color(1f, 1f, 1f, 0.95f));
        }
    }

    public partial class ShellCasing : Node2D
    {
        public Vector2 Velocity;
        private float _rotSpeed = 15f;
        private float _life = 0.35f;

        public override void _Ready()
        {
            _rotSpeed = (float)GD.RandRange(-20f, 20f);
        }

        public override void _Process(double delta)
        {
            GlobalPosition += Velocity * (float)delta;
            Velocity *= 0.85f;
            Rotation += _rotSpeed * (float)delta;
            _life -= (float)delta;
            QueueRedraw();
            if (_life <= 0) QueueFree();
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(-3, -1, 6, 2), new Color(0.9f, 0.75f, 0.2f, Mathf.Clamp(_life / 0.35f, 0f, 1f)));
        }
    }

    public partial class ExplosionEffect : Node2D
    {
        private readonly float _maxRadius;
        private float _progress = 0f;
        private const float Duration = 0.55f;

        public ExplosionEffect(float maxRadius)
        {
            _maxRadius = maxRadius;
        }

        public override void _Ready()
        {
            AudioManager.Instance?.PlaySpatial("explosion", GlobalPosition, 3f);
        }

        public override void _Process(double delta)
        {
            _progress += (float)delta / Duration;
            QueueRedraw();
            if (_progress >= 1.0f)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            float p = _progress;
            float currentRadius = Mathf.Lerp(10f, _maxRadius, Mathf.Sqrt(p));
            float alpha = 1f - p;

            // Outer shockwave ring
            DrawArc(Vector2.Zero, currentRadius, 0f, Mathf.Tau, 32, new Color(1f, 0.8f, 0.3f, alpha * 0.8f), 4f * alpha);

            // Inner fireball
            float fireRadius = currentRadius * 0.7f * (1f - p * 0.5f);
            DrawCircle(Vector2.Zero, fireRadius, new Color(1f, 0.45f, 0.1f, alpha * 0.7f));
            DrawCircle(Vector2.Zero, fireRadius * 0.45f, new Color(1f, 0.95f, 0.6f, alpha * 0.9f));

            // Smoke clouds
            for (int i = 0; i < 6; i++)
            {
                float angle = i * (Mathf.Tau / 6f) + p;
                Vector2 smokePos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (currentRadius * 0.55f);
                DrawCircle(smokePos, 14f * (1f + p), new Color(0.25f, 0.25f, 0.25f, alpha * 0.4f));
            }
        }
    }

    public partial class HealParticle : Node2D
    {
        private float _life = 0.6f;
        private Vector2 _vel;

        public override void _Ready()
        {
            _vel = new Vector2((float)GD.RandRange(-10f, 10f), -40f);
        }

        public override void _Process(double delta)
        {
            GlobalPosition += _vel * (float)delta;
            _life -= (float)delta;
            QueueRedraw();
            if (_life <= 0) QueueFree();
        }

        public override void _Draw()
        {
            float a = _life / 0.6f;
            Color c = new Color(0.2f, 0.95f, 0.4f, a);
            DrawRect(new Rect2(-1.5f, -5f, 3f, 10f), c);
            DrawRect(new Rect2(-5f, -1.5f, 10f, 3f), c);
        }
    }
}
