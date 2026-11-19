// Authoritative combat. The client may predict, but only this result is trusted.
// Formulas intentionally mirror ProjectInfinity/Assets/Scripts/Combat/CombatFormulas.cs.

export const ABILITIES = {
  light: { power: 1.5, range: 2.8, cooldownMs: 300, energy: 0 },
  light_2: { power: 1.72, range: 2.8, cooldownMs: 300, energy: 0 },
  light_3: { power: 1.94, range: 2.8, cooldownMs: 300, energy: 0 },
  heavy: { power: 3.2, range: 2.9, cooldownMs: 900, energy: 0 },
  verdant_lash: { power: 2.6, range: 3.4, cooldownMs: 5000, energy: 18 },
  ember_nova: { power: 3.5, range: 5.2, cooldownMs: 9000, energy: 26 },
  gale_step: { power: 2.2, range: 4.2, cooldownMs: 7000, energy: 21 },
  awakening_burst: { power: 8, range: 8, cooldownMs: 30000, energy: 100 }
};

const RANGE_SLACK = 0.75;
const MAX_ATTACK_RANGE = 9;

export function mitigate(raw, defense) {
  if (!(raw > 0)) return 0;
  const value = Math.max(0, Number(defense) || 0);
  return raw * (100 / (100 + value));
}

export function computeDamage({ attack, power, defense, critMultiplier = 1.55, critical = false }) {
  let raw = Math.max(0, Number(attack) || 0) * Math.max(0, Number(power) || 0);
  if (critical) raw *= Math.max(1, critMultiplier);
  return Math.round(mitigate(raw, defense) * 10) / 10;
}

export function distance(a, b) {
  const dx = (a?.x ?? 0) - (b?.x ?? 0);
  const dz = (a?.z ?? 0) - (b?.z ?? 0);
  return Math.hypot(dx, dz);
}

/**
 * Validates an attack intent and returns the authoritative damage.
 * Rejections are explicit so a cheating client gets a reason instead of silent success.
 */
export function validateAttack({ attacker, target, abilityId, now = Date.now() }) {
  const ability = ABILITIES[abilityId];
  if (!ability) return { ok: false, reason: 'unknown_ability' };
  if (!attacker) return { ok: false, reason: 'unknown_attacker' };
  if (!target || target.health <= 0) return { ok: false, reason: 'target_dead' };

  if (now - (attacker.lastAttackAt ?? 0) < ability.cooldownMs) {
    return { ok: false, reason: 'cooldown' };
  }

  const range = Math.min(ability.range + RANGE_SLACK, MAX_ATTACK_RANGE);
  if (distance(attacker.position, target.position) > range) {
    return { ok: false, reason: 'out_of_range' };
  }

  if ((attacker.energy ?? 0) < ability.energy) {
    return { ok: false, reason: 'no_energy' };
  }

  const damage = computeDamage({ attack: attacker.attack, power: ability.power, defense: target.defense });
  return { ok: true, damage, ability };
}

/** Applies a validated attack to the target and returns the resulting state. */
export function applyAttack({ attacker, target, abilityId, now = Date.now() }) {
  const result = validateAttack({ attacker, target, abilityId, now });
  if (!result.ok) return result;

  attacker.lastAttackAt = now;
  attacker.energy = Math.max(0, (attacker.energy ?? 0) - result.ability.energy);
  target.health = Math.max(0, Math.round((target.health - result.damage) * 10) / 10);

  return {
    ok: true,
    damage: result.damage,
    targetHealth: target.health,
    killed: target.health <= 0
  };
}
