// Store catalogue. Prices are in minor units of the platform currency (EUR cents by default).
// Every product here is cosmetic, convenience or content — never combat power.

export const CATALOG = {
  'lumina.100': { priceMinor: 99, category: 'currency', grants: { lumina: 100 }, name: '100 Lumina' },
  'lumina.500': { priceMinor: 399, category: 'currency', grants: { lumina: 500 }, name: '500 Lumina' },
  'lumina.1200': { priceMinor: 799, category: 'currency', grants: { lumina: 1200 }, name: '1200 Lumina' },
  'lumina.2400': { priceMinor: 1499, category: 'currency', grants: { lumina: 2400 }, name: '2400 Lumina' },

  'cosmetic.outfit.starfall': {
    priceMinor: 499, category: 'cosmetic', grants: {}, name: 'Starfall Outfit', cosmetic: true
  },
  'cosmetic.mount.emberwing': {
    priceMinor: 599, category: 'cosmetic', grants: {}, name: 'Emberwing Mount Skin', cosmetic: true
  },
  'cosmetic.emote.wave': {
    priceMinor: 99, category: 'cosmetic', grants: {}, name: 'Sunrise Wave Emote', cosmetic: true
  },
  'housing.decor.lantern': {
    priceMinor: 149, category: 'cosmetic', grants: {}, name: 'Spirit Lantern (housing)', cosmetic: true
  },

  'seasonpass.s1': {
    priceMinor: 899, category: 'season_pass', grants: {}, name: 'Season 1 Premium Track'
  },
  'expansion.s1': {
    priceMinor: 1299, category: 'expansion', grants: {}, name: 'Expansion I: Tidebound Reach'
  },
  'membership.supporter': {
    priceMinor: 499, category: 'membership', grants: { lumina: 100 }, name: 'Supporter Membership (30 days)'
  }
};

export function findSku(sku) {
  return Object.hasOwn(CATALOG, sku) ? CATALOG[sku] : null;
}

export function publicCatalog() {
  return Object.entries(CATALOG).map(([sku, product]) => ({
    sku,
    name: product.name,
    priceMinor: product.priceMinor,
    category: product.category,
    cosmetic: Boolean(product.cosmetic)
  }));
}

/** Hard rule the game must never break: no product may carry a stat or damage bonus. */
export function isCosmeticOnly(product) {
  return product.category !== 'expansion' && product.category !== 'season_pass'
    ? Boolean(product.cosmetic) || product.category === 'currency' || product.category === 'membership'
    : true;
}
