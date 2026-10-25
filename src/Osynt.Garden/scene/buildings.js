import * as THREE from "three";
import {
  box,
  cylinder,
  group,
  material,
  roof,
  shadow,
  sphere,
  stem,
} from "./primitives.js";
import { planter } from "./flora.js";

export function cottage(parent) {
  const root = group(parent, [-5.2, 0.25, -3.4]);
  root.rotation.y = -0.12;
  shadow(root, 2.5, [0, 0.01, 0], 0.2);
  box(root, [3.2, 0.2, 2.7], "#b6ae8c", [0, 0.1, 0.1], 0.1);
  box(root, [2.9, 2.25, 2.35], "#eee0b9", [0, 1.25, 0], 0.06);
  box(root, [3, 0.19, 2.45], "#c8b68d", [0, 0.38, 0], 0.03);
  roof(root, 3.55, 1.3, 2.95, "#b78672", [0, 2.4, 0]);
  // Roof seams, solid timber, and a warm little chimney.
  for (let side = -1; side <= 1; side += 2)
    for (let i = 0; i < 4; i++) {
      const strip = box(
        root,
        [0.055, 0.045, 3.02],
        "#c5947b",
        [side * (0.25 + i * 0.43), 3.55 - i * 0.32, 0],
        0.015,
      );
      strip.rotation.z = side * -0.65;
    }
  box(root, [0.45, 1.1, 0.5], "#c6b498", [0.82, 3.05, -0.35], 0.035);
  box(root, [0.57, 0.16, 0.61], "#a59a81", [0.82, 3.6, -0.35], 0.03);
  box(root, [0.65, 1.35, 0.09], "#9e8462", [0, 1.03, 1.22], 0.055);
  box(root, [0.48, 1.13, 0.05], "#b39b73", [0, 1, 1.28], 0.06);
  sphere(root, 0.04, "#d9bf78", [0.18, 1.03, 1.34]);
  const windows = [];
  for (const x of [-0.93, 0.93]) {
    box(root, [0.64, 0.72, 0.11], "#a68e68", [x, 1.56, 1.22], 0.03);
    const window = box(
      root,
      [0.49, 0.55, 0.045],
      "#efd78f",
      [x, 1.56, 1.29],
      0.015,
      { emissive: "#edbb66", emissiveIntensity: 0.12 },
    );
    window.material = window.material.clone();
    windows.push(window.material);
    box(root, [0.038, 0.56, 0.055], "#b09b73", [x, 1.56, 1.33], 0.005);
    box(root, [0.5, 0.038, 0.055], "#b09b73", [x, 1.56, 1.33], 0.005);
    box(root, [0.77, 0.09, 0.28], "#bca683", [x, 1.17, 1.25], 0.025);
  }
  box(root, [1.5, 0.14, 0.65], "#c8b798", [0, 0.28, 1.55], 0.045);
  box(root, [1.65, 0.09, 0.42], "#d7caab", [0, 0.12, 1.94], 0.04);
  planter(root, -1.43, 1.34);
  planter(root, 1.42, 1.32, "#a6b09b");
  return { root, windows, smokeOrigin: new THREE.Vector3(-4.4, 3.95, -3.75) };
}

export function greenhouse(parent) {
  const root = group(parent, [5.6, 0.25, 3.5]);
  root.rotation.y = -0.15;
  shadow(root, 1.9, [0, 0.01, 0], 0.16);
  box(root, [2.65, 0.18, 2.25], "#c6bca1", [0, 0.09, 0], 0.08);
  const glass = new THREE.MeshPhysicalMaterial({
    color: "#d7e7d1",
    roughness: 0.12,
    metalness: 0.05,
    transparent: true,
    opacity: 0.29,
    side: THREE.DoubleSide,
    depthWrite: false,
    clearcoat: 1,
  });
  box(root, [2.45, 1.75, 2.05], glass, [0, 1.04, 0], 0.02);
  roof(root, 2.5, 0.85, 2.12, glass, [0, 1.91, 0]);
  const frame = "#ebe4c8";
  for (const x of [-1.24, 0, 1.24]) {
    for (const z of [-1.04, 1.04])
      stem(root, [x, 0.15, z], [x, 1.94, z], 0.035, frame);
    stem(root, [x, 1.94, -1.04], [0, 2.76, -1.04], 0.032, frame);
    stem(root, [x, 1.94, 1.04], [0, 2.76, 1.04], 0.032, frame);
  }
  for (const y of [0.2, 1.05, 1.93])
    for (const z of [-1.04, 1.04])
      stem(root, [-1.24, y, z], [1.24, y, z], 0.03, frame);
  for (const x of [-1.24, 0, 1.24])
    stem(
      root,
      [x, 1.94 + (x === 0 ? 0.83 : 0), -1.06],
      [x, 1.94 + (x === 0 ? 0.83 : 0), 1.06],
      0.035,
      frame,
    );
  for (const x of [-0.75, 0.65])
    for (const z of [-0.5, 0.5]) planter(root, x, z, "#c5a487");
  return root;
}

export function fence(parent, x, z, length, rotation = 0) {
  const root = group(parent, [x, 0.22, z]);
  root.rotation.y = rotation;
  const color = "#ded7b7";
  for (let i = 0; i <= length; i++) {
    box(root, [0.13, 0.77, 0.13], color, [i * 0.72, 0.39, 0], 0.03);
    sphere(root, 0.09, "#eee5c9", [i * 0.72, 0.8, 0], [1, 0.55, 1]);
  }
  box(
    root,
    [length * 0.72 + 0.1, 0.11, 0.08],
    color,
    [length * 0.36, 0.3, 0],
    0.02,
  );
  box(
    root,
    [length * 0.72 + 0.1, 0.1, 0.08],
    color,
    [length * 0.36, 0.61, 0],
    0.02,
  );
  return root;
}

export function bench(parent) {
  const root = group(parent, [-6.5, 0.22, 3.2]);
  root.rotation.y = 0.3;
  for (const x of [-0.56, 0.56]) {
    box(root, [0.1, 0.55, 0.1], "#a89372", [x, 0.27, 0.18], 0.02);
    box(root, [0.1, 0.9, 0.1], "#a89372", [x, 0.45, -0.2], 0.02);
  }
  for (let i = 0; i < 3; i++)
    box(root, [1.5, 0.09, 0.15], "#cbb28b", [0, 0.55, (i - 1) * 0.17], 0.02);
  for (let i = 0; i < 2; i++)
    box(root, [1.5, 0.17, 0.08], "#cbb28b", [0, 0.79 + i * 0.2, -0.25], 0.02);
  return root;
}

export function lanterns(parent) {
  const lights = [];
  for (const [x, z] of [
    [-3.5, 4.5],
    [3.7, 3.9],
    [2, -4.6],
    [-6.7, 0.3],
  ]) {
    cylinder(parent, 0.04, 0.07, 2, "#a29170", [x, 1.22, z], 6);
    box(parent, [0.34, 0.45, 0.34], "#e9ca86", [x, 2.03, z], 0.045, {
      emissive: "#ffd18a",
      emissiveIntensity: 1.2,
    });
    box(parent, [0.42, 0.08, 0.42], "#a59471", [x, 2.3, z], 0.04);
    const light = new THREE.PointLight("#ffd18a", 0, 3, 2);
    light.position.set(x, 2, z);
    parent.add(light);
    lights.push(light);
  }
  return lights;
}
