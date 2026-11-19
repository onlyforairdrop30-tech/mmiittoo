using System;
using System.Collections.Generic;

namespace ProjectInfinity.Progression
{
    public enum StatType
    {
        MaxHealth,
        Attack,
        Defense,
        CritChance,
        CritDamage,
        MoveSpeed,
        MaxStamina,
        MaxEnergy,
        SkillPower
    }

    /// <summary>Additive stat container. Equipment, buffs and level all contribute blocks.</summary>
    [Serializable]
    public class StatBlock
    {
        private readonly Dictionary<StatType, float> _values = new Dictionary<StatType, float>();

        public float this[StatType type]
        {
            get => _values.TryGetValue(type, out var v) ? v : 0f;
            set => _values[type] = value;
        }

        public void Add(StatType type, float amount) => this[type] = this[type] + amount;

        public void Clear() => _values.Clear();

        public void Add(StatBlock other)
        {
            if (other == null) return;
            foreach (var kv in other._values) Add(kv.Key, kv.Value);
        }

        public StatBlock Clone()
        {
            var copy = new StatBlock();
            foreach (var kv in _values) copy._values[kv.Key] = kv.Value;
            return copy;
        }

        public IEnumerable<KeyValuePair<StatType, float>> All => _values;
    }
}
