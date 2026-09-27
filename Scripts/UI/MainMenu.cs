using System;
using System.Collections.Generic;
using Godot;
using KingWar.Common;
using KingWar.Environment;

namespace KingWar.UI
{
    public partial class MainMenu : Control
    {
        private const string DemoStripLabel = "内置演示条带 (但泽 ➔ 波兹南 ➔ 华沙)";
        private const string CampaignLabel = "★ 波兰战役：但泽 ➔ 波兹南 ➔ 华沙（三城拼接）";

        private OptionButton _teamOption = null!;
        private OptionButton _mapOption = null!;
        private readonly List<(string Label, string Scene, string Mask)> _mapOptions = new();
        private HSlider _aiCountSlider = null!;
        private Label _aiCountLabel = null!;
        private HSlider _ticketSlider = null!;
        private Label _ticketLabel = null!;
        private OptionButton _weaponOption = null!;
        private OptionButton _gadgetOption = null!;
        private Button _startButton = null!;

        public override void _Ready()
        {
            BuildMenuUI();
        }

        private void BuildMenuUI()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);

            // Dark military theme background
            var bg = new ColorRect
            {
                Color = new Color(0.08f, 0.10f, 0.08f, 1f)
            };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);

            // Main Layout Container
            var mainMargin = new MarginContainer();
            mainMargin.SetAnchorsPreset(LayoutPreset.FullRect);
            mainMargin.AddThemeConstantOverride("margin_left", 60);
            mainMargin.AddThemeConstantOverride("margin_right", 60);
            mainMargin.AddThemeConstantOverride("margin_top", 40);
            mainMargin.AddThemeConstantOverride("margin_bottom", 40);
            AddChild(mainMargin);

            var hSplit = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            hSplit.AddThemeConstantOverride("separation", 50);
            mainMargin.AddChild(hSplit);

            // Left Side: Title & Game Settings
            var leftVBox = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
            leftVBox.AddThemeConstantOverride("separation", 16);
            hSplit.AddChild(leftVBox);

            // Title
            var title = new Label { Text = "KINGWAR: 2D BATTLEFIELD" };
            title.AddThemeFontSizeOverride("font_size", 34);
            title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.35f));
            leftVBox.AddChild(title);

            var subtitle = new Label { Text = "战地2D：步兵战术突破战 | 波兰战役 3 座重镇依次攻防" };
            subtitle.AddThemeFontSizeOverride("font_size", 16);
            subtitle.AddThemeColorOverride("font_color", new Color(0.75f, 0.78f, 0.72f));
            leftVBox.AddChild(subtitle);

            var sep1 = new HSeparator();
            leftVBox.AddChild(sep1);

            // 1. Team selection
            var teamRow = new HBoxContainer();
            var teamLbl = new Label { Text = "选择参战阵营:", CustomMinimumSize = new Vector2(160, 0) };
            teamRow.AddChild(teamLbl);
            _teamOption = new OptionButton { CustomMinimumSize = new Vector2(240, 32) };
            _teamOption.AddItem("进攻方 (进攻3座重镇 / 消耗敌军)", 0);
            _teamOption.AddItem("防守方 (驻守阵线 / 歼灭攻方兵力)", 1);
            _teamOption.Selected = 0;
            teamRow.AddChild(_teamOption);
            leftVBox.AddChild(teamRow);

            // 2. Battlefield selection: demo strip, the stitched Polish campaign, or a single city
            var mapRow = new HBoxContainer();
            var mapLbl = new Label { Text = "选择作战地图:", CustomMinimumSize = new Vector2(160, 0) };
            mapRow.AddChild(mapLbl);
            _mapOption = new OptionButton { CustomMinimumSize = new Vector2(240, 32) };

            _mapOptions.Clear();
            _mapOptions.Add((DemoStripLabel, "res://Scenes/Game/MainGame.tscn", ""));
            _mapOption.AddItem(DemoStripLabel, 0);

            _mapOptions.Add((CampaignLabel, "res://Scenes/Game/MainGameCampaign.tscn", ""));
            _mapOption.AddItem(CampaignLabel, _mapOptions.Count - 1);

            int cityCount = 0;
            foreach (var maskPath in MaskBattlefieldMap.FindAvailableMaps())
            {
                string label = $"单城: {MaskBattlefieldMap.DisplayName(maskPath)}";
                _mapOptions.Add((label, "res://Scenes/Game/MainGameMask.tscn", maskPath));
                _mapOption.AddItem(label, _mapOptions.Count - 1);
                cityCount++;
            }

            _mapOption.Selected = 1; // Polish campaign by default
            mapRow.AddChild(_mapOption);
            leftVBox.AddChild(mapRow);

            var mapHint = new Label
            {
                Text = $"※ 拼接战役把 3 座城市串成一条战线（每城 1 个据点），另可选 {cityCount} 张单城地图",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            mapHint.AddThemeFontSizeOverride("font_size", 12);
            mapHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.66f, 0.6f));
            leftVBox.AddChild(mapHint);

            // 3. AI Bot count
            var aiRow = new HBoxContainer();
            var aiLbl = new Label { Text = "战场AI士兵数量:", CustomMinimumSize = new Vector2(160, 0) };
            aiRow.AddChild(aiLbl);
            _aiCountSlider = new HSlider
            {
                MinValue = 6,
                MaxValue = 48,
                Step = 2,
                Value = 24,
                CustomMinimumSize = new Vector2(180, 24)
            };
            _aiCountLabel = new Label { Text = "24 人 (12 vs 12)" };
            _aiCountSlider.ValueChanged += (val) =>
            {
                int c = (int)val;
                _aiCountLabel.Text = $"{c} 人 ({c / 2} vs {c / 2})";
                MatchSettings.TotalAiCount = c;
            };
            aiRow.AddChild(_aiCountSlider);
            aiRow.AddChild(_aiCountLabel);
            leftVBox.AddChild(aiRow);

            // 4. Attacker tickets
            var ticketRow = new HBoxContainer();
            var ticketHeader = new Label { Text = "攻方兵力点数:", CustomMinimumSize = new Vector2(160, 0) };
            ticketRow.AddChild(ticketHeader);
            _ticketSlider = new HSlider
            {
                MinValue = 50,
                MaxValue = 300,
                Step = 25,
                Value = 150,
                CustomMinimumSize = new Vector2(180, 24)
            };
            _ticketLabel = new Label { Text = "150 兵力" };
            _ticketSlider.ValueChanged += (val) =>
            {
                int t = (int)val;
                _ticketLabel.Text = $"{t} 兵力";
                MatchSettings.AttackerTickets = t;
            };
            ticketRow.AddChild(_ticketSlider);
            ticketRow.AddChild(_ticketLabel);
            leftVBox.AddChild(ticketRow);

            // 5. Weapon Selection
            var wepRow = new HBoxContainer();
            var wepLbl = new Label { Text = "选择配发主武器:", CustomMinimumSize = new Vector2(160, 0) };
            wepRow.AddChild(wepLbl);
            _weaponOption = new OptionButton { CustomMinimumSize = new Vector2(280, 32) };
            _weaponOption.AddItem("M4A1 卡宾步枪 (高射速、全自动、均衡)", 0);
            _weaponOption.AddItem("AK-74M 重型突击步枪 (大威力、强掩体穿透)", 1);
            _weaponOption.AddItem("PKM 通用机枪 (100发大容量弹链、强火力压制)", 2);
            _weaponOption.AddItem("SVD 精确射手步枪 (远距离超高杀伤、瞄准高倍视野)", 3);
            _weaponOption.AddItem("MP5 战术冲锋枪 (极速射击、近战突击)", 4);
            _weaponOption.Selected = 0;
            wepRow.AddChild(_weaponOption);
            leftVBox.AddChild(wepRow);

            // 5. Gadget Selection
            var gadRow = new HBoxContainer();
            var gadLbl = new Label { Text = "选择辅助道具:", CustomMinimumSize = new Vector2(160, 0) };
            gadRow.AddChild(gadLbl);
            _gadgetOption = new OptionButton { CustomMinimumSize = new Vector2(280, 32) };
            _gadgetOption.AddItem("战地医药箱 (部署范围急救阵地，持续大量回血)", 0);
            _gadgetOption.AddItem("RPG-7 火箭筒 (发射高爆火箭，摧毁掩体与范围轰杀)", 1);
            _gadgetOption.AddItem("便携防弹沙袋 (架设高耐久掩体抵御弹雨)", 2);
            _gadgetOption.AddItem("补给弹药箱 (部署范围补给站，补充子弹与手雷)", 3);
            _gadgetOption.AddItem("M67 破片手雷 (投掷延时反弹手雷，清理死角)", 4);
            _gadgetOption.Selected = 0;
            gadRow.AddChild(_gadgetOption);
            leftVBox.AddChild(gadRow);

            var sep2 = new HSeparator();
            leftVBox.AddChild(sep2);

            // Start Button
            _startButton = new Button
            {
                Text = "▶ 开始战斗 DEPLOY",
                CustomMinimumSize = new Vector2(260, 52)
            };
            _startButton.AddThemeFontSizeOverride("font_size", 20);
            _startButton.Pressed += OnStartPressed;
            leftVBox.AddChild(_startButton);

            // Right Side: Battlefield Tactical Manual & Controls
            var rightVBox = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
            rightVBox.AddThemeConstantOverride("separation", 12);
            hSplit.AddChild(rightVBox);

            var manualTitle = new Label { Text = "战术作战手册 & 操作指南" };
            manualTitle.AddThemeFontSizeOverride("font_size", 20);
            manualTitle.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 0.4f));
            rightVBox.AddChild(manualTitle);

            var helpBox = new PanelContainer();
            rightVBox.AddChild(helpBox);

            var helpVBox = new VBoxContainer();
            helpVBox.AddThemeConstantOverride("separation", 8);
            helpBox.AddChild(helpVBox);

            string controlsText =
                "【逃离鸭科夫式鼠标射击机制】\n" +
                "• 鼠标位置 = 准星与射击瞄准点\n" +
                "• 视野随鼠标前瞻偏移，洞悉射程远端敌情\n" +
                "• 鼠标右键：战术瞄准(ADS)，大幅收窄弹道散布并拉远镜头\n" +
                "• 鼠标左键：开火 (全自动扫射/点射)\n\n" +
                "【移动与战术战备】\n" +
                "• W / A / S / D：移动\n" +
                "• 左 Shift：战术冲刺 (消耗耐力)\n" +
                "• R 键：战术装填换弹\n" +
                "• G 或 Q 键：使用战术辅助道具 (医疗箱/火箭筒/手雷等)\n" +
                "• 1 / 2 / 3 / 4 / 5 键 或 滚轮：随时切换枪械 (M4/AK/PKM机枪/SVD/MP5)\n" +
                "• E 键：载具交互 (预留坦克/步战车拓展接口)\n" +
                "• Esc 键：战役暂停 / 返回主菜单\n\n" +
                "【波兰战役三阶段重镇依次攻克机制】\n" +
                "1. [A] 但泽 (Danzig) ➔\n" +
                "2. [B] 波兹南 (Poznan) ➔\n" +
                "3. [C] 华沙 (Warsaw)\n\n" +
                "进攻方进入据点光圈推进占领条，完全占领后解锁下一战区并补充兵力！全部占领夺取大捷！";

            var controlsLbl = new Label
            {
                Text = controlsText,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            controlsLbl.AddThemeFontSizeOverride("font_size", 13);
            helpVBox.AddChild(controlsLbl);
        }

        private void OnStartPressed()
        {
            MatchSettings.PlayerTeam = (_teamOption.Selected == 0) ? Team.Attackers : Team.Defenders;
            MatchSettings.TotalAiCount = (int)_aiCountSlider.Value;
            MatchSettings.AttackerTickets = (int)_ticketSlider.Value;
            MatchSettings.SelectedPrimaryWeaponIndex = _weaponOption.Selected;
            MatchSettings.SelectedGadgetIndex = _gadgetOption.Selected;

            int mapIndex = Mathf.Clamp(_mapOption.Selected, 0, _mapOptions.Count - 1);
            var (_, scenePath, maskPath) = _mapOptions[mapIndex];
            MatchSettings.SelectedMapPath = maskPath;

            GetTree().ChangeSceneToFile(scenePath);
        }
    }
}
