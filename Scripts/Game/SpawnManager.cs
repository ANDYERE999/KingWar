using System;
using System.Collections.Generic;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Environment;
using KingWar.Gadgets;
using KingWar.Objectives;
using KingWar.Weapons;

namespace KingWar.Game
{
    public class RespawnRequest
    {
        public Team Team;
        public bool IsPlayer;
        public string Name = "Bot";
        public float Timer;
        public WeaponData? SelectedWeapon;
        public GadgetData? SelectedGadget;
    }

    public partial class SpawnManager : Node
    {
        public static SpawnManager Instance { get; private set; } = null!;

        /// <summary>Distance units are pushed away from a spawn junction, clearing the truce barrier.</summary>
        public const float SpawnJunctionClearance = 70f;

        [Export] public Node2D? EntitiesContainer { get; set; }

        private readonly List<RespawnRequest> _respawnQueue = new();

        public float PlayerRespawnTimer { get; private set; } = 0f;
        public bool IsPlayerRespawning => PlayerRespawnTimer > 0f;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            EntitiesContainer ??= GetParent()?.GetNodeOrNull<Node2D>("Entities");
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (PlayerRespawnTimer > 0f)
            {
                PlayerRespawnTimer = Mathf.Max(0f, PlayerRespawnTimer - dt);
            }

            for (int i = _respawnQueue.Count - 1; i >= 0; i--)
            {
                _respawnQueue[i].Timer -= dt;
                if (_respawnQueue[i].Timer <= 0f)
                {
                    var req = _respawnQueue[i];
                    _respawnQueue.RemoveAt(i);
                    SpawnSoldier(req.Team, req.IsPlayer, req.Name, req.SelectedWeapon, req.SelectedGadget);
                }
            }
        }

        /// <summary>
        /// Spawns are anchored on the junction (midpoint) between the contested objective and the
        /// neighbouring one: attackers come from the junction with the sector behind them,
        /// defenders from the junction with the sector ahead of them.
        /// The junction with the first/last objective falls back to the staging / rear base.
        /// </summary>
        public Vector2 GetSpawnPosition(Team team)
        {
            var objectives = ObjectiveManager.Instance;
            var nodes = objectives?.Nodes;
            int activeIndex = objectives?.ActiveSectorIndex ?? 0;

            Vector2 staging = objectives?.AttackerStagingPosition ?? new Vector2(200f, 600f);
            Vector2 rear = objectives?.DefenderRearPosition ?? new Vector2(4850f, 600f);
            float mapHeight = objectives?.MapHeight ?? 1200f;

            Vector2 anchor = ResolveSpawnJunction(team, nodes, activeIndex, staging, rear);

            // Keep units off the truce barrier that sits exactly on the attacker junction, and give
            // each soldier/passenger its own slot so a spawn wave does not stack on one pixel.
            float lateralOffset = team == Team.Attackers ? -SpawnJunctionClearance : SpawnJunctionClearance;
            float spread = Mathf.Clamp(mapHeight * 0.1f, 60f, 220f);
            Vector2 probe = anchor + new Vector2(
                lateralOffset + (float)GD.RandRange(-spread * 0.3f, spread * 0.3f),
                (float)GD.RandRange(-spread, spread));

            var battlefield = BattlefieldBase.Current;
            return battlefield?.FindSpawnPoint(probe.X, probe.Y) ?? probe;
        }

