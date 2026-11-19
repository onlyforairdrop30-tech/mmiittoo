using System;
using UnityEngine;

namespace ProjectInfinity.Progression
{
    /// <summary>
    /// Owns the player's level, total XP and derived stats. Equipment blocks are pushed in by
    /// <see cref="Inventory.EquipmentLoadout"/>; the resulting <see cref="Stats"/> is the single
    /// truth for combat and movement.
    /// </summary>
    public class PlayerProgression : MonoBehaviour
    {
        [SerializeField] private int startLevel = 1;
        [SerializeField] private long startingXp = 0;

        public int Level { get; private set; } = 1;
        public long TotalXp { get; private set; }
        public int SkillPoints { get; private set; }
        public StatBlock Stats { get; } = new StatBlock();

        private StatBlock _equipment = new StatBlock();
        private StatBlock _talents = new StatBlock();

        public event Action<int> LevelChanged;
        public event Action<long, long> XpChanged; // (xpIntoLevel, xpToNext)

        private void Awake()
        {
            Level = Mathf.Clamp(startLevel, 1, XpCurve.MaxLevel);
            TotalXp = startingXp > 0 ? startingXp : XpCurve.TotalXpForLevel(Level);
            Recalculate();
        }

        /// <summary>Adds XP and returns how many levels were gained.</summary>
        public int AddXp(long amount)
        {
            if (amount <= 0 || Level >= XpCurve.MaxLevel) return 0;

            TotalXp += amount;
            int before = Level;
            int after = XpCurve.LevelForXp(TotalXp);

            if (after != Level)
            {
                Level = after;
                int gained = Level - before;
                SkillPoints += gained;
                Recalculate();
                LevelChanged?.Invoke(Level);
            }

            XpChanged?.Invoke(XpCurve.XpIntoLevel(TotalXp), XpCurve.XpToNext(Level));
            return Mathf.Max(0, after - before);
        }

        public void SetEquipmentBlock(StatBlock block)
        {
            _equipment = block ?? new StatBlock();
            Recalculate();
        }

        public void SetTalentBlock(StatBlock block)
        {
            _talents = block ?? new StatBlock();
            Recalculate();
        }

        private void Recalculate()
        {
            Stats.Clear();
            Stats[StatType.MaxHealth] = 120f + (Level - 1) * 14f;
            Stats[StatType.Attack] = 12f + (Level - 1) * 2.1f;
            Stats[StatType.Defense] = 6f + (Level - 1) * 1.4f;
            Stats[StatType.CritChance] = 0.08f;
            Stats[StatType.CritDamage] = 1.55f;
            Stats[StatType.MoveSpeed] = 0f; // movement comes from PlayerMotor, equipment may add
            Stats[StatType.MaxStamina] = 100f;
            Stats[StatType.MaxEnergy] = 100f;
            Stats[StatType.SkillPower] = 1f;

            Stats.Add(_equipment);
            Stats.Add(_talents);

            XpChanged?.Invoke(XpCurve.XpIntoLevel(TotalXp), XpCurve.XpToNext(Level));
        }
    }
}
