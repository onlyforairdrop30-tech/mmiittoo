// Loot and the authoritative enemy content table. The client never supplies XP, gold or drops.

/** Deterministic PRNG so loot is reproducible in tests and replays. */
export function mulberry32(seed) {
  let state = seed >>> 0;
  return function random() {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function rollLoot(table, random) {
  const drops = [];
  if (!Array.isArray(table)) return drops;

  for (const entry of table) {
    if (!entry || !entry.itemId) continue;
    if (random() > (entry.chance ?? 0)) continue;

    const min = Math.max(1, entry.min ?? 1);
    const max = Math.max(min, entry.max ?? min);
    const count = min === max ? min : min + Math.floor(random() * (max - min + 1));
    drops.push({ itemId: entry.itemId, count });
  }

  return drops;
}

export function rollGold(goldReward, random) {
  if (!(goldReward > 0)) return 0;
  return goldReward + Math.floor(random() * (goldReward / 2 + 1));
}

/**
 * Authoritative enemy table. Mirrors Assets/Editor/ContentSeeder.cs; the server is the only
 * source of rewards, so a modified client cannot inflate them.
 */
export const ENEMIES = {
  'monster.blight_stalker': {
    displayName: 'Blight Stalker',
    level: 3,
    baseXp: 55,
    goldReward: 9,
    maxHealth: 110,
    defense: 5,
    loot: [{ itemId: 'material.ember_shard', chance: 0.75, min: 1, max: 2 }]
  },
  'monster.ember_wisp': {
    displayName: 'Ember Wisp',
    level: 4,
    baseXp: 65,
    goldReward: 10,
    maxHealth: 80,
    defense: 2,
    loot: [{ itemId: 'material.ember_shard', chance: 0.55, min: 1, max: 1 }]
  },
  'monster.stone_sentry': {
    displayName: 'Stone Sentry',
    level: 5,
    baseXp: 95,
    goldReward: 15,
    maxHealth: 190,
    defense: 12,
    loot: [{ itemId: 'weapon.verdant_blade', chance: 0.18, min: 1, max: 1 }]
  },
  'monster.rootwarden': {
    displayName: 'The Rootwarden',
    level: 8,
    baseXp: 900,
    goldReward: 120,
    maxHealth: 1100,
    defense: 14,
    loot: [{ itemId: 'weapon.verdant_blade', chance: 0.9, min: 1, max: 1 }]
  }
};

export function getEnemy(enemyId) {
  return ENEMIES[enemyId] ?? null;
}
