using System.Collections.Generic;
using ProjectInfinity.Enemies;
using ProjectInfinity.Inventory;
using ProjectInfinity.Progression;
using ProjectInfinity.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectInfinity.EditorTools
{
    /// <summary>
    /// Authors the prototype's data assets (items, enemies, quest) as real ScriptableObjects.
    /// Idempotent: re-running updates the existing assets instead of duplicating them.
    /// </summary>
    public static class ContentSeeder
    {
        public const string ContentRoot = "Assets/Content";
        public const string ItemsPath = ContentRoot + "/Items";
        public const string EnemiesPath = ContentRoot + "/Enemies";
        public const string QuestsPath = ContentRoot + "/Quests";
        public const string ItemDbPath = ContentRoot + "/ItemDatabase.asset";

        public const string QuestId = "quest.blight_purge";
        public const string StalkerId = "monster.blight_stalker";
        public const string WispId = "monster.ember_wisp";
        public const string SentryId = "monster.stone_sentry";
        public const string BossId = "monster.rootwarden";
        public const string ShardId = "material.ember_shard";
        public const string BladeId = "weapon.verdant_blade";

        [MenuItem("Project Infinity/3. Seed Content", priority = 3)]
        public static void SeedFromMenu()
        {
            var db = CreateAll(out var mobs, out var boss, out var quest);
            Debug.Log($"[Project Infinity] Content seeded: {db.All.Count} items, {mobs.Count} enemies, " +
                      $"boss '{boss.displayName}', quest '{quest.title}'.");
        }

        public static ItemDatabase CreateAll(out List<EnemyDefinition> mobs, out EnemyDefinition boss,
                                             out QuestDefinition quest)
        {
            EnsureFolder(ItemsPath);
            EnsureFolder(EnemiesPath);
            EnsureFolder(QuestsPath);

            var db = LoadOrCreate<ItemDatabase>(ItemDbPath);

            var shard = Item(ShardId, "Ember Shard", ItemCategory.Material, EquipmentSlot.None, 99, 6,
                             "A warm sliver of crystallised flame. Alchemists pay well for these.");
            var blade = Item(BladeId, "Verdant Blade", ItemCategory.Weapon, EquipmentSlot.Weapon, 1, 120,
                             "A leaf-steel longsword grown, not forged.",
                             new StatBonus { stat = StatType.Attack, amount = 8f });
            var vest = Item("armor.leaf_vest", "Leafweave Vest", ItemCategory.Armor, EquipmentSlot.Chest, 1, 90,
                            "Living armour that drinks the morning dew.",
                            new StatBonus { stat = StatType.MaxHealth, amount = 30f },
                            new StatBonus { stat = StatType.Defense, amount = 4f });
            var petal = Item("consumable.moonpetal", "Moonpetal Draught", ItemCategory.Consumable,
                             EquipmentSlot.None, 20, 12, "Restores a little health when consumed.");
            var ring = Item("accessory.wanderer_ring", "Wanderer's Ring", ItemCategory.Accessory,
                            EquipmentSlot.Ring, 1, 150, "It hums when danger is near.",
                            new StatBonus { stat = StatType.CritChance, amount = 0.05f });

            foreach (var item in new[] { shard, blade, vest, petal, ring }) db.Register(item);
            EditorUtility.SetDirty(db);

            mobs = new List<EnemyDefinition>
            {
                Enemy(StalkerId, "Blight Stalker", EnemyArchetype.Melee, 3, 110f, 9f, 5f, 3.9f, 2.0f, 1.85f, 55,
                      new Color(0.24f, 0.42f, 0.34f), 1f, ShardId, 0.75f),
                Enemy(WispId, "Ember Wisp", EnemyArchetype.Ranged, 4, 80f, 12f, 2f, 3.3f, 6.5f, 2.4f, 65,
                      new Color(0.95f, 0.52f, 0.18f), 0.85f, ShardId, 0.55f),
                Enemy(SentryId, "Stone Sentry", EnemyArchetype.Charger, 5, 190f, 15f, 12f, 2.9f, 2.2f, 2.6f, 95,
                      new Color(0.46f, 0.47f, 0.42f), 1.35f, BladeId, 0.18f)
            };

            boss = Enemy(BossId, "The Rootwarden", EnemyArchetype.Boss, 8, 1100f, 18f, 14f, 2.7f, 5f, 1.7f, 900,
                         new Color(0.36f, 0.29f, 0.20f), 2.6f, BladeId, 0.9f);
            boss.goldReward = 120;
            EditorUtility.SetDirty(boss);

            quest = Quest(QuestId, "Blight of the Hollow",
                          "Warden Sylva says the standing stones are bleeding blight. Thin the stalkers and " +
                          "bring back ember shards so the wards can be re-lit.",
                          1, 400, 60, new[] { BladeId },
                          new QuestObjective
                          {
                              type = QuestObjectiveType.Kill,
                              targetId = StalkerId,
                              description = "Defeat Blight Stalkers",
                              required = 3
                          },
                          new QuestObjective
                          {
                              type = QuestObjectiveType.Collect,
                              targetId = ShardId,
                              description = "Recover Ember Shards",
                              required = 2
                          });

            return db;
        }

        public static bool LoadAll(out ItemDatabase db, out List<EnemyDefinition> mobs,
                                   out EnemyDefinition boss, out QuestDefinition quest)
        {
            db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDbPath);
            mobs = new List<EnemyDefinition>();
            boss = null;
            quest = null;

            if (db == null) return false;

            foreach (var id in new[] { StalkerId, WispId, SentryId })
            {
                var def = LoadEnemy(id);
                if (def == null) return false;
                mobs.Add(def);
            }

            boss = LoadEnemy(BossId);
            quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>($"{QuestsPath}/{QuestId.Replace('.', '_')}.asset");
            return boss != null && quest != null;
        }

        private static EnemyDefinition LoadEnemy(string id) =>
            AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EnemiesPath}/{id.Replace('.', '_')}.asset");

        // ---------------------------------------------------------------- helpers

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ItemDefinition Item(string id, string displayName, ItemCategory category,
                                           EquipmentSlot slot, int maxStack, int value, string description,
                                           params StatBonus[] bonuses)
        {
            string path = $"{ItemsPath}/{id.Replace('.', '_')}.asset";
            var item = LoadOrCreate<ItemDefinition>(path);
            item.itemId = id;
            item.displayName = displayName;
            item.category = category;
            item.slot = slot;
            item.maxStack = maxStack;
            item.goldValue = value;
            item.description = description;
            item.bonuses = bonuses;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static EnemyDefinition Enemy(string id, string displayName, EnemyArchetype archetype, int level,
                                             float health, float attack, float defense, float speed, float range,
                                             float cooldown, long xp, Color tint, float scale,
                                             string lootItem, float lootChance)
        {
            string path = $"{EnemiesPath}/{id.Replace('.', '_')}.asset";
            var def = LoadOrCreate<EnemyDefinition>(path);
            def.enemyId = id;
            def.displayName = displayName;
            def.archetype = archetype;
            def.level = level;
            def.maxHealth = health;
            def.attack = attack;
            def.defense = defense;
            def.moveSpeed = speed;
            def.attackRange = range;
            def.attackCooldown = cooldown;
            def.baseXp = xp;
            def.tint = tint;
            def.scale = scale;
            def.goldReward = Mathf.Max(3, (int)(xp / 6));
            def.loot = new[]
            {
                new LootEntry { itemId = lootItem, chance = lootChance, min = 1, max = 2 }
            };
            EditorUtility.SetDirty(def);
            return def;
        }

        private static QuestDefinition Quest(string id, string title, string description, int requiredLevel,
                                              long xp, int gold, string[] items, params QuestObjective[] objectives)
        {
            string path = $"{QuestsPath}/{id.Replace('.', '_')}.asset";
            var quest = LoadOrCreate<QuestDefinition>(path);
            quest.questId = id;
            quest.title = title;
            quest.description = description;
            quest.requiredLevel = requiredLevel;
            quest.xpReward = xp;
            quest.goldReward = gold;
            quest.itemRewards = items;
            quest.objectives = objectives;
            quest.isMainStory = true;
            EditorUtility.SetDirty(quest);
            return quest;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
