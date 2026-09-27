using System;
using System.Collections.Generic;
using Godot;
using KingWar.Common;
using KingWar.Entities;

namespace KingWar.Objectives
{
    public enum SectorState
    {
        Locked,
        Active,
        Captured
    }

    public partial class CaptureNode : Node2D
    {
        [Export] public int SectorIndex { get; set; } = 0; // 0 to 4
        [Export] public string SectorName { get; set; } = "Sector";
        [Export] public string SectorLetter { get; set; } = "A";
        [Export] public float CaptureRadius { get; set; } = 180f;

        public SectorState State { get; set; } = SectorState.Locked;
        public Team CurrentOwner { get; set; } = Team.Defenders;
        public float CaptureProgress { get; set; } = -1.0f; // -1.0 = full Defender, +1.0 = full Attacker
        public bool IsContested { get; private set; } = false;

        public int AttackersInside { get; private set; } = 0;
        public int DefendersInside { get; private set; } = 0;

        public event Action<CaptureNode>? OnSectorCaptured;

        private float _pulseTimer = 0f;

        public override void _Ready()
        {
            if (SectorIndex == 0)
            {
                State = SectorState.Active;
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _pulseTimer += dt * 3f;

            if (State == SectorState.Active)
            {
                CountSoldiersInRadius();
                if (ObjectiveManager.Instance == null || !ObjectiveManager.Instance.IsTruceActive)
                {
                    UpdateCaptureProgress(dt);
                }
            }

            QueueRedraw();
        }

        private void CountSoldiersInRadius()
        {
            AttackersInside = 0;
            DefendersInside = 0;

            var soldiers = GetTree().GetNodesInGroup("Soldiers");
            foreach (var node in soldiers)
            {
                if (node is Soldier s && !s.IsDead)
                {
                    if (GlobalPosition.DistanceTo(s.GlobalPosition) <= CaptureRadius)
                    {
                        if (s.Team == Team.Attackers) AttackersInside++;
                        else if (s.Team == Team.Defenders) DefendersInside++;
                    }
                }
            }

            IsContested = (AttackersInside > 0 && DefendersInside > 0);
        }

        private void UpdateCaptureProgress(float dt)
        {
            float captureSpeed = 0.22f; // Takes ~9 seconds for 1 soldier to cap from 0 to 1

            int diff = AttackersInside - DefendersInside;
            if (diff > 0)
            {
                // Attackers pushing capture towards +1.0
                float rate = captureSpeed * Mathf.Min(diff, 4);
                CaptureProgress = Mathf.MoveToward(CaptureProgress, 1.0f, rate * dt);

                if (CaptureProgress >= 1.0f)
                {
                    CompleteCapture(Team.Attackers);
                }
            }
            else if (diff < 0)
            {
                // Defenders recapturing towards -1.0
                float rate = captureSpeed * Mathf.Min(-diff, 4);
                CaptureProgress = Mathf.MoveToward(CaptureProgress, -1.0f, rate * dt);

                if (CaptureProgress <= -1.0f)
                {
                    CurrentOwner = Team.Defenders;
                }
            }
        }

        private void CompleteCapture(Team capturingTeam)
        {
            CurrentOwner = capturingTeam;
            State = SectorState.Captured;
            AudioManager.Instance?.PlaySound("capture_alarm", 2f);
            OnSectorCaptured?.Invoke(this);
        }

        public override void _Draw()
        {
            Color zoneColor = Colors.Gray;
            if (State == SectorState.Locked)
            {
                zoneColor = new Color(0.4f, 0.4f, 0.4f, 0.3f);
            }
            else if (State == SectorState.Captured)
            {
                zoneColor = GameColors.AttackerBlue;
            }
            else
            {
                if (IsContested) zoneColor = GameColors.ContestedYellow;
                else if (CaptureProgress > 0f) zoneColor = GameColors.AttackerBlue;
                else zoneColor = GameColors.DefenderRed;
            }

            // Capture radius perimeter
            float alpha = (State == SectorState.Active) ? (0.35f + Mathf.Sin(_pulseTimer) * 0.12f) : 0.2f;
            DrawCircle(Vector2.Zero, CaptureRadius, new Color(zoneColor.R, zoneColor.G, zoneColor.B, alpha * 0.25f));
            DrawArc(Vector2.Zero, CaptureRadius, 0f, Mathf.Tau, 48, new Color(zoneColor.R, zoneColor.G, zoneColor.B, alpha), 2.5f);

            // Flagpole base
            DrawCircle(Vector2.Zero, 18f, new Color(0.15f, 0.15f, 0.15f, 0.85f));
            DrawCircle(Vector2.Zero, 14f, zoneColor);

            // Sector badge in center
            DrawCircle(Vector2.Zero, 10f, Colors.White);
        }
    }
}
