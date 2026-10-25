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
  await page
    .getByRole("button", { name: "Close milestones", exact: true })
    .click();
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
    "PASS WebGL rendering, milestone features, orbit camera, atmosphere, reduced motion, growth particles, and zero shader errors",
  );
  console.log(`Previews saved in ${output}`);
} finally {
  await browser?.close();
  await new Promise((resolve) => server.close(resolve));
}
