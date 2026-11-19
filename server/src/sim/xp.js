// XP authority. Mirrors ProjectInfinity/Assets/Scripts/Progression/XpCurve.cs exactly:
// the client predicts a level-up, the server decides it.

export const MAX_LEVEL = 60;
const BASE = 90;
const GROWTH = 1.115;

export function xpToNext(level) {
  if (level >= MAX_LEVEL) return 0;
  const clamped = Math.max(1, level);
  return Math.round(BASE * Math.pow(GROWTH, clamped - 1));
}

export function totalXpForLevel(level) {
  if (level <= 1) return 0;
  const capped = Math.min(MAX_LEVEL, level);
  let total = 0;
  for (let i = 1; i < capped; i += 1) total += xpToNext(i);
  return total;
}

export function levelForXp(totalXp) {
  let level = 1;
  let remaining = Math.max(0, totalXp);
  while (level < MAX_LEVEL) {
    const need = xpToNext(level);
    if (remaining < need) break;
    remaining -= need;
    level += 1;
  }
  return level;
}

export function xpIntoLevel(totalXp) {
  return Math.max(0, totalXp) - totalXpForLevel(levelForXp(totalXp));
}

/** Diminishing rewards so farming trivial content is never the optimal path. */
export function killXp(playerLevel, enemyLevel, baseXp) {
  const diff = enemyLevel - playerLevel;
  let scale;
  if (diff >= 3) scale = 1.0;
  else if (diff >= 0) scale = 0.8 + diff * 0.1;
  else if (diff >= -5) scale = Math.max(0.25, 0.8 + diff * 0.12);
  else scale = 0.1;

  return Math.max(0, Math.round(baseXp * scale));
}

/** Derived stats for a level — same numbers as PlayerProgression.Recalculate(). */
export function statsForLevel(level) {
  return {
    maxHealth: 120 + (level - 1) * 14,
    attack: 12 + (level - 1) * 2.1,
    defense: 6 + (level - 1) * 1.4
  };
}

/**
 * Grants kill XP and any level-ups. Returns the applied result; the caller broadcasts it.
 * There is no path for a client to submit its own XP value.
 */
export function grantKillXp(character, enemy) {
  if (!character || !enemy) return { xp: 0, levelsGained: 0, level: character?.level ?? 1 };

  const awarded = killXp(character.level, enemy.level ?? 1, enemy.baseXp ?? 0);
  if (awarded <= 0) return { xp: 0, levelsGained: 0, level: character.level };

  character.xp += awarded;
  const nextLevel = levelForXp(character.xp);
  const levelsGained = Math.max(0, nextLevel - character.level);

  if (levelsGained > 0) {
    character.level = nextLevel;
    const stats = statsForLevel(nextLevel);
    character.maxHealth = stats.maxHealth;
    character.attack = stats.attack;
    character.defense = stats.defense;
    character.health = Math.min(character.maxHealth, character.health + stats.maxHealth * 0.35);
  }

  return { xp: awarded, levelsGained, level: character.level };
}
