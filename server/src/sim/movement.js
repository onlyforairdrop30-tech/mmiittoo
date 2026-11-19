// Movement authority. The client sends a position; the server checks the implied speed against a
// budget the fastest legal run could reach, plus a small slack for latency and tick jitter.

export const WORLD_BOUNDS = 400;

export function validateMove({
  from,
  to,
  dtSeconds,
  maxSpeed,
  tolerance = 1.35,
  slack = 0.75,
  bounds = WORLD_BOUNDS
}) {
  if (!Number.isFinite(to?.x) || !Number.isFinite(to?.z)) {
    return { ok: false, reason: 'invalid_position' };
  }

  if (Math.abs(to.x) > bounds || Math.abs(to.z) > bounds) {
    return { ok: false, reason: 'out_of_bounds' };
  }

  const dt = Math.max(0.001, dtSeconds);
  const travelled = Math.hypot(to.x - (from?.x ?? 0), to.z - (from?.z ?? 0));
  const budget = maxSpeed * tolerance * dt + slack;

  if (travelled > budget) {
    return {
      ok: false,
      reason: 'speed',
      travelled,
      budget,
      position: { x: from?.x ?? 0, y: from?.y ?? 0, z: from?.z ?? 0 }
    };
  }

  return { ok: true, position: { x: to.x, y: Number(to.y) || 0, z: to.z } };
}
