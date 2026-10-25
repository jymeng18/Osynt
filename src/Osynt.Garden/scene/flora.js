import * as THREE from "three";
import {
  box,
  cylinder,
  group,
  material,
  shadow,
  sphere,
  stem,
} from "./primitives.js";
import { seeded } from "./progression.js";

const flowers = [
  {
    name: "Sunbeam daisy",
    color: "#fff3c9",
    center: "#d4a35c",
    detail: "A tiny piece of sunshine. Grown one good response at a time.",
  },
  {
    name: "Blush cosmos",
    color: "#e5a4ac",
    center: "#d8b26b",
    detail: "Soft petals, strong roots. A reminder to keep making things.",
  },
  {
    name: "Lavender",
    color: "#ad9ac6",
    center: "#bdaed4",
    detail: "A quiet little bloom for a busy day.",
  },
  {
    name: "Golden marigold",
    color: "#e9ba66",
    center: "#ac7940",
    detail: "Warm, bright, and a little bit wild.",
  },
  {
    name: "Blue forget-me-not",
    color: "#9dc9d0",
    center: "#f1d884",
    detail: "A small discovery worth keeping.",
  },
  {
    name: "Strawberry",
    color: "#fff5d7",
    center: "#e1b95f",
    detail: "Sweet things grow from steady little efforts.",
  },
];

export function plant(parent, descriptor, successCount, animationTime = -100) {
  const random = seeded(descriptor.index * 73 + 19),
    species = flowers[descriptor.type];
  const root = group(parent, [descriptor.x, 0.23, descriptor.z]);
  root.userData.plant = {
    name:
      descriptor.stage < 1
        ? `${species.name} · ${descriptor.stage < 0.5 ? "seedling" : "sprout"}`
        : species.name,
    detail: species.detail,
    index: descriptor.index,
  };
  root.userData.wind = random() * 6.28;
  root.userData.growAt = animationTime;
  const size = 0.7 + random() * 0.4,
    height = (0.48 + random() * 0.18) * descriptor.stage;
  root.userData.baseScale = size;
  root.scale.setScalar(size);
  shadow(root, 0.37, [0, 0.015, 0], 0.16);
  const stalk = group(root);
  stalk.userData.stalk = true;
  stem(stalk, [0, 0, 0], [0.025, height, 0], 0.022, "#76945b");
  for (let side = -1; side <= 1; side += 2) {
    const leaf = sphere(
      stalk,
      0.17,
      side < 0 ? "#87a664" : "#a2b777",
      [side * 0.105, height * 0.45, 0],
      [1.1, 0.22, 0.55],
    );
    leaf.rotation.z = side * 0.4;
    leaf.rotation.y = side * 0.35;
  }
  if (descriptor.stage > 0.5) {
    if (descriptor.type === 2) {
      for (let i = 0; i < 5; i++) {
        const blossom = sphere(
          stalk,
          0.08,
          i % 2 ? "#b5a3cd" : "#c6b8da",
          [((i % 2) - 0.5) * 0.07, height + i * 0.065, 0],
          [0.8, 1.25, 0.8],
        );
        blossom.rotation.y = i * 1.2;
      }
    } else {
      const head = group(stalk, [0.025, height, 0]);
      head.rotation.z = (random() - 0.5) * 0.25;
      for (let i = 0; i < 6; i++) {
        const angle = (i * Math.PI) / 3;
        const petal = sphere(
          head,
          0.115,
          species.color,
          [Math.cos(angle) * 0.12, 0.015, Math.sin(angle) * 0.12],
          [0.8, 0.35, 1.4],
        );
        petal.rotation.y = -angle + Math.PI / 2;
      }
      sphere(head, 0.075, species.center, [0, 0.055, 0], [1, 0.55, 1]);
      if (descriptor.type === 5 && descriptor.stage === 1) {
        sphere(stalk, 0.105, "#c77e6b", [0.16, 0.13, 0.08], [0.8, 1.2, 0.8]);
        sphere(stalk, 0.08, "#db9d85", [-0.13, 0.2, -0.07], [0.8, 1.2, 0.8]);
      }
    }
  }
  return root;
}

