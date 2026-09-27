using System;
using System.Collections.Generic;
using Godot;
using KingWar.Common;
using KingWar.Gadgets;
using KingWar.Objectives;

namespace KingWar.Entities
{
    public enum AiState
    {
        AdvanceToObjective,
        HoldPosition,
        EngageEnemy,
        TakeCoverAndHeal
    }

    public partial class AIController : Node2D
    {
        [Export] public Soldier? ControlledSoldier { get; set; }

        private AiState _currentState = AiState.AdvanceToObjective;
        private Soldier? _currentTarget = null;

        private float _decisionTimer = 0f;
        private float _burstTimer = 0f;
        private int _burstShotsRemaining = 0;
        private float _strafeTimer = 0f;
        private Vector2 _strafeDir = Vector2.Zero;
        private Vector2 _patrolOffset = Vector2.Zero;
        private float _patrolAngle = 0f;

        // Anti-Stuck Tracking
        private Vector2 _lastCheckPos = Vector2.Zero;
        private float _stuckCheckTimer = 0f;
        private float _unstuckDuration = 0f;
        private Vector2 _unstuckEscapeDir = Vector2.Zero;

        private const float DetectionRange = 750f;
        private const float MinEngageRange = 180f;

        public override void _Ready()
        {
            _patrolAngle = (float)GD.RandRange(0f, Mathf.Tau);
            _patrolOffset = new Vector2(
                (float)GD.RandRange(-100f, 100f),
                (float)GD.RandRange(-80f, 80f)
            );
        }

        public override void _PhysicsProcess(double delta)
        {
            if (ControlledSoldier == null || ControlledSoldier.IsDead) return;
            if (ControlledSoldier.CurrentVehicle != null) return; // Handled by vehicle crew system

            float dt = (float)delta;

            // Anti-stuck monitor
            UpdateAntiStuckMonitor(dt);

            _decisionTimer -= dt;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = (float)GD.RandRange(0.18f, 0.30f);
                UpdateAiTactics();
            }

            ExecuteCurrentBehavior(dt);
        }

        private void UpdateAntiStuckMonitor(float dt)
        {
            if (ControlledSoldier == null) return;

            if (_unstuckDuration > 0f)
            {
                _unstuckDuration -= dt;
                ControlledSoldier.MoveInDirection(_unstuckEscapeDir);
                return;
            }

            _stuckCheckTimer += dt;
            if (_stuckCheckTimer >= 1.2f)
            {
                _stuckCheckTimer = 0f;
                float distMoved = ControlledSoldier.GlobalPosition.DistanceTo(_lastCheckPos);

                // If moving towards an objective and moved less than 15px over 1.2s -> STUCK!
                if (distMoved < 15f && _currentState != AiState.HoldPosition)
                {
                    // Pick unstuck escape vector: steer towards closest open lane (Y=280, Y=600, or Y=900)
                    float currentY = ControlledSoldier.GlobalPosition.Y;
                    float targetLaneY = (currentY < 450f) ? 280f : ((currentY > 750f) ? 900f : 600f);
                    float yDir = (targetLaneY - currentY > 0) ? 1.0f : -1.0f;

                    // Escape laterally and forward
                    float fwdX = (ControlledSoldier.Team == Team.Attackers) ? 0.6f : -0.6f;
                    _unstuckEscapeDir = new Vector2(fwdX, yDir).Normalized();
                    _unstuckDuration = 1.0f;
                }

                _lastCheckPos = ControlledSoldier.GlobalPosition;
            }
        }

        private void UpdateAiTactics()
        {
            if (ControlledSoldier == null) return;

            // Health check: use medkit if low
            if (ControlledSoldier.CurrentHealth < 45f && ControlledSoldier.GadgetCharges > 0)
            {
                ControlledSoldier.TryUseGadget();
            }

            // Check if friendly tank is nearby and has an open seat
            if (ControlledSoldier.Team == Team.Attackers)
            {
                var tank = Game.TankManager.Instance?.ActiveTank;
                if (tank != null && !tank.IsDead && tank.CanEnter(ControlledSoldier))
                {
                    if (ControlledSoldier.GlobalPosition.DistanceTo(tank.GlobalPosition) < 140f)
                    {
                        tank.TryEnter(ControlledSoldier);
                        return;
                    }
                }
            }

            // Find nearest visible enemy
            _currentTarget = FindNearestEnemy();

            if (_currentTarget != null)
            {
                _currentState = AiState.EngageEnemy;
            }
            else
            {
                var activeNode = ObjectiveManager.Instance?.CurrentActiveNode;
                if (activeNode != null)
                {
                    if (ControlledSoldier.Team == Team.Attackers)
                    {
                        _currentState = AiState.AdvanceToObjective;
                    }
                    else
                    {
                        _currentState = AiState.HoldPosition;
                    }
                }
            }
        }

