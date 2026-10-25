import { test } from "node:test";
import assert from "node:assert/strict";
import {
  growthMessage,
  layout,
  milestones,
  normalizeState,
  plantCount,
  seeded,
} from "../scene/progression.js";

test("each group of three successes grows a plant from seed to bloom", () => {
  assert.equal(plantCount(0), 0);
  assert.equal(plantCount(1), 1);
  assert.equal(plantCount(3), 1);
  assert.equal(plantCount(4), 2);
  assert.equal(layout(1)[0].stage, 1 / 3);
  assert.equal(layout(2)[0].stage, 2 / 3);
  assert.equal(layout(3)[0].stage, 1);
});
test("garden placement is deterministic across restarts", () => {
  assert.deepEqual(layout(75), layout(75));
  assert.deepEqual(layout(75).slice(0, 20), layout(90).slice(0, 20));
  assert.equal(seeded(73)(), seeded(73)());
});
test("large lifetime counts retain progress while geometry is bounded", () => {
  assert.equal(plantCount(3_000_000), 1_000_000);
  assert.equal(layout(3_000_000).length, 72);
});
test("renderer state and milestone messages are predictable", () => {
  assert.deepEqual(normalizeState({ successes: -20, atmosphere: "invalid" }), {
    successes: 0,
    atmosphere: "day",
    reducedMotion: false,
  });
  assert.match(growthMessage(40), /cottage/);
  assert.deepEqual(
    milestones.map((m) => m[0]),
    [1, 5, 12, 24, 40, 75, 120, 200],
  );
});