export function tree(parent, x, z, seed, fruit = true, scale = 1) {
  const random = seeded(seed),
    root = group(parent, [x, 0.22, z]);
  root.scale.setScalar(scale);
  root.userData.wind = random() * Math.PI * 2;
  root.userData.tree = true;
  shadow(root, 1.65, [0, 0.02, 0], 0.18);
  stem(root, [0, 0, 0], [0.08, 1.9, 0], 0.16, "#a38c67");
  stem(root, [0, 1, 0], [-0.55, 1.8, 0.1], 0.065, "#a38c67");
  stem(root, [0.04, 1.2, 0], [0.65, 2, -0.1], 0.07, "#a38c67");
  const canopy = group(root, [0, 1.8, 0]);
  root.userData.canopy = canopy;
  const colors = ["#91ac70", "#a1b77d", "#bac88c", "#839d64", "#adbf81"];
  for (let i = 0; i < 10; i++) {
    const angle = i * 2.4,
      radius = i < 7 ? 0.65 : 0.3;
    sphere(
      canopy,
      0.78 + random() * 0.13,
      colors[i % colors.length],
      [Math.cos(angle) * radius, random() * 0.85, Math.sin(angle) * radius],
      [1.05, 0.88, 1],
    );
  }
  if (fruit)
    for (let i = 0; i < 7; i++) {
      const angle = i * 2.4;
      const point = [
        Math.cos(angle) * 0.99,
        0.2 + random() * 0.6,
        Math.sin(angle) * 0.95,
      ];
      sphere(canopy, 0.14, i % 3 === 0 ? "#e2b773" : "#d39a86", point);
      const leaf = sphere(
        canopy,
        0.1,
        "#6d8e57",
        [point[0] + 0.04, point[1] + 0.14, point[2]],
        [1, 0.15, 0.5],
      );
      leaf.rotation.z = 0.5;
    }
  return root;
}

export function shrub(parent, x, z, size = 0.45, color = "#95aa73") {
  const root = group(parent, [x, 0.3, z]);
  for (let i = 0; i < 3; i++)
    sphere(
      root,
      size,
      color,
      [(i - 1) * size * 0.55, size * 0.35, (i % 2) * size * 0.3],
      [1, 0.75, 1],
    );
  return root;
}

export function grass(parent, count = 500) {
  const shape = new THREE.Shape();
  shape.moveTo(-0.028, 0);
  shape.quadraticCurveTo(0.03, 0.13, 0.02, 0.27);
  shape.quadraticCurveTo(0.07, 0.13, 0.028, 0);
  const geometry = new THREE.ShapeGeometry(shape),
    grassMaterial = material("#abc386", { side: THREE.DoubleSide });
  const batch = new THREE.InstancedMesh(geometry, grassMaterial, count),
    dummy = new THREE.Object3D(),
    random = seeded(829);
  let accepted = 0;
  while (accepted < count) {
    const x = (random() - 0.5) * 16.5,
      z = (random() - 0.5) * 12;
    // Keep the cultivated beds, path, pond, and cottage sites clear.
    if (
      (Math.abs(x + 0.7) < 3.4 && z > 0.05 && z < 4.9) ||
      (x > 3.3 && z > -4.3 && z < 0.3) ||
      (x < -3.3 && z < -2.2) ||
      Math.abs(x + 1) < 0.6
    )
      continue;
    dummy.position.set(x, 0.22, z);
    dummy.rotation.set(0, random() * Math.PI * 2, (random() - 0.5) * 0.25);
    const size = 0.6 + random() * 0.65;
    dummy.scale.setScalar(size);
    dummy.updateMatrix();
    batch.setMatrixAt(accepted, dummy.matrix);
    batch.setColorAt(
      accepted,
      new THREE.Color(
        ["#afc28a", "#9fb97d", "#bfcf96", "#8ea96f"][accepted % 4],
      ),
    );
    accepted++;
  }
  batch.receiveShadow = true;
  parent.add(batch);
  return batch;
}

export function planter(parent, x, z, color = "#c5a484") {
  const root = group(parent, [x, 0.22, z]);
  cylinder(root, 0.28, 0.2, 0.4, color, [0, 0.2, 0]);
  cylinder(root, 0.31, 0.31, 0.09, color, [0, 0.42, 0]);
  cylinder(root, 0.25, 0.25, 0.025, "#8d795b", [0, 0.46, 0]);
  for (let i = 0; i < 4; i++) {
    const leaf = sphere(
      root,
      0.18,
      "#9fb77e",
      [Math.cos(i * 1.57) * 0.1, 0.58, Math.sin(i * 1.57) * 0.1],
      [0.7, 1.1, 0.35],
    );
    leaf.rotation.z = ((i % 2) - 0.5) * 0.7;
  }
  return root;
}
