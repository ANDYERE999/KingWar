using Godot;

namespace KingWar.Common
{
    public static class MatchSettings
    {
        public static int PlayerCount { get; set; } = 1;
        public static Team PlayerTeam { get; set; } = Team.Attackers;
        public static int TotalAiCount { get; set; } = 24; // 12 vs 12
        public static int AttackerTickets { get; set; } = 150;
        public static float AiAccuracy { get; set; } = 0.75f;
        public static int SelectedPrimaryWeaponIndex { get; set; } = 0; // 0=M4A1, 1=AK74M, 2=SVD
        public static int SelectedGadgetIndex { get; set; } = 0; // 0=Medkit, 1=RPG, 2=AmmoBox, 3=Grenade, 4=Cover

        /// <summary>
        /// Full path to the semantic mask of the battlefield to play on, or empty for the
        /// built-in demo strip.</summary>
        public static string SelectedMapPath { get; set; } = "";

        /// <summary>True when the selected battlefield is an asset-pipeline generated map.</summary>
        public static bool UsesGeneratedMap => !string.IsNullOrWhiteSpace(SelectedMapPath);
    }
}