        private void ExecuteCurrentBehavior(float dt)
        {
            if (ControlledSoldier == null) return;
            if (_unstuckDuration > 0f) return; // Currently performing unstuck maneuver

            var activeNode = ObjectiveManager.Instance?.CurrentActiveNode;
            Vector2 targetObjectivePos = activeNode?.GlobalPosition ?? ControlledSoldier.GlobalPosition;

            // 10s Truce phase behavior: Defenders retreat and deploy covers; Attackers stage behind boundary
            if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsTruceActive)
            {
                if (ControlledSoldier.Team == Team.Defenders)
                {
                    // Fall back to active sector node
                    Vector2 defendPos = targetObjectivePos + new Vector2(50f, 0f);
                    MoveTowardTargetWithClearance(defendPos, dt);
                    ControlledSoldier.AimTargetPosition = targetObjectivePos + Vector2.Left * 120f;

                    // Surviving defenders place sandbag covers facing oncoming attackers!
                    if (ControlledSoldier.Gadget.Type == GadgetType.DeployableCover && ControlledSoldier.GadgetCharges > 0)
                    {
                        if (ControlledSoldier.GlobalPosition.DistanceTo(targetObjectivePos) < 180f)
                        {
                            ControlledSoldier.Rotation = Mathf.Pi; // Face west towards attackers
                            ControlledSoldier.TryUseGadget();
                        }
                    }
                    return;
                }
                else
                {
                    // Attackers stage behind the truce boundary barrier
                    var (minX, maxX) = ObjectiveManager.Instance.GetAllowedXRange(Team.Attackers);
                    if (ControlledSoldier.GlobalPosition.X > maxX - 45f)
                    {
                        ControlledSoldier.MoveInDirection(Vector2.Left);
                    }
                    else if (ControlledSoldier.GlobalPosition.X < maxX - 140f)
                    {
                        ControlledSoldier.MoveInDirection(Vector2.Right);
                    }
                    else
                    {
                        ControlledSoldier.MoveInDirection(Vector2.Zero);
                    }
                    ControlledSoldier.AimTargetPosition = new Vector2(maxX + 100f, 600f);
                    return;
                }
            }

