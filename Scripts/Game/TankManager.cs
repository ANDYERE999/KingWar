using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Objectives;

namespace KingWar.Game
{
    public partial class TankManager : Node
    {
        public static TankManager Instance { get; private set; } = null!;

        public Tank? ActiveTank { get; set; }
        public float RespawnTimer { get; private set; } = 0f;
        public const float TankRespawnDelay = 60.0f; // 60s after tank destruction
        public bool IsWaitingForRespawn => ActiveTank == null && RespawnTimer > 0f;

        public event Action<Tank>? OnTankSpawned;
        public event Action? OnTankDestroyed;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            // Spawn the initial tank for Attackers at match start
            CallDeferred(nameof(SpawnTank));
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (ActiveTank == null && RespawnTimer > 0f)
            {
                RespawnTimer -= dt;
                if (RespawnTimer <= 0f)
                {
                    RespawnTimer = 0f;
                    SpawnTank();
                }
            }
        }

        public void SpawnTank()
        {
            if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsGameOver) return;

            Vector2 spawnPos = GetLastCapturedNodePosition();

            var tank = new Tank
            {
                GlobalPosition = spawnPos
            };

            tank.OnDestroyed += HandleTankDestroyed;

            var container = SpawnManager.Instance?.EntitiesContainer ?? GetTree().CurrentScene;
            container.AddChild(tank);
            ActiveTank = tank;

            OnTankSpawned?.Invoke(tank);
            AudioManager.Instance?.PlaySound("deploy", 0f, 0.85f);
        }

        private void HandleTankDestroyed()
        {
            ActiveTank = null;
            RespawnTimer = TankRespawnDelay; // 60s countdown begins!
            OnTankDestroyed?.Invoke();
        }

        public Vector2 GetLastCapturedNodePosition()
        {
            int activeIndex = ObjectiveManager.Instance?.ActiveSectorIndex ?? 0;
            var nodes = ObjectiveManager.Instance?.Nodes;

            if (activeIndex == 0 || nodes == null || nodes.Count == 0)
            {
                // No nodes captured yet: spawn at attacker staging base
                return new Vector2(240f, 600f);
            }
            else
            {
                // Spawn at the last captured node (ActiveSectorIndex - 1)
                int lastCapturedIndex = Mathf.Clamp(activeIndex - 1, 0, nodes.Count - 1);
                var lastNode = nodes[lastCapturedIndex];
                return lastNode.GlobalPosition + new Vector2(-50f, (float)GD.RandRange(-40f, 40f));
            }
        }
    }
}