        private static Vector2 ResolveSpawnJunction(
            Team team, IReadOnlyList<CaptureNode>? nodes, int activeIndex, Vector2 staging, Vector2 rear)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return team == Team.Attackers ? staging : rear;
            }

            int index = Mathf.Clamp(activeIndex, 0, nodes.Count - 1);

            if (team == Team.Attackers)
            {
                return index == 0
                    ? staging
                    : (nodes[index - 1].GlobalPosition + nodes[index].GlobalPosition) * 0.5f;
            }

            return index + 1 < nodes.Count
                ? (nodes[index].GlobalPosition + nodes[index + 1].GlobalPosition) * 0.5f
                : rear;
        }

        public Soldier SpawnSoldier(Team team, bool isPlayer, string name, WeaponData? weapon = null, GadgetData? gadget = null)
        {
            var soldier = new Soldier
            {
                Team = team,
                IsPlayerControlled = isPlayer,
                SoldierName = name,
                GlobalPosition = GetSpawnPosition(team)
            };

            // Assign weapon & gadget
            if (isPlayer)
            {
                int wIdx = MatchSettings.SelectedPrimaryWeaponIndex;
                soldier.Weapon = wIdx switch
                {
                    0 => WeaponData.CreateM4A1(),
                    1 => WeaponData.CreateAK74M(),
                    2 => WeaponData.CreatePKM(),
                    3 => WeaponData.CreateSVD(),
                    _ => WeaponData.CreateMP5()
                };

                int gIdx = MatchSettings.SelectedGadgetIndex;
                soldier.Gadget = gIdx switch
                {
                    0 => GadgetData.CreateMedkit(),
                    1 => GadgetData.CreateRPG(),
                    2 => GadgetData.CreateDeployableCover(),
                    3 => GadgetData.CreateAmmoBox(),
                    _ => GadgetData.CreateFragGrenade()
                };
            }
            else
            {
                if (weapon != null && gadget != null)
                {
                    soldier.Weapon = weapon;
                    soldier.Gadget = gadget;
                }
                else
                {
                    SelectAiLoadout(team, out var aiWep, out var aiGdg);
                    soldier.Weapon = aiWep;
                    soldier.Gadget = aiGdg;
                }
            }

            soldier.AddToGroup("Soldiers");

            soldier.OnKilled += (victim, killer) =>
            {
                ObjectiveManager.Instance?.OnSoldierKilled(victim, killer);

                if (killer is Soldier killerSoldier)
                {
                    GameManager.Instance?.GameHUD?.AddKillFeedEntry(
                        killerSoldier.SoldierName,
                        killerSoldier.Team,
                        victim.SoldierName,
                        victim.Team,
                        killerSoldier.Weapon.Name
                    );
                }

                // Queue respawn if match is still running
                if (ObjectiveManager.Instance != null && !ObjectiveManager.Instance.IsGameOver)
                {
                    if (victim.IsPlayerControlled)
                    {
                        PlayerRespawnTimer = 5.0f;
                    }

                    // AI evaluates and selects loadout during respawn wait period
                    WeaponData? nextWep = null;
                    GadgetData? nextGdg = null;
                    if (!victim.IsPlayerControlled)
                    {
                        SelectAiLoadout(victim.Team, out nextWep, out nextGdg);
                    }

                    _respawnQueue.Add(new RespawnRequest
                    {
                        Team = victim.Team,
                        IsPlayer = victim.IsPlayerControlled,
                        Name = victim.SoldierName,
                        Timer = victim.IsPlayerControlled ? 5.0f : (float)GD.RandRange(6.0f, 9.0f),
                        SelectedWeapon = nextWep,
                        SelectedGadget = nextGdg
                    });
                }
            };

            var container = EntitiesContainer ?? GetTree().CurrentScene;
            container.AddChild(soldier);

            // Add controller
            if (isPlayer)
            {
                GameManager.Instance?.SetupPlayerController(soldier);
            }
            else
            {
                var ai = new AIController { ControlledSoldier = soldier };
                soldier.AddChild(ai);
            }

            return soldier;
        }

        public void SelectAiLoadout(Team team, out WeaponData weapon, out GadgetData gadget)
        {
            var soldiers = GetTree().GetNodesInGroup("Soldiers");
            int livingTeammates = 0;
            int rpgCount = 0;
            int coverCount = 0;
            int mgCount = 0;

            foreach (var node in soldiers)
            {
                if (node is Soldier s && s.Team == team && !s.IsDead)
                {
                    livingTeammates++;
                    if (s.Gadget.Type == GadgetType.RPG) rpgCount++;
                    else if (s.Gadget.Type == GadgetType.DeployableCover) coverCount++;

                    if (s.Weapon.Type == WeaponType.PKM) mgCount++;
                }
            }

            bool enemyTankActive = (TankManager.Instance?.ActiveTank != null && !TankManager.Instance.ActiveTank.IsDead);

            if (team == Team.Defenders)
            {
                // === DEFENDER WEAPON SELECTION ===
                // Defenders heavily prioritize Machine Guns (PKM: 45%) and Heavy Rifles (AK-74M: 30%)
                float mgRatio = livingTeammates > 0 ? (float)mgCount / livingTeammates : 0f;
                float wr = GD.Randf();
                if (mgRatio < 0.40f || wr < 0.45f)
                {
                    weapon = WeaponData.CreatePKM(); // PKM Machine Gun for area suppression
                }
                else if (wr < 0.75f)
                {
                    weapon = WeaponData.CreateAK74M(); // Punchy heavy assault rifle
                }
                else if (wr < 0.90f)
                {
                    weapon = WeaponData.CreateSVD(); // Long-range sniper support
                }
                else
                {
                    weapon = WeaponData.CreateM4A1();
                }

                // === DEFENDER GADGET SELECTION ===
                // If enemy tank is on the field, prioritize RPG (榴弹炮/反坦克火箭筒) up to 60%!
                float rpgRatio = livingTeammates > 0 ? (float)rpgCount / livingTeammates : 0f;
                float coverRatio = livingTeammates > 0 ? (float)coverCount / livingTeammates : 0f;

                if (enemyTankActive && rpgRatio < 0.60f)
                {
                    gadget = GadgetData.CreateRPG();
                }
                else if (coverRatio < 0.35f)
                {
                    gadget = GadgetData.CreateDeployableCover(); // Sandbag covers for defense
                }
                else
                {
                    float gr = GD.Randf();
                    if (gr < 0.40f) gadget = GadgetData.CreateMedkit();
                    else if (gr < 0.70f) gadget = GadgetData.CreateAmmoBox();
                    else gadget = GadgetData.CreateFragGrenade();
                }
            }
            else
            {
                // === ATTACKER WEAPON SELECTION ===
                // Attackers prioritize mobile assault rifles (M4A1: 50%, AK-74M: 30%, MP5: 15%)
                float wr = GD.Randf();
                if (wr < 0.50f)
                {
                    weapon = WeaponData.CreateM4A1(); // Standard mobile carbine
                }
                else if (wr < 0.80f)
                {
                    weapon = WeaponData.CreateAK74M();
                }
                else if (wr < 0.92f)
                {
                    weapon = WeaponData.CreateMP5(); // Fast breaching SMG
                }
                else
                {
                    weapon = WeaponData.CreatePKM();
                }

                // === ATTACKER GADGET SELECTION ===
                // Attackers prioritize Medkits and Frag Grenades for aggressive room clearing
                float gr = GD.Randf();
                if (gr < 0.40f) gadget = GadgetData.CreateMedkit();
                else if (gr < 0.70f) gadget = GadgetData.CreateFragGrenade();
                else if (gr < 0.85f) gadget = GadgetData.CreateRPG(); // Bunker buster
                else gadget = GadgetData.CreateAmmoBox();
            }
        }
    }
}
