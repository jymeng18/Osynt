export const milestones = [
  [1, "First plant", "First seed planted."],
  [5, "Wildflowers", "Wildflowers unlocked."],
  [12, "Orchard", "Fruit trees and butterflies unlocked."],
  [24, "Pond", "Pond and bridge unlocked."],
  [40, "Cottage", "Garden cottage unlocked."],
  [75, "Greenhouse", "Greenhouse unlocked."],
  [120, "Lanterns", "Lanterns unlocked."],
  [200, "More islands", "New islands unlocked."],
];

export function normalizeState(value = {}) {
  return {
    successes: Math.max(0, Math.floor(Number(value.successes) || 0)),
    atmosphere: value.atmosphere === "dusk" ? "dusk" : "day",
    reducedMotion: value.reducedMotion === true,
  };
}
export function growthMessage(count) {
  return (
    milestones.find(([at]) => at === count)?.[2] ??
    [
      "Plant fully grown.",
      "Seed planted.",
      "Plant growing.",
    ][count % 3]
  );
}
export function plantCount(successes) {
  return Math.ceil(Math.max(0, successes) / 3);
}
export function seeded(seed) {
  let value = seed >>> 0;
  return () => {
    value += 0x6d2b79f5;
    let t = value;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
// Visible detail is bounded. Additional lifetime growth matures the garden and opens new islands.
export function layout(successes) {
  const count = Math.min(72, plantCount(successes));
  const plants = [];
  for (let i = 0; i < count; i++) {
    const random = seeded(i * 713 + 29);
    const bed = Math.floor(i / 24),
      cell = i % 24;
    const x = ((cell % 6) - 2.5) * 0.9 + (random() - 0.5) * 0.12;
    const z = Math.floor(cell / 6) * 0.9 - 0.4;
    plants.push({
      index: i,
      x: bed === 0 ? x - 0.7 : x * 0.7 + (bed === 1 ? -6.1 : 5.7),
      z: bed === 0 ? z + 1.2 : z * 0.7 + (bed === 1 ? -0.4 : -5.3),
      stage:
        i === plantCount(successes) - 1 ? (((successes - 1) % 3) + 1) / 3 : 1,
      type: i % 6,
      seed: random() * 100,
      bed,
    });
  }
  return plants;
}
