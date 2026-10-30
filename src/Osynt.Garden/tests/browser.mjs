import { chromium } from "playwright";
import { createServer } from "node:http";
import { readFile, mkdir } from "node:fs/promises";
import { resolve, dirname, extname } from "node:path";
import { fileURLToPath } from "node:url";
import assert from "node:assert/strict";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const assets = resolve(root, "src/Osynt.Desktop/Assets/Garden");
const output = resolve(root, "artifacts/garden");
const server = createServer(async (request, response) => {
  try {
    const name =
      request.url === "/"
        ? "index.html"
        : new URL(request.url, "http://localhost").pathname.slice(1);
    if (!["index.html", "garden.js", "garden.css"].includes(name)) {
      response.writeHead(404);
      response.end();
      return;
    }
    response.setHeader(
      "Content-Type",
      { ".html": "text/html", ".js": "text/javascript", ".css": "text/css" }[
        extname(name)
      ],
    );
    response.end(await readFile(resolve(assets, name)));
  } catch {
    response.writeHead(500);
    response.end();
  }
});
await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
let browser;
try {
  await mkdir(output, { recursive: true });
  browser = await chromium.launch({
    headless: true,
    args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"],
  });
  const page = await browser.newPage({
    viewport: { width: 1120, height: 760 },
    deviceScaleFactor: 1,
  });
  const errors = [];
  // Exercise the same message contract used by the embedded Windows WebView.
  await page.addInitScript(() => {
    window.hostMessages = [];
    window.chrome ??= {};
    window.chrome.webview = {
      postMessage: (message) => window.hostMessages.push(message),
      addEventListener: () => {},
    };
  });
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error" && !message.text().includes("favicon"))
      errors.push(message.text());
  });
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  await page.waitForFunction(() => window.garden?.info().frames > 2);
  assert.match(
    (await page.evaluate(() => window.garden.info())).renderer,
    /WebGL/,
  );
  await page.evaluate(() =>
    window.garden.receive({ type: "state", successes: 75, atmosphere: "day" }),
  );
  await page.waitForFunction(() => window.garden.info().features.includes(75));
  await page.waitForTimeout(1500);
  await page.evaluate(() => window.garden.freeze(6));
  await page.screenshot({ path: resolve(output, "garden-day.png") });
  assert.equal(await page.locator("#success-count").textContent(), "75");
  assert.equal(await page.locator("#plant-count").textContent(), "25");
  const beforeZoom = await page.evaluate(() => window.garden.info().camera);
  const originalZoom = await page.evaluate(() => window.garden.info().zoom);
  await page.getByRole("button", { name: "Zoom in", exact: true }).click();
  assert.ok(
    (await page.evaluate(() => window.garden.info().zoom)) > originalZoom,
  );
  await page.getByRole("button", { name: "Zoom out", exact: true }).click();
  assert.ok(
    Math.abs(
      (await page.evaluate(() => window.garden.info().zoom)) - originalZoom,
    ) < 0.001,
  );
  await page.locator("#scene").hover();
  await page.mouse.move(500, 360);
  await page.mouse.down();
  await page.mouse.move(620, 400, { steps: 12 });
  await page.mouse.up();
  await page.evaluate(() => {
    window.garden.resume();
  });
  await page.waitForTimeout(250);
  const afterOrbit = await page.evaluate(() => window.garden.info().camera);
  assert.notDeepEqual(beforeZoom, afterOrbit);
  await page.getByRole("button", { name: "Reset camera", exact: true }).click();
  await page.waitForTimeout(300);
  const resetCamera = await page.evaluate(() => window.garden.info());
  assert.ok(
    resetCamera.camera.every(
      (value, index) => Math.abs(value - beforeZoom[index]) < 0.01,
    ),
    `Reset must stop orbit inertia and restore the starting view: ${resetCamera.camera} vs ${beforeZoom}`,
  );
  assert.equal(resetCamera.zoom, originalZoom);
  await page.getByRole("button", { name: "Dusk", exact: true }).click();
  await page.waitForTimeout(1500);
  await page.evaluate(() => window.garden.freeze(8));
  await page.screenshot({ path: resolve(output, "garden-dusk.png") });
  assert.equal(
    (await page.evaluate(() => window.garden.info())).atmosphere,
    "dusk",
  );
  await page
    .getByRole("button", { name: "Reduce motion", exact: true })
    .click();
  assert.equal(
    (await page.evaluate(() => window.garden.info())).reducedMotion,
    true,
  );
  await page.getByRole("button", { name: "Milestones", exact: false }).click();
  assert.equal(await page.locator("#milestones").evaluate((d) => d.open), true);
  await page.setViewportSize({ width: 756, height: 480 });
  await page.screenshot({ path: resolve(output, "milestones-compact.png") });
  const dialog = page.getByRole("dialog", { name: "Garden milestones" });
  assert.equal(await dialog.isVisible(), true);
  assert.equal(await dialog.getByRole("listitem").count(), 8);
  const list = page.getByRole("list", { name: "Garden unlocks" });
  await list.getByRole("listitem").last().scrollIntoViewIfNeeded();
  assert.equal(await list.evaluate((d) => d.scrollTop > 0), true);
  for (const locator of [
    dialog,
    page.locator("#close-milestones"),
    page.locator("#milestones-help"),
  ]) {
    const bounds = await locator.boundingBox();
    assert.ok(bounds && bounds.y >= 0 && bounds.y + bounds.height <= 480);
  }
  await page.keyboard.press("Control+Enter");
  assert.equal(
    await page.evaluate(() =>
      window.hostMessages.some((m) => m.type === "send"),
    ),
    false,
  );
  await page
    .getByRole("button", { name: "Close milestones", exact: true })
    .click();
  await page.setViewportSize({ width: 1120, height: 760 });
  assert.equal(
    await page.locator("#milestones").evaluate((d) => d.open),
    false,
  );
  assert.equal(
    await page
      .locator("#open-milestones")
      .evaluate((d) => d === document.activeElement),
    true,
  );
  await page.getByRole("button", { name: "Milestones", exact: false }).click();
  await page.keyboard.press("Escape");
  assert.equal(
    await page.locator("#milestones").evaluate((d) => d.open),
    false,
  );
  assert.equal(
    await page.evaluate(
      () => window.hostMessages.filter((m) => m.type === "back").length,
    ),
    0,
  );
  await page
    .getByRole("button", { name: "Back to requests", exact: true })
    .click();
  await page.keyboard.press("Control+Enter");
  const messages = await page.evaluate(() => window.hostMessages);
  assert.ok(messages.some((m) => m.type === "ready"));
  assert.ok(messages.some((m) => m.type === "back"));
  assert.ok(messages.some((m) => m.type === "send"));
  assert.ok(
    messages.some(
      (m) =>
        m.type === "preferences" && m.atmosphere === "dusk" && m.reducedMotion,
    ),
  );
  await page
    .getByRole("button", { name: "Reduce motion", exact: true })
    .click();
  await page.evaluate(() => {
    window.garden.resume();
    window.garden.receive({ type: "state", successes: 76, atmosphere: "day" });
  });
  assert.ok((await page.evaluate(() => window.garden.info())).particles > 0);
  await page.waitForTimeout(700);
  await page.screenshot({ path: resolve(output, "garden-growth.png") });
  assert.equal(errors.length, 0, errors.join("\n"));
  console.log(
    "PASS WebGL rendering, camera zoom/reset, compact milestones and keyboard focus, host messages, atmosphere, reduced motion, growth particles, and zero shader errors",
  );
  console.log(`Previews saved in ${output}`);
} finally {
  await browser?.close();
  await new Promise((resolve) => server.close(resolve));
}
