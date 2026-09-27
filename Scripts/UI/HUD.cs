using System;
using Godot;
using KingWar.Common;
using KingWar.Entities;
using KingWar.Game;
using KingWar.Objectives;

namespace KingWar.UI
{
    public partial class HUD : CanvasLayer
    {
        private Soldier? _boundSoldier;

        // Top bar
        private Label _ticketsLabel = null!;
        private HBoxContainer _sectorsContainer = null!;
        private ProgressBar _activeSectorProgressBar = null!;
        private Label _activeSectorNameLabel = null!;

        // Bottom left
        private ProgressBar _healthBar = null!;
        private Label _healthLabel = null!;
        private ProgressBar _staminaBar = null!;

        // Bottom right
        private Label _weaponNameLabel = null!;
        private Label _ammoLabel = null!;
        private Label _gadgetLabel = null!;
        private Label _hotkeyHintLabel = null!;

        // Center / Victory Defeat
        private PanelContainer _gameOverPanel = null!;
        private Label _gameOverTitle = null!;
        private Label _gameOverSubtitle = null!;

        // Kill feed (top right below minimap)
        private VBoxContainer _killFeedVBox = null!;

        // Truce phase panel
        private PanelContainer _trucePanel = null!;
        private Label _truceTimerLabel = null!;

        // Respawn countdown & loadout selection panel
        private PanelContainer _respawnPanel = null!;
        private Label _respawnCountdownLabel = null!;
        private readonly Button[] _wepButtons = new Button[5];
        private readonly Button[] _gadgetButtons = new Button[5];

        public override void _Ready()
        {
            BuildUI();

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnTicketsChanged += UpdateTicketsDisplay;
                ObjectiveManager.Instance.OnSectorAdvanced += (node) => UpdateSectorBar();
                ObjectiveManager.Instance.OnGameOver += ShowGameOver;
            }

            UpdateTicketsDisplay();
            UpdateSectorBar();
        }

        public void BindPlayer(Soldier soldier)
        {
            _boundSoldier = soldier;
            soldier.OnHealthChanged += UpdateHealthDisplay;
            soldier.OnAmmoChanged += UpdateAmmoDisplay;
            UpdateHealthDisplay();
            UpdateAmmoDisplay();
        }

        public override void _Process(double delta)
        {
            // Process Truce Phase Display
            if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsTruceActive)
            {
                _trucePanel.Visible = true;
                _truceTimerLabel.Text = $"⏸ 战术停战撤退阶段: {ObjectiveManager.Instance.TruceTimer:0.0}s (守方后撤架设掩体 | 攻方集结待命)";
            }
            else if (_trucePanel != null && _trucePanel.Visible)
            {
                _trucePanel.Visible = false;
            }

            // Process Respawn Countdown Display
            if (SpawnManager.Instance != null && SpawnManager.Instance.IsPlayerRespawning)
            {
                _respawnPanel.Visible = true;
                _respawnCountdownLabel.Text = $"重新部署倒计时: {SpawnManager.Instance.PlayerRespawnTimer:0.0} 秒";
            }
            else if (_respawnPanel != null && _respawnPanel.Visible)
            {
                _respawnPanel.Visible = false;
            }

            // Continuously update active sector progress and tickets
            var activeNode = ObjectiveManager.Instance?.CurrentActiveNode;
            if (activeNode != null)
            {
                // Map -1..1 to 0..100
                float pct = (activeNode.CaptureProgress + 1.0f) * 50f;
                _activeSectorProgressBar.Value = pct;
                _activeSectorNameLabel.Text = $"{activeNode.SectorLetter} - {activeNode.SectorName} (攻方:{activeNode.AttackersInside} 守方:{activeNode.DefendersInside})";
            }
            UpdateTicketsDisplay();

