using Godot;

namespace KingWar.Gadgets
{
    public enum GadgetType
    {
        Medkit = 0,
        RPG = 1,
        AmmoBox = 2,
        FragGrenade = 3,
        DeployableCover = 4
    }

    public class GadgetData
    {
        public string Name { get; set; } = "Gadget";
        public string Description { get; set; } = "";
        public GadgetType Type { get; set; }
        public int MaxCharges { get; set; } = 2;
        public float Cooldown { get; set; } = 15f;
        public Color IconColor { get; set; } = Colors.White;

        public static GadgetData CreateMedkit()
        {
            return new GadgetData
            {
                Name = "战地医药箱",
                Description = "放置一个持续提供范围治疗的急救箱，为附近友军每秒恢复大量生命值。",
                Type = GadgetType.Medkit,
                MaxCharges = 2,
                Cooldown = 12f,
                IconColor = new Color(0.2f, 0.85f, 0.3f)
            };
        }

        public static GadgetData CreateRPG()
        {
            return new GadgetData
            {
                Name = "RPG-7 火箭炮",
                Description = "发射一枚重型反掩体高爆火箭弹，产生巨大爆炸半径与毁灭性冲击波。",
                Type = GadgetType.RPG,
                MaxCharges = 3,
                Cooldown = 4.5f,
                IconColor = new Color(0.95f, 0.5f, 0.15f)
            };
        }

        public static GadgetData CreateAmmoBox()
        {
            return new GadgetData
            {
                Name = "补给弹药箱",
                Description = "放置战术弹药补给箱，持续为周围友军补充主武器弹药及手雷/火箭弹备弹。",
                Type = GadgetType.AmmoBox,
                MaxCharges = 2,
                Cooldown = 15f,
                IconColor = new Color(0.95f, 0.8f, 0.2f)
            };
        }

        public static GadgetData CreateFragGrenade()
        {
            return new GadgetData
            {
                Name = "M67 破片手雷",
                Description = "投掷延时引信高爆手雷，遇墙反弹并在延时后爆破，适合清理室内死角与掩体后目标。",
                Type = GadgetType.FragGrenade,
                MaxCharges = 4,
                Cooldown = 8f,
                IconColor = new Color(0.4f, 0.6f, 0.3f)
            };
        }

        public static GadgetData CreateDeployableCover()
        {
            return new GadgetData
            {
                Name = "便携防弹沙袋掩体",
                Description = "在鼠标指向方向瞬间架设一道高强度防弹沙袋，阻挡直射火力提供战术掩护。",
                Type = GadgetType.DeployableCover,
                MaxCharges = 2,
                Cooldown = 18f,
                IconColor = new Color(0.6f, 0.5f, 0.35f)
            };
        }
    }
}
