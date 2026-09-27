using System;
using System.Collections.Generic;
using Godot;
using KingWar.Common;
using KingWar.Entities;

namespace KingWar.Objectives
{
    public partial class ObjectiveManager : Node
    {
        public static ObjectiveManager Instance { get; private set; } = null!;

        public List<CaptureNode> Nodes { get; private set; } = new();
        public int ActiveSectorIndex { get; private set; } = 0;
        public CaptureNode? CurrentActiveNode => (ActiveSectorIndex < Nodes.Count) ? Nodes[ActiveSectorIndex] : null;

        public int AttackerTickets { get; set; } = 150;
        public int InitialTickets { get; private set; } = 150;
        public bool IsGameOver { get; private set; } = false;
        public Team WinningTeam { get; private set; } = Team.None;

        public const float TruceDuration = 10.0f;
        public float TruceTimer { get; private set; } = 10.0f;
        public bool IsTruceActive { get; private set; } = true;

        // Battlefield extents, supplied by the active map. Defaults match the demo strip.
        public float MapWidth { get; private set; } = 5000f;
        public float MapHeight { get; private set; } = 1200f;
        public Vector2 AttackerStagingPosition { get; private set; } = new(200f, 600f);
        public Vector2 DefenderRearPosition { get; private set; } = new(4850f, 600f);

        /// <summary>How far the first sector boundary sits behind the first capture node.</summary>
        public float SectorEdgeMargin { get; set; } = 350f;

        /// <summary>How far the last sector boundary sits in front of the defender rear base.</summary>
        public float SectorRearMargin { get; set; } = 200f;

        public void ConfigureMapExtents(float mapWidth, float mapHeight, Vector2 attackerStaging, Vector2 defenderRear)
        {
            if (mapWidth > 0f) MapWidth = mapWidth;
            if (mapHeight > 0f) MapHeight = mapHeight;
            AttackerStagingPosition = attackerStaging;
            DefenderRearPosition = defenderRear;
        }

        public event Action<CaptureNode>? OnSectorAdvanced;
        public event Action<Team>? OnGameOver;
        public event Action? OnTicketsChanged;
        public event Action? OnTruceStarted;
        public event Action? OnTruceEnded;
        public event Action<float>? OnTruceTimerUpdated;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            AttackerTickets = MatchSettings.AttackerTickets;
            InitialTickets = AttackerTickets;
            IsTruceActive = true;
            TruceTimer = TruceDuration;
            OnTruceStarted?.Invoke();
        }

        public override void _Process(double delta)
        {
            if (IsTruceActive && !IsGameOver)
            {
                TruceTimer -= (float)delta;
                OnTruceTimerUpdated?.Invoke(TruceTimer);

                if (TruceTimer <= 0f)
                {
                    TruceTimer = 0f;
                    IsTruceActive = false;
                    OnTruceEnded?.Invoke();
                    AudioManager.Instance?.PlaySound("horn", 2f);
                }
            }
        }

        /// <summary>
        /// Sector boundaries are derived from the registered capture nodes rather than hard-coded,
        /// so the same logic works for the demo strip and for auto-generated city maps.
        /// </summary>
        public (float Left, float Right) GetSectorBounds(int sectorIndex)
        {
            if (Nodes.Count == 0)
            {
                return (AttackerStagingPosition.X, AttackerStagingPosition.X);
            }

            int index = Mathf.Clamp(sectorIndex, 0, Nodes.Count - 1);

            float left;
            if (index == 0)
            {
                float gap = Nodes[0].GlobalPosition.X - AttackerStagingPosition.X;
                left = AttackerStagingPosition.X + Mathf.Clamp(gap * 0.35f, 0f, SectorEdgeMargin);
            }
            else
            {
                left = (Nodes[index - 1].GlobalPosition.X + Nodes[index].GlobalPosition.X) * 0.5f;
            }

            float right;
            if (index == Nodes.Count - 1)
            {
                float gap = DefenderRearPosition.X - Nodes[index].GlobalPosition.X;
                right = DefenderRearPosition.X - Mathf.Clamp(gap * 0.5f, 0f, SectorRearMargin);
            }
            else
            {
                right = (Nodes[index].GlobalPosition.X + Nodes[index + 1].GlobalPosition.X) * 0.5f;
            }

            if (right <= left) right = left + 1f;
            return (left, right);
        }

        public (float MinX, float MaxX) GetAllowedXRange(Team team)
        {
            var (left, right) = GetSectorBounds(ActiveSectorIndex);
            if (team == Team.Attackers)
            {
                float maxX = IsTruceActive ? left : right;
                return (0f, maxX);
            }
            else
            {
                return (left, MapWidth);
            }
        }

        public (float MinX, float MaxX) GetActiveCombatZoneBounds()
        {
            return GetSectorBounds(ActiveSectorIndex);
        }

        public void RegisterNode(CaptureNode node)
        {
            if (!Nodes.Contains(node))
            {
                Nodes.Add(node);
                Nodes.Sort((a, b) => a.SectorIndex.CompareTo(b.SectorIndex));
                node.OnSectorCaptured += HandleSectorCaptured;
            }
        }

        private void HandleSectorCaptured(CaptureNode capturedNode)
        {
            if (IsGameOver) return;

            // Grant bonus tickets to attackers
            AttackerTickets = Mathf.Min(InitialTickets + 50, AttackerTickets + 35);
            OnTicketsChanged?.Invoke();

            if (ActiveSectorIndex + 1 < Nodes.Count)
            {
                ActiveSectorIndex++;
                Nodes[ActiveSectorIndex].State = SectorState.Active;
                OnSectorAdvanced?.Invoke(Nodes[ActiveSectorIndex]);

                // Initiate 10s truce for defenders to retreat and set up defenses
                IsTruceActive = true;
                TruceTimer = TruceDuration;
                OnTruceStarted?.Invoke();
            }
            else
            {
                // All 3 sectors of the Polish campaign captured! Attackers win!
                TriggerGameOver(Team.Attackers);
            }
        }

        public void OnSoldierKilled(Soldier victim, Node2D? killer)
        {
            if (IsGameOver) return;

            if (victim.Team == Team.Attackers)
            {
                AttackerTickets--;
                OnTicketsChanged?.Invoke();

                if (AttackerTickets <= 0)
                {
                    TriggerGameOver(Team.Defenders);
                }
            }
        }

        private void TriggerGameOver(Team winner)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            WinningTeam = winner;
            OnGameOver?.Invoke(winner);
        }
    }
}