            if (_boundSoldier != null && !_boundSoldier.IsDead)
            {
                if (_boundSoldier.CurrentVehicle is Tank tank)
                {
                    _healthBar.MaxValue = tank.MaxArmor;
                    _healthBar.Value = tank.CurrentArmor;
                    _healthLabel.Text = $"坦克装甲: {(int)tank.CurrentArmor} / {(int)tank.MaxArmor}";
                    _healthLabel.AddThemeColorOverride("font_color", GameColors.AttackerBlue);

                    bool isDriver = (tank.Driver == _boundSoldier);
                    _weaponNameLabel.Text = isDriver ? "主驾驶: 120mm 榴弹主炮" : "副驾驶: 12.7mm 重机枪";
                    _ammoLabel.Text = isDriver
                        ? (tank.CannonCooldownTimer <= 0f ? "● 榴弹炮 [就绪]" : $"○ 装填中: {tank.CannonCooldownTimer:0.0}s")
                        : (tank.Gunner != null ? "● 重机枪 [就绪]" : "○ 重机枪 [需副驾驶]");

                    string driverStr = tank.Driver != null ? (tank.Driver.IsPlayerControlled ? "玩家" : "AI") : "空缺";
                    string gunnerStr = tank.Gunner != null ? (tank.Gunner.IsPlayerControlled ? "玩家" : "AI") : "空缺";
                    _gadgetLabel.Text = $"乘员配置: [主驾:{driverStr}] [副驾:{gunnerStr}]";
                    _hotkeyHintLabel.Text = "[E] 离开坦克 | [鼠标左键] 榴弹炮开火 | [右键/机枪]";
                }
                else
                {
                    _healthBar.MaxValue = _boundSoldier.MaxHealth;
                    _healthBar.Value = _boundSoldier.CurrentHealth;
                    _healthLabel.Text = $"生命值: {(int)_boundSoldier.CurrentHealth} / {(int)_boundSoldier.MaxHealth}";
                    _healthLabel.RemoveThemeColorOverride("font_color");

                    _staminaBar.Value = _boundSoldier.CurrentStamina;
                    UpdateAmmoDisplay();
                }
            }
        }

        private void BuildUI()
        {
            // Root Control
            var root = new Control();
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(root);

            // 1. TOP OBJECTIVE BAR
            var topPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(500, 64)
            };
            topPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            topPanel.Position = new Vector2(-250, 16);
            root.AddChild(topPanel);

            var topVBox = new VBoxContainer();
            topPanel.AddChild(topVBox);

            var topHeader = new HBoxContainer();
            topVBox.AddChild(topHeader);

            _ticketsLabel = new Label
            {
                Text = "攻方兵力: 150",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _ticketsLabel.AddThemeColorOverride("font_color", GameColors.AttackerBlue);
            _ticketsLabel.AddThemeFontSizeOverride("font_size", 18);
            topHeader.AddChild(_ticketsLabel);

            _sectorsContainer = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            topVBox.AddChild(_sectorsContainer);

            _activeSectorProgressBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(400, 12),
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                ShowPercentage = false
            };
            topVBox.AddChild(_activeSectorProgressBar);

            _activeSectorNameLabel = new Label
            {
                Text = "目标 A: 但泽",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _activeSectorNameLabel.AddThemeFontSizeOverride("font_size", 13);
            topVBox.AddChild(_activeSectorNameLabel);

            // 2. BOTTOM LEFT (Health & Stamina)
            var blPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(240, 80)
            };
            blPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            blPanel.Position = new Vector2(24, -104);
            root.AddChild(blPanel);

            var blVBox = new VBoxContainer();
            blPanel.AddChild(blVBox);

            _healthLabel = new Label { Text = "生命值: 100 / 100" };
            _healthLabel.AddThemeFontSizeOverride("font_size", 16);
            blVBox.AddChild(_healthLabel);

            _healthBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(220, 16),
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false
            };
            blVBox.AddChild(_healthBar);

            _staminaBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(220, 8),
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false
            };
            blVBox.AddChild(_staminaBar);

            // 3. BOTTOM RIGHT (Weapons & Gadgets)
            var brPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(320, 100)
            };
            brPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            brPanel.Position = new Vector2(-344, -124);
            root.AddChild(brPanel);

            var brVBox = new VBoxContainer();
            brPanel.AddChild(brVBox);

            _weaponNameLabel = new Label { Text = "主武器: M4A1 卡宾步枪" };
            _weaponNameLabel.AddThemeFontSizeOverride("font_size", 16);
            brVBox.AddChild(_weaponNameLabel);

            _ammoLabel = new Label { Text = "弹药: 30 / 150" };
            _ammoLabel.AddThemeFontSizeOverride("font_size", 22);
            _ammoLabel.AddThemeColorOverride("font_color", GameColors.AmmoGold);
            brVBox.AddChild(_ammoLabel);

            _gadgetLabel = new Label { Text = "[G/Q] 战地医药箱 (充能: 2)" };
            _gadgetLabel.AddThemeFontSizeOverride("font_size", 14);
            _gadgetLabel.AddThemeColorOverride("font_color", GameColors.HealthGreen);
            brVBox.AddChild(_gadgetLabel);

            _hotkeyHintLabel = new Label { Text = "[1]M4  [2]AK  [3]PKM  [4]SVD  [5]MP5 | [R]换弹 | [Shift]冲刺" };
            _hotkeyHintLabel.AddThemeFontSizeOverride("font_size", 11);
            _hotkeyHintLabel.AddThemeColorOverride("font_color", Colors.DarkGray);
            brVBox.AddChild(_hotkeyHintLabel);

            // 4. GAME OVER PANEL
            _gameOverPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(460, 220),
                Visible = false
            };
            _gameOverPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _gameOverPanel.Position = new Vector2(-230, -110);
            root.AddChild(_gameOverPanel);

            var goVBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _gameOverPanel.AddChild(goVBox);

            _gameOverTitle = new Label
            {
                Text = "胜利！",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _gameOverTitle.AddThemeFontSizeOverride("font_size", 36);
            goVBox.AddChild(_gameOverTitle);

            _gameOverSubtitle = new Label
            {
                Text = "所有目标已全部攻克，敌方防线全线瓦解！",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _gameOverSubtitle.AddThemeFontSizeOverride("font_size", 16);
            goVBox.AddChild(_gameOverSubtitle);

            var btnRestart = new Button { Text = "重新开始游戏", CustomMinimumSize = new Vector2(180, 40) };
            btnRestart.Pressed += () => GetTree().ReloadCurrentScene();
            goVBox.AddChild(btnRestart);

            var btnMenu = new Button { Text = "返回主菜单", CustomMinimumSize = new Vector2(180, 36) };
            btnMenu.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
            goVBox.AddChild(btnMenu);

            // 5. KILL FEED (Top right under minimap)
            _killFeedVBox = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(240, 100)
            };
            _killFeedVBox.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _killFeedVBox.Position = new Vector2(-260, 105);
            root.AddChild(_killFeedVBox);

            // 6. RESPAWN & TACTICAL DEPLOYMENT OVERLAY (Centered on screen upon player death)
            _respawnPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(560, 280),
                Visible = false
            };
            _respawnPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _respawnPanel.Position = new Vector2(-280, -140);
            root.AddChild(_respawnPanel);

            var respawnVBox = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            _respawnPanel.AddChild(respawnVBox);

            var skullLabel = new Label
            {
                Text = "☠ 您已阵亡 - 战术重新部署 ☠",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            skullLabel.AddThemeFontSizeOverride("font_size", 22);
            skullLabel.AddThemeColorOverride("font_color", GameColors.DefenderRed);
            respawnVBox.AddChild(skullLabel);

            _respawnCountdownLabel = new Label
            {
                Text = "重新部署倒计时: 5.0 秒",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _respawnCountdownLabel.AddThemeFontSizeOverride("font_size", 20);
            _respawnCountdownLabel.AddThemeColorOverride("font_color", GameColors.ContestedYellow);
            respawnVBox.AddChild(_respawnCountdownLabel);

            // Weapon Selector Row
            var wepHeader = new Label
            {
                Text = "● 下一命配发主武器选择:",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            wepHeader.AddThemeFontSizeOverride("font_size", 13);
            wepHeader.AddThemeColorOverride("font_color", GameColors.AttackerBlue);
            respawnVBox.AddChild(wepHeader);

            var wepHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            respawnVBox.AddChild(wepHBox);

            string[] wepNames = new[] { "M4A1", "AK-74M", "PKM机枪", "SVD狙击", "MP5冲锋" };
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                _wepButtons[i] = new Button
                {
                    Text = wepNames[i],
                    CustomMinimumSize = new Vector2(98, 30)
                };
                _wepButtons[i].Pressed += () =>
                {
                    MatchSettings.SelectedPrimaryWeaponIndex = idx;
                    UpdateRespawnButtonsHighlight();
                };
                wepHBox.AddChild(_wepButtons[i]);
            }

            // Gadget Selector Row
            var gdgHeader = new Label
            {
                Text = "● 下一命战术辅助道具选择:",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            gdgHeader.AddThemeFontSizeOverride("font_size", 13);
            gdgHeader.AddThemeColorOverride("font_color", GameColors.HealthGreen);
            respawnVBox.AddChild(gdgHeader);

            var gdgHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            respawnVBox.AddChild(gdgHBox);

            string[] gdgNames = new[] { "急救箱", "火箭炮", "防弹沙袋", "弹药箱", "破片雷" };
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                _gadgetButtons[i] = new Button
                {
                    Text = gdgNames[i],
                    CustomMinimumSize = new Vector2(98, 30)
                };
                _gadgetButtons[i].Pressed += () =>
                {
                    MatchSettings.SelectedGadgetIndex = idx;
                    UpdateRespawnButtonsHighlight();
                };
                gdgHBox.AddChild(_gadgetButtons[i]);
            }

            UpdateRespawnButtonsHighlight();

            // 7. TRUCE PHASE BANNER (Center top below objective bar)
            _trucePanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(520, 36),
                Visible = false
            };
            _trucePanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _trucePanel.Position = new Vector2(-260, 92);
            root.AddChild(_trucePanel);

            _truceTimerLabel = new Label
            {
                Text = "⏸ 战术停战撤退阶段: 10.0s (守方后撤架设掩体 | 攻方集结待命)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _truceTimerLabel.AddThemeFontSizeOverride("font_size", 14);
            _truceTimerLabel.AddThemeColorOverride("font_color", GameColors.ContestedYellow);
            _trucePanel.AddChild(_truceTimerLabel);
        }

        private void UpdateRespawnButtonsHighlight()
        {
            string[] wepNames = new[] { "M4A1", "AK-74M", "PKM机枪", "SVD狙击", "MP5冲锋" };
            for (int i = 0; i < 5; i++)
            {
                if (_wepButtons[i] != null)
                {
                    bool sel = (i == MatchSettings.SelectedPrimaryWeaponIndex);
                    _wepButtons[i].Text = sel ? $"✓ {wepNames[i]}" : wepNames[i];
                    if (sel)
                    {
                        _wepButtons[i].AddThemeColorOverride("font_color", GameColors.AttackerBlue);
                    }
                    else
                    {
                        _wepButtons[i].RemoveThemeColorOverride("font_color");
                    }
                }
            }

            string[] gdgNames = new[] { "急救箱", "火箭炮", "防弹沙袋", "弹药箱", "破片雷" };
            for (int i = 0; i < 5; i++)
            {
                if (_gadgetButtons[i] != null)
                {
                    bool sel = (i == MatchSettings.SelectedGadgetIndex);
                    _gadgetButtons[i].Text = sel ? $"✓ {gdgNames[i]}" : gdgNames[i];
                    if (sel)
                    {
                        _gadgetButtons[i].AddThemeColorOverride("font_color", GameColors.HealthGreen);
                    }
                    else
                    {
                        _gadgetButtons[i].RemoveThemeColorOverride("font_color");
                    }
                }
            }
        }

        public void AddKillFeedEntry(string killerName, Team killerTeam, string victimName, Team victimTeam, string weaponName)
        {
            if (_killFeedVBox == null) return;

            var entry = new Label
            {
                Text = $"{killerName} [{weaponName}] ➔ {victimName}",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            entry.AddThemeFontSizeOverride("font_size", 12);
            Color killerCol = (killerTeam == Team.Attackers) ? GameColors.AttackerBlue : GameColors.DefenderRed;
            entry.AddThemeColorOverride("font_color", killerCol);

            _killFeedVBox.AddChild(entry);

            // Limit to 5 entries
            if (_killFeedVBox.GetChildCount() > 5)
            {
                _killFeedVBox.GetChild(0).QueueFree();
            }

            // Auto fade out after 4 seconds
            var tween = CreateTween();
            tween.TweenProperty(entry, "modulate:a", 0f, 1.0f).SetDelay(3.0f);
            tween.TweenCallback(Callable.From(() => { if (IsInstanceValid(entry)) entry.QueueFree(); }));
        }

        private void UpdateTicketsDisplay()
        {
            if (ObjectiveManager.Instance != null && _ticketsLabel != null)
            {
                string tankStatus = "";
                if (TankManager.Instance != null)
                {
                    if (TankManager.Instance.IsWaitingForRespawn)
                    {
                        tankStatus = $" | 坦克增援: {(int)TankManager.Instance.RespawnTimer}s";
                    }
                    else if (TankManager.Instance.ActiveTank != null)
                    {
                        tankStatus = " | 坦克: 作战中";
                    }
                }
                _ticketsLabel.Text = $"攻方兵力剩余: {ObjectiveManager.Instance.AttackerTickets}{tankStatus}";
            }
        }

        private void UpdateSectorBar()
        {
            if (_sectorsContainer == null || ObjectiveManager.Instance == null) return;

            foreach (var c in _sectorsContainer.GetChildren())
            {
                c.QueueFree();
            }

            var nodes = ObjectiveManager.Instance.Nodes;
            int activeIdx = ObjectiveManager.Instance.ActiveSectorIndex;

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                var badge = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(36, 32)
                };
                var lbl = new Label
                {
                    Text = n.SectorLetter,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                lbl.AddThemeFontSizeOverride("font_size", 16);

                if (i < activeIdx)
                {
                    lbl.AddThemeColorOverride("font_color", GameColors.AttackerBlue);
                }
                else if (i == activeIdx)
                {
                    lbl.AddThemeColorOverride("font_color", GameColors.ContestedYellow);
                }
                else
                {
                    lbl.AddThemeColorOverride("font_color", Colors.DimGray);
                }

                badge.AddChild(lbl);
                _sectorsContainer.AddChild(badge);

                if (i < nodes.Count - 1)
                {
                    var arrow = new Label { Text = "➔" };
                    arrow.AddThemeColorOverride("font_color", Colors.Gray);
                    _sectorsContainer.AddChild(arrow);
                }
            }
        }

        private void UpdateHealthDisplay()
        {
            if (_boundSoldier == null) return;
            _healthBar.Value = _boundSoldier.CurrentHealth;
            _healthLabel.Text = $"生命值: {(int)_boundSoldier.CurrentHealth} / {(int)_boundSoldier.MaxHealth}";
        }

        private void UpdateAmmoDisplay()
        {
            if (_boundSoldier == null) return;
            _weaponNameLabel.Text = $"主武器: {_boundSoldier.Weapon.Name}";
            _ammoLabel.Text = $"弹药: {_boundSoldier.AmmoInMag} / {_boundSoldier.ReserveAmmo}";
            _gadgetLabel.Text = $"[G/Q] {_boundSoldier.Gadget.Name} (剩余: {_boundSoldier.GadgetCharges})";
        }

        private void ShowGameOver(Team winner)
        {
            _gameOverPanel.Visible = true;
            bool isPlayerWinner = (winner == MatchSettings.PlayerTeam);

            if (isPlayerWinner)
            {
                _gameOverTitle.Text = "★ 战术大捷 VICTORY ★";
                _gameOverTitle.AddThemeColorOverride("font_color", GameColors.AttackerBlue);
                _gameOverSubtitle.Text = (winner == Team.Attackers)
                    ? "但泽、波兹南、华沙 3 座重镇已全部攻克，波兰战役大捷！"
                    : "成功消耗敌方全部攻击兵力，防线坚不可摧！";
            }
            else
            {
                _gameOverTitle.Text = "☠ 战役失利 DEFEAT ☠";
                _gameOverTitle.AddThemeColorOverride("font_color", GameColors.DefenderRed);
                _gameOverSubtitle.Text = (winner == Team.Attackers)
                    ? "敌军突破了最后一道防线，基地被完全占领。"
                    : "进攻部队兵力耗尽，攻势被迫终止。";
            }
        }
    }
}
