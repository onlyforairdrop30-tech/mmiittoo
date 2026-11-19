import test from 'node:test';
import assert from 'node:assert/strict';

import { applyAttack, computeDamage, mitigate, validateAttack, ABILITIES } from '../src/sim/combat.js';
import { validateMove } from '../src/sim/movement.js';
import { killXp, grantKillXp, levelForXp, statsForLevel, xpToNext, MAX_LEVEL, totalXpForLevel } from '../src/sim/xp.js';
import { mulberry32, rollLoot, rollGold, ENEMIES } from '../src/sim/loot.js';

function fighter(overrides = {}) {
  return {
    position: { x: 0, y: 0, z: 0 },
    health: 120,
    maxHealth: 120,
    attack: 20,
    defense: 6,
    energy: 100,
    lastAttackAt: 0,
    ...overrides
  };
}

// ---------------------------------------------------------------- combat

test('damage is computed by the server, never taken from the client', () => {
  const attacker = fighter();
  const target = fighter({ position: { x: 1.5, y: 0, z: 0 }, health: 500, maxHealth: 500 });

  const result = applyAttack({
    attacker,
    target,
    abilityId: 'light',
    now: 10_000,
    // A cheating client would attach its own numbers; they must be ignored.
    damage: 99999,
    crit: true,
    targetHealth: 0
  });

  const expected = computeDamage({ attack: 20, power: ABILITIES.light.power, defense: 6 });

  assert.equal(result.ok, true);
  assert.equal(result.damage, expected);
  assert.ok(result.damage < 50, `server damage must stay sane, got ${result.damage}`);
  assert.equal(target.health, 500 - expected);
});

test('attacks inside the cooldown window are rejected', () => {
  const attacker = fighter();
  const target = fighter({ position: { x: 1, y: 0, z: 0 } });

  assert.equal(validateAttack({ attacker, target, abilityId: 'heavy', now: 1000 }).ok, true);

  applyAttack({ attacker, target, abilityId: 'heavy', now: 1000 });
  const second = validateAttack({ attacker, target, abilityId: 'heavy', now: 1100 });

  assert.equal(second.ok, false);
  assert.equal(second.reason, 'cooldown');
});

test('attacks beyond the ability range are rejected', () => {
  const attacker = fighter();
  const target = fighter({ position: { x: 12, y: 0, z: 0 } });

  const result = validateAttack({ attacker, target, abilityId: 'light', now: 5000 });
  assert.equal(result.ok, false);
  assert.equal(result.reason, 'out_of_range');
});

test('attacks against dead targets are rejected', () => {
  const attacker = fighter();
  const target = fighter({ position: { x: 1, y: 0, z: 0 }, health: 0 });

  const result = validateAttack({ attacker, target, abilityId: 'light', now: 5000 });
  assert.equal(result.ok, false);
  assert.equal(result.reason, 'target_dead');
});

test('abilities without energy are rejected', () => {
  const attacker = fighter({ energy: 0 });
  const target = fighter({ position: { x: 2, y: 0, z: 0 } });

  const result = validateAttack({ attacker, target, abilityId: 'ember_nova', now: 20000 });
  assert.equal(result.ok, false);
  assert.equal(result.reason, 'no_energy');
});

test('unknown abilities are rejected', () => {
  const result = validateAttack({ attacker: fighter(), target: fighter(), abilityId: 'one_shot_kill', now: 5000 });
  assert.equal(result.ok, false);
  assert.equal(result.reason, 'unknown_ability');
});

test('mitigation has diminishing returns and never reaches zero', () => {
  assert.equal(mitigate(100, 0), 100);
  assert.equal(mitigate(100, 100), 50);
  assert.ok(mitigate(100, 400) > 0);
});

// ---------------------------------------------------------------- movement

test('teleporting (speed hack) is rejected', () => {
  const result = validateMove({
    from: { x: 0, y: 0, z: 0 },
    to: { x: 25, y: 0, z: 0 },
    dtSeconds: 0.05,
    maxSpeed: 7.4
  });

  assert.equal(result.ok, false);
  assert.equal(result.reason, 'speed');
});

