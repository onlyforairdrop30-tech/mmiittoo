// Configuration is environment-driven. Nothing here enables a billable service on its own.
import { randomBytes } from 'node:crypto';

function numberFromEnv(name, fallback) {
  const raw = process.env[name];
  if (raw === undefined || raw === '') return fallback;
  const value = Number(raw);
  return Number.isFinite(value) ? value : fallback;
}

export const config = {
  port: numberFromEnv('PORT', 8787),
  host: process.env.HOST ?? '127.0.0.1',

  // In production this MUST come from the environment; the dev fallback is random per boot so
  // tokens never survive a restart and no secret is committed to the repository.
  tokenSecret: process.env.TOKEN_SECRET ?? randomBytes(32).toString('hex'),
  tokenTtlSeconds: numberFromEnv('TOKEN_TTL_SECONDS', 900),

  tickHz: numberFromEnv('TICK_HZ', 20),

  // Movement authority: how much faster than the fastest legal run a player may appear to move.
  speedTolerance: numberFromEnv('SPEED_TOLERANCE', 1.35),
  maxRunSpeed: numberFromEnv('MAX_RUN_SPEED', 7.4),

  rateLimit: {
    authAttempts: numberFromEnv('RATE_AUTH_ATTEMPTS', 10),
    authWindowMs: numberFromEnv('RATE_AUTH_WINDOW_MS', 60_000),
    messagesPerSecond: numberFromEnv('RATE_MESSAGES_PER_SECOND', 60)
  },

  // Payment providers. Unset by default -> the verify endpoint answers 501 instead of pretending.
  payments: {
    googlePlay: process.env.GOOGLE_PLAY_SERVICE_ACCOUNT === undefined ? null : 'configured',
    appStore: process.env.APPLE_ISSUER_ID === undefined ? null : 'configured',
    steam: process.env.STEAM_WEB_API_KEY === undefined ? null : 'configured',
    stripe: process.env.STRIPE_SECRET_KEY === undefined ? null : 'configured'
  },

  adminEmail: process.env.ADMIN_EMAIL ?? 'admin@localhost',

  logLevel: process.env.LOG_LEVEL ?? 'info'
};

export function isPaymentProviderConfigured(platform, cfg = config) {
  switch (platform) {
    case 'google_play': return Boolean(cfg.payments.googlePlay);
    case 'app_store': return Boolean(cfg.payments.appStore);
    case 'steam': return Boolean(cfg.payments.steam);
    case 'stripe': return Boolean(cfg.payments.stripe);
    default: return false;
  }
}
