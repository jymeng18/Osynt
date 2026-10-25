import { build } from "esbuild";
import { execFileSync } from "node:child_process";
import { mkdir, copyFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const output = resolve(root, "../Osynt.Desktop/Assets/Garden");
await mkdir(output, { recursive: true });
await build({
  entryPoints: [resolve(root, "scene/main.js")],
  outfile: resolve(output, "garden.js"),
  bundle: true,
  format: "iife",
  minify: true,
  legalComments: "eof",
  target: "chrome120",
});
execFileSync(
  process.execPath,
  [
    resolve(root, "node_modules/@tailwindcss/cli/dist/index.mjs"),
    "-i",
    resolve(root, "garden.css"),
    "-o",
    resolve(output, "garden.css"),
    "--minify",
  ],
  { cwd: root, stdio: "inherit" },
);
for (const name of ["index.html"])
  await copyFile(resolve(root, name), resolve(output, name));
await copyFile(
  resolve(root, "node_modules/three/LICENSE"),
  resolve(output, "THREE-LICENSE.txt"),
);
console.log(`Bundled offline garden renderer into ${output}`);