            switch (_currentState)
            {
                case AiState.AdvanceToObjective:
                    _patrolAngle += dt * 0.8f;
                    Vector2 dynamicApproach = targetObjectivePos + new Vector2(Mathf.Cos(_patrolAngle) * 50f, Mathf.Sin(_patrolAngle) * 40f);
                    MoveTowardTargetWithClearance(dynamicApproach, dt);
                    ControlledSoldier.AimTargetPosition = targetObjectivePos;
                    break;

                case AiState.HoldPosition:
                    // Defenders patrol the perimeter around the active node (between 120 and 220px)
                    _patrolAngle += dt * 0.6f;
                    Vector2 holdPoint = targetObjectivePos + new Vector2(Mathf.Cos(_patrolAngle) * 140f, Mathf.Sin(_patrolAngle) * 90f);

                    if (ControlledSoldier.GlobalPosition.DistanceTo(holdPoint) > 50f)
                    {
                        MoveTowardTargetWithClearance(holdPoint, dt);
                    }
                    else
                    {
                        // Slight patrol drift to stay responsive and avoid stationary timeouts
                        Vector2 drift = new Vector2(Mathf.Cos(_patrolAngle * 2f), Mathf.Sin(_patrolAngle * 2f)) * 30f;
                        ControlledSoldier.MoveInDirection(drift.Normalized() * 0.35f);
                    }
                    // Aim towards attacker arrival direction (West)
                    ControlledSoldier.AimTargetPosition = ControlledSoldier.GlobalPosition + new Vector2(-200f, 0f);
                    break;

                case AiState.EngageEnemy:
                    if (_currentTarget == null || _currentTarget.IsDead)
                    {
                        _currentState = AiState.AdvanceToObjective;
                        return;
                    }

                    Vector2 toEnemy = _currentTarget.GlobalPosition - ControlledSoldier.GlobalPosition;
                    float dist = toEnemy.Length();
                    Vector2 leadAim = _currentTarget.GlobalPosition + _currentTarget.Velocity * 0.12f;
                    ControlledSoldier.AimTargetPosition = leadAim;

                    // Combat strafing
                    _strafeTimer -= dt;
                    if (_strafeTimer <= 0f)
                    {
                        _strafeTimer = (float)GD.RandRange(0.6f, 1.4f);
                        _strafeDir = toEnemy.Normalized().Rotated(GD.Randf() > 0.5f ? Mathf.Pi * 0.5f : -Mathf.Pi * 0.5f);
                    }

                    Vector2 combatMove = _strafeDir;
                    if (dist > 450f) combatMove += toEnemy.Normalized() * 0.6f;
                    else if (dist < MinEngageRange) combatMove -= toEnemy.Normalized() * 0.8f;

                    ControlledSoldier.MoveInDirection(combatMove);

                    // Burst fire
                    _burstTimer -= dt;
                    if (_burstTimer <= 0f)
                    {
                        if (_burstShotsRemaining <= 0)
                        {
                            _burstShotsRemaining = (int)GD.RandRange(3, 6);
                            _burstTimer = (float)GD.RandRange(0.2f, 0.45f);
                        }
                        else
                        {
                            _burstShotsRemaining--;
                            ControlledSoldier.TryFire();
                            _burstTimer = ControlledSoldier.Weapon.ShotCooldown + (float)GD.RandRange(0.02f, 0.05f);

                            if (dist > 220f && GD.Randf() < 0.035f)
                            {
                                ControlledSoldier.TryUseGadget();
                            }
                        }
                    }

                    if (ControlledSoldier.AmmoInMag <= 0)
                    {
                        ControlledSoldier.StartReload();
                    }
                    break;
            }
        }

        private void MoveTowardTargetWithClearance(Vector2 targetPos, float dt)
        {
            if (ControlledSoldier == null) return;

            Vector2 directDir = (targetPos - ControlledSoldier.GlobalPosition).Normalized();
            Vector2 finalDir = directDir;

            // Multi-ray whisker clearance: Center (60px), Left 35° (50px), Right 35° (50px)
            var space = GetWorld2D().DirectSpaceState;
            Rid myRid = ControlledSoldier.GetRid();

            float centerDist = CastRayDistance(space, ControlledSoldier.GlobalPosition, directDir, 65f, myRid);
            if (centerDist < 60f)
            {
                Vector2 leftDir = directDir.Rotated(-Mathf.DegToRad(38f));
                Vector2 rightDir = directDir.Rotated(Mathf.DegToRad(38f));

                float leftDist = CastRayDistance(space, ControlledSoldier.GlobalPosition, leftDir, 55f, myRid);
                float rightDist = CastRayDistance(space, ControlledSoldier.GlobalPosition, rightDir, 55f, myRid);

                // Steer towards the more open direction
                if (leftDist > rightDist)
                {
                    finalDir = leftDir;
                }
                else
                {
                    finalDir = rightDir;
                }
            }

            // Road / Lane bias: pull towards Y=600 if traversing long stretches
            float currentY = ControlledSoldier.GlobalPosition.Y;
            if (currentY > 400f && currentY < 800f)
            {
                // Moderate pull towards the open road at Y=600 to prevent brushing against building walls
                float roadPull = (600f - currentY) * 0.0025f;
                finalDir = new Vector2(finalDir.X, finalDir.Y + roadPull).Normalized();
            }

            ControlledSoldier.MoveInDirection(finalDir);
        }

        private static float CastRayDistance(PhysicsDirectSpaceState2D space, Vector2 origin, Vector2 dir, float maxDist, Rid ignoreRid)
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
            if (ControlledSoldier == null) return null;

            Soldier? bestTarget = null;
            float closestDist = DetectionRange;

            var space = GetWorld2D().DirectSpaceState;
            var soldiers = GetTree().GetNodesInGroup("Soldiers");

            foreach (var node in soldiers)
            {
                if (node is Soldier other && other != ControlledSoldier && other.Team != ControlledSoldier.Team && !other.IsDead)
                {
                    float dist = ControlledSoldier.GlobalPosition.DistanceTo(other.GlobalPosition);
                    if (dist < closestDist)
                    {
                        var query = PhysicsRayQueryParameters2D.Create(ControlledSoldier.GlobalPosition, other.GlobalPosition);
                        query.Exclude = new Godot.Collections.Array<Rid> { ControlledSoldier.GetRid(), other.GetRid() };
                        var hit = space.IntersectRay(query);

                        if (hit.Count == 0)
                        {
                            closestDist = dist;
                            bestTarget = other;
                        }
                    }
                }
            }

            return bestTarget;
        }
    }
}
