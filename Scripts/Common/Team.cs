using Godot;

namespace KingWar.Common
{
    public enum Team
    {
        None = 0,
        Attackers = 1, // Blue / NATO / Player Default
        Defenders = 2  // Red / Coalition / Enemy Default
    }

    public static class GameColors
    {
        public static readonly Color AttackerBlue = new Color(0.2f, 0.6f, 1.0f);
        public static readonly Color DefenderRed = new Color(0.95f, 0.25f, 0.25f);
        public static readonly Color NeutralWhite = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color ContestedYellow = new Color(1.0f, 0.8f, 0.2f);
        public static readonly Color HealthGreen = new Color(0.2f, 0.85f, 0.3f);
        public static readonly Color AmmoGold = new Color(0.9f, 0.75f, 0.2f);
    }
}
