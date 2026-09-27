using Godot;

namespace KingWar.Weapons
{
    public enum WeaponType
    {
        M4A1 = 0,
        AK74M = 1,
        PKM = 2,
        SVD = 3,
        MP5 = 4
    }

    public class WeaponData
    {
        public string Name { get; set; } = "Rifle";
        public string Description { get; set; } = "";
        public WeaponType Type { get; set; }
        public float Damage { get; set; } = 25f;
        public float FireRateRpm { get; set; } = 600f; // Rounds per minute
        public int MagazineCapacity { get; set; } = 30;
        public int MaxReserveAmmo { get; set; } = 150;
        public float ReloadDuration { get; set; } = 2.0f;
        public float BaseSpreadDeg { get; set; } = 1.5f;
        public float MaxSpreadDeg { get; set; } = 6.0f;
        public float SpreadRecoveryRate { get; set; } = 12f;
        public float SpreadPerShotDeg { get; set; } = 0.8f;
        public float BulletSpeed { get; set; } = 2500f;
        public float MaxRange { get; set; } = 1800f;
        public bool IsFullAuto { get; set; } = true;
        public float RecoilKick { get; set; } = 1.5f;
        public int PenetrationPower { get; set; } = 0; // 0 = none, 1 = penetrates 1 light wall/obstacle
        public string SoundName { get; set; } = "rifle_m4";
        public Color WeaponColor { get; set; } = new Color(0.2f, 0.2f, 0.2f);
        public float ZoomFactor { get; set; } = 1.0f; // Camera aim zoom multiplier

        public float ShotCooldown => 60f / FireRateRpm;

        public static WeaponData CreateM4A1()
        {
            return new WeaponData
            {
                Name = "M4A1 卡宾步枪",
                Description = "标准突击步枪，全自动射击，射速高，后坐力均衡，适合中近距离交火。",
                Type = WeaponType.M4A1,
                Damage = 26f,
                FireRateRpm = 750f,
                MagazineCapacity = 30,
                MaxReserveAmmo = 180,
                ReloadDuration = 1.8f,
                BaseSpreadDeg = 1.2f,
                MaxSpreadDeg = 5.5f,
                SpreadPerShotDeg = 0.6f,
                BulletSpeed = 2600f,
                MaxRange = 1700f,
                IsFullAuto = true,
                RecoilKick = 1.2f,
                PenetrationPower = 0,
                SoundName = "rifle_m4",
                WeaponColor = new Color(0.22f, 0.24f, 0.26f),
                ZoomFactor = 1.15f
            };
        }

        public static WeaponData CreateAK74M()
        {
            return new WeaponData
            {
                Name = "AK-74M 重型突击步枪",
                Description = "大威力突击步枪，高穿透力与单发杀伤，后坐力稍大，可贯穿轻质掩体。",
                Type = WeaponType.AK74M,
                Damage = 36f,
                FireRateRpm = 580f,
                MagazineCapacity = 30,
                MaxReserveAmmo = 150,
                ReloadDuration = 2.2f,
                BaseSpreadDeg = 1.8f,
                MaxSpreadDeg = 7.0f,
                SpreadPerShotDeg = 0.9f,
                BulletSpeed = 2300f,
                MaxRange = 1800f,
                IsFullAuto = true,
                RecoilKick = 2.4f,
                PenetrationPower = 1, // Can pierce light obstacle
                SoundName = "rifle_ak",
                WeaponColor = new Color(0.35f, 0.25f, 0.18f),
                ZoomFactor = 1.2f
            };
        }

        public static WeaponData CreateSVD()
        {
            return new WeaponData
            {
                Name = "SVD 精确射手步枪 (DMR)",
                Description = "半自动狙击步枪，超高单发伤害与极高弹速，右键瞄准可获得广阔远距离视野。",
                Type = WeaponType.SVD,
                Damage = 78f,
                FireRateRpm = 230f,
                MagazineCapacity = 10,
                MaxReserveAmmo = 50,
                ReloadDuration = 2.6f,
                BaseSpreadDeg = 0.2f,
                MaxSpreadDeg = 2.2f,
                SpreadPerShotDeg = 1.8f,
                BulletSpeed = 3400f,
                MaxRange = 2400f,
                IsFullAuto = false,
                RecoilKick = 4.5f,
                PenetrationPower = 2,
                SoundName = "rifle_svd",
                WeaponColor = new Color(0.18f, 0.18f, 0.2f),
                ZoomFactor = 1.55f // High zoom when aiming
            };
        }

        public static WeaponData CreateMP5()
        {
            return new WeaponData
            {
                Name = "MP5 战术冲锋枪",
                Description = "极高射速与机动性，近距离压制利器，换弹迅速。",
                Type = WeaponType.MP5,
                Damage = 20f,
                FireRateRpm = 880f,
                MagazineCapacity = 30,
                MaxReserveAmmo = 210,
                ReloadDuration = 1.4f,
                BaseSpreadDeg = 2.0f,
                MaxSpreadDeg = 6.0f,
                SpreadPerShotDeg = 0.45f,
                BulletSpeed = 2100f,
                MaxRange = 1300f,
                IsFullAuto = true,
                RecoilKick = 0.9f,
                PenetrationPower = 0,
                SoundName = "smg",
                WeaponColor = new Color(0.15f, 0.15f, 0.18f),
                ZoomFactor = 1.05f
            };
        }

        public static WeaponData CreatePKM()
        {
            return new WeaponData
            {
                Name = "PKM 通用机枪",
                Description = "100发大容量弹链供弹，高持续压制火力，防守架点与阵地战利器。",
                Type = WeaponType.PKM,
                Damage = 32f,
                FireRateRpm = 650f,
                MagazineCapacity = 100,
                MaxReserveAmmo = 300,
                ReloadDuration = 3.6f,
                BaseSpreadDeg = 2.0f,
                MaxSpreadDeg = 7.5f,
                SpreadPerShotDeg = 0.5f,
                BulletSpeed = 2500f,
                MaxRange = 1900f,
                IsFullAuto = true,
                RecoilKick = 2.2f,
                PenetrationPower = 1,
                SoundName = "rifle_ak",
                WeaponColor = new Color(0.24f, 0.28f, 0.22f),
                ZoomFactor = 1.15f
            };
        }
    }
}