test('normal running is accepted', () => {
  const result = validateMove({
    from: { x: 0, y: 0, z: 0 },
    to: { x: 0.35, y: 0, z: 0 },
    dtSeconds: 0.05,
    maxSpeed: 7.4
  });

  assert.equal(result.ok, true);
  assert.equal(result.position.x, 0.35);
});

test('leaving the world bounds is rejected', () => {
  const result = validateMove({
    from: { x: 0, y: 0, z: 0 },
    to: { x: 5000, y: 0, z: 0 },
    dtSeconds: 60,
    maxSpeed: 7.4
  });

  assert.equal(result.ok, false);
  assert.equal(result.reason, 'out_of_bounds');
});

test('invalid numeric input is rejected rather than applied', () => {
  const result = validateMove({
    from: { x: 0, y: 0, z: 0 },
    to: { x: Number.NaN, z: 0 },
    dtSeconds: 1,
    maxSpeed: 7.4
  });

  assert.equal(result.ok, false);
  assert.equal(result.reason, 'invalid_position');
});

// ---------------------------------------------------------------- XP

test('kill XP drops for trivial content and never goes negative', () => {
  assert.ok(killXp(20, 5, 100) < killXp(20, 20, 100));
  assert.ok(killXp(20, 25, 100) > killXp(20, 20, 100));
  assert.equal(killXp(60, 1, 0), 0);
});

test('level curve is capped at 60 with no further XP requirement', () => {
  assert.equal(xpToNext(MAX_LEVEL), 0);
  assert.equal(levelForXp(Number.MAX_SAFE_INTEGER), MAX_LEVEL);
});

test('level lookup is the inverse of the cumulative table', () => {
  for (const level of [1, 5, 17, 33, 60]) {
    assert.equal(levelForXp(totalXpForLevel(level)), level);
  }
});

test('granting XP applies level ups and derived stats on the server', () => {
  const character = { level: 1, xp: 0, health: 120, maxHealth: 120, attack: 20, defense: 6 };

  const result = grantKillXp(character, { level: 8, baseXp: 5000 });

  assert.ok(result.xp > 0);
  assert.ok(result.levelsGained > 0);
  assert.ok(character.level > 1);
  assert.equal(character.maxHealth, statsForLevel(character.level).maxHealth);
  assert.equal(character.attack, statsForLevel(character.level).attack);
});

test('a level-capped character gains no further levels', () => {
  const character = { level: MAX_LEVEL, xp: totalXpForLevel(MAX_LEVEL), health: 900, maxHealth: 900, attack: 99, defense: 40 };
  const result = grantKillXp(character, { level: 8, baseXp: 5000 });

  assert.equal(result.levelsGained, 0);
  assert.equal(character.level, MAX_LEVEL);
});

// ---------------------------------------------------------------- loot

test('loot rolls are deterministic for a seed', () => {
  const table = [{ itemId: 'material.ember_shard', chance: 0.5, min: 1, max: 3 }];
  const a = rollLoot(table, mulberry32(42));
  const b = rollLoot(table, mulberry32(42));

  assert.deepEqual(a, b);
});

test('loot chance zero never drops and chance one always drops', () => {
  const random = mulberry32(7);
  assert.deepEqual(rollLoot([{ itemId: 'x', chance: 0, min: 1, max: 1 }], random), []);
  assert.equal(rollLoot([{ itemId: 'x', chance: 1, min: 1, max: 1 }], random).length, 1);
});

test('gold rolls stay within the designed band', () => {
  const random = mulberry32(3);
  for (let i = 0; i < 200; i += 1) {
    const gold = rollGold(10, random);
    assert.ok(gold >= 10 && gold <= 15, `gold out of band: ${gold}`);
  }
});

test('the server enemy table matches the client content ids', () => {
  for (const id of ['monster.blight_stalker', 'monster.ember_wisp', 'monster.stone_sentry', 'monster.rootwarden']) {
    assert.ok(ENEMIES[id], `missing enemy definition for ${id}`);
    assert.ok(ENEMIES[id].baseXp > 0);
  }
});
