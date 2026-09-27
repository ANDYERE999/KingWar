using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Environment;
using KingWar.Objectives;
using KingWar.UI;
using KingWar.Visuals;

namespace KingWar.Game
{
    public partial class GameManager : Node2D
    {
        public static GameManager Instance { get; private set; } = null!;

        [Export] public BattlefieldBase? Map { get; set; }
        [Export] public Camera2D? MainCamera { get; set; }
        [Export] public HUD? GameHUD { get; set; }
        [Export] public Crosshair? GameCrosshair { get; set; }

        public PlayerController? PlayerCtrl { get; private set; }
        public Soldier? LocalPlayer { get; private set; }

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            Map ??= GetNodeOrNull<BattlefieldBase>("BattlefieldMap");
            MainCamera ??= GetNodeOrNull<Camera2D>("MainCamera");
            GameHUD ??= GetNodeOrNull<HUD>("GameHUD");
            GameCrosshair ??= GetNodeOrNull<Crosshair>("GameCrosshair");

            // Publish battlefield extents first: sector bounds, spawn points and the truce
            // barrier all follow the active map, so the same code drives any battlefield.
            if (Map != null)
            {
                ObjectiveManager.Instance?.ConfigureMapExtents(
                    Map.MapWidth, Map.MapHeight, Map.AttackerStagingPosition, Map.DefenderRearPosition);

                foreach (var node in Map.CaptureNodes)
                {
                    ObjectiveManager.Instance?.RegisterNode(node);
                }
            }

            // Spawn player
            SpawnLocalPlayer();

            // Spawn AI bots
            SpawnInitialAiBots();
        }

        private void SpawnLocalPlayer()
        {
            LocalPlayer = SpawnManager.Instance?.SpawnSoldier(MatchSettings.PlayerTeam, true, "Player_1");
            if (LocalPlayer != null)
            {
                SetupPlayerController(LocalPlayer);
            }
        }

        public void SetupPlayerController(Soldier soldier)
        {
            LocalPlayer = soldier;

            if (PlayerCtrl == null)
            {
                PlayerCtrl = new PlayerController
                {
                    Camera = MainCamera
                };
                AddChild(PlayerCtrl);
            }

            PlayerCtrl.ControlledSoldier = soldier;

            if (GameCrosshair != null)
            {
                GameCrosshair.TrackedSoldier = soldier;
            }

            if (GameHUD != null)
            {
                GameHUD.BindPlayer(soldier);
            }
        }

        private void SpawnInitialAiBots()
        {
            int totalBots = MatchSettings.TotalAiCount;
            int attackersCount = totalBots / 2;
            int defendersCount = totalBots - attackersCount;

            // If player is on Attackers, decrement 1 attacker bot
            if (MatchSettings.PlayerTeam == Team.Attackers)
            {
                attackersCount = Mathf.Max(1, attackersCount - 1);
            }
            else
            {
                defendersCount = Mathf.Max(1, defendersCount - 1);
            }

            for (int i = 0; i < attackersCount; i++)
            {
                SpawnManager.Instance?.SpawnSoldier(Team.Attackers, false, $"US_Soldier_{i + 1}");
            }

            for (int i = 0; i < defendersCount; i++)
            {
                SpawnManager.Instance?.SpawnSoldier(Team.Defenders, false, $"RU_Soldier_{i + 1}");
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
            {
                if (ek.Keycode == Key.Escape)
                {
                    // Return to main menu or restart
                    GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
                }
            }
        }
    }
}
