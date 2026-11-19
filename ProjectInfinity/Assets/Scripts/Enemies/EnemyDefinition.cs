using System;
using UnityEngine;

namespace ProjectInfinity.Enemies
{
    public enum EnemyArchetype { Melee, Ranged, Charger, Boss }

    [Serializable]
    public struct LootEntry
    {
        public string itemId;
        [Range(0f, 1f)] public float chance;
        public int min;
        public int max;
    }

    [CreateAssetMenu(menuName = "Project Infinity/Enemy", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Stable id shared with the server content table.")]
        public string enemyId = "monster.new";
        public string displayName = "Unnamed";

        public EnemyArchetype archetype = EnemyArchetype.Melee;
        [Range(1, 60)] public int level = 1;

        [Header("Combat")]
        public float maxHealth = 90f;
        public float attack = 10f;
        public float defense = 4f;
        public float attackRange = 1.9f;
        public float attackCooldown = 1.8f;
        public float windup = 0.45f;
        public float aggroRange = 16f;
        public float leashRange = 30f;

        [Header("Movement")]
        public float moveSpeed = 3.6f;
        public float strafeBias = 0.35f;

        [Header("Rewards")]
        public long baseXp = 45;
        public int goldReward = 8;
        public LootEntry[] loot = Array.Empty<LootEntry>();

        [Header("Presentation")]
        public Color tint = Color.white;
        public float scale = 1f;
        public float spawnRadius = 1.1f;
    }
}
