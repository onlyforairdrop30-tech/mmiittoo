using UnityEngine;

namespace ProjectInfinity.Core
{
    /// <summary>Graphics tiers the Settings screen and boot query choose between.</summary>
    public enum QualityTier { Low, Medium, High, Ultra }

    /// <summary>Global tuning constants. Gameplay numbers live here, not scattered in components.</summary>
    public static class GameRules
    {
        public const int MaxLevel = 60;

        // Defence windows
        public const float ParryWindow = 0.20f;
        public const float DodgeInvulnerability = 0.36f;
        public const float DodgeDuration = 0.42f;
        public const float DodgeStaminaCost = 22f;
        public const float DodgeCooldown = 0.55f;

        // Resource economy
        public const float StaminaRegenPerSecond = 18f;
        public const float EnergyRegenPerSecond = 6.5f;
        public const float BlockStaminaPerHit = 12f;

        // Combo
        public const float ComboWindow = 0.75f;
        public const float AttackLockTime = 0.42f;

        /// <summary>Movement speed the server accepts before flagging a client as cheating.</summary>
        public const float ServerSpeedTolerance = 1.35f;

        public static QualityTier DetectDefaultTier()
        {
            int mem = SystemInfo.systemMemorySize;
            int gfx = SystemInfo.graphicsMemorySize;
            bool mobile = Application.isMobilePlatform;

            if (mobile && (mem < 3000 || gfx < 512)) return QualityTier.Low;
            if (mobile) return QualityTier.Medium;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return QualityTier.Low;
            return mem >= 16000 && gfx >= 6000 ? QualityTier.Ultra : QualityTier.High;
        }

        public static void ApplyTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 20f;
                    QualitySettings.antiAliasing = 0;
                    break;
                case QualityTier.Medium:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 35f;
                    QualitySettings.antiAliasing = 0;
                    break;
                case QualityTier.High:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 55f;
                    QualitySettings.antiAliasing = 2;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 90f;
                    QualitySettings.antiAliasing = 4;
                    break;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = tier == QualityTier.Low ? 30 : 60;
        }
    }
}
