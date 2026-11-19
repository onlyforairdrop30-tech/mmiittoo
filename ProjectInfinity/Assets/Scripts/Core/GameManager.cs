using System;
using System.Collections;
using ProjectInfinity.Combat;
using ProjectInfinity.Enemies;
using ProjectInfinity.Inventory;
using ProjectInfinity.Player;
using ProjectInfinity.Progression;
using ProjectInfinity.Quests;
using UnityEngine;

namespace ProjectInfinity.Core
{
    /// <summary>
    /// The only scene singleton. Owns reward granting, player death/respawn and the notice feed.
    /// In multiplayer the same reward call is driven by the server's kill event instead of the local
    /// EnemyBrain (see docs/ROADMAP.md phase 6); the client never rolls its own loot online.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene wiring")]
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private QuestLog questLog;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform respawnPoint;

        public PlayerProgression Progression => progression;
        public InventoryComponent Inventory => inventory;
        public QuestLog Quests => questLog;
        public PlayerHealth PlayerHealth => playerHealth;
        public PlayerMotor Motor => motor;
        public int Gold => inventory != null ? inventory.Gold : 0;

        public event Action<string> Notice;
        public event Action<EnemyDefinition, Vector3, bool> EnemyDefeated;

        private readonly System.Random _offlineRng = new System.Random(9174);
        private bool _respawning;

        private void Awake() => Instance = this;

        /// <summary>Public entry point so other systems can post to the HUD notice feed.</summary>
        public void PushNotice(string message) => Notice?.Invoke(message);

        private void OnEnable()
        {
            if (questLog != null) questLog.QuestCompleted += OnQuestCompleted;
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (questLog != null) questLog.QuestCompleted -= OnQuestCompleted;
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        public void Bind(PlayerProgression prog, InventoryComponent inv, QuestLog quests,
                         PlayerHealth health, PlayerMotor playerMotor, Transform spawn)
        {
            progression = prog;
            inventory = inv;
            questLog = quests;
            playerHealth = health;
            motor = playerMotor;
            respawnPoint = spawn;
        }

        public void RegisterKill(EnemyBrain brain)
        {
            if (brain == null) return;
            GrantKillRewards(brain.Definition, brain.transform.position, false);
        }

        public void RegisterBossKill(BossBrain boss)
        {
            if (boss == null) return;
            GrantKillRewards(boss.Definition, boss.transform.position, true);
        }

        public void GrantKillRewards(EnemyDefinition definition, Vector3 at, bool isBoss)
        {
            if (definition == null || progression == null) return;

            long xp = XpCurve.KillXp(progression.Level, definition.level, definition.baseXp);
            int levelsGained = progression.AddXp(xp);

            int gold = LootTable.RollGold(definition, _offlineRng);
            if (inventory != null && gold > 0) inventory.AddGold(gold);

            if (inventory != null)
            {
                foreach (var drop in LootTable.Roll(definition, _offlineRng))
                {
                    inventory.TryGive(drop.itemId, drop.count);
                }
            }

            if (questLog != null) questLog.ReportKill(definition.enemyId);

            Notice?.Invoke($"{definition.displayName} defeated   +{xp} XP" + (gold > 0 ? $"   +{gold} gold" : ""));
            if (levelsGained > 0) Notice?.Invoke($"Level {progression.Level} reached — stats increased");

            EnemyDefeated?.Invoke(definition, at, isBoss);
        }

        private void OnQuestCompleted(QuestDefinition quest)
        {
            if (quest == null) return;

            if (progression != null && quest.xpReward > 0)
            {
                progression.AddXp(quest.xpReward);
                Notice?.Invoke($"Quest complete: {quest.title}   +{quest.xpReward} XP");
            }

            if (inventory != null)
            {
                if (quest.goldReward > 0) inventory.AddGold(quest.goldReward);
                if (quest.itemRewards != null)
                {
                    foreach (var itemId in quest.itemRewards)
                    {
                        if (!string.IsNullOrEmpty(itemId)) inventory.TryGive(itemId, 1);
                    }
                }
            }
        }

        private void OnPlayerDied(DamageInfo info)
        {
            if (_respawning) return;
            _respawning = true;
            Notice?.Invoke("You have fallen. Reforming at the shrine...");
            StartCoroutine(RespawnAfter(3.5f));
        }

        private IEnumerator RespawnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (playerHealth != null) playerHealth.ReviveFull();
            if (motor != null && respawnPoint != null) motor.Teleport(respawnPoint.position + Vector3.up * 0.2f);

            _respawning = false;
            Notice?.Invoke("Returned to the Verdant Shrine");
        }
    }
}
