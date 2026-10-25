import * as THREE from "three";
import {
  box,
  cylinder,
  disposeOwned,
  group,
  material,
  shadow,
  sphere,
} from "./primitives.js";
import { grass, plant, planter, shrub, tree } from "./flora.js";
import { bench, cottage, fence, greenhouse, lanterns } from "./buildings.js";
import { pond } from "./water.js";
import { layout, plantCount, seeded } from "./progression.js";

export class GardenWorld {
  constructor(scene, effects) {
    this.scene = scene;
    this.effects = effects;
    this.root = group(scene);
    this.features = new Set();
    this.windObjects = [];
    this.windows = [];
    this.lights = [];
    this.water = null;
    this.plantsRoot = group(this.root);
    this.plants = [];
    this.makeIsland();
    this.successes = 0;
  }
  makeIsland() {
    const random = seeded(492);
    // Bevelled turf, warm earth strata, and a soft ground shadow give the island weight.
    box(this.root, [17.6, 0.3, 13.7], "#9dbd7c", [0, 0.03, 0], 0.22);
    box(this.root, [17.45, 0.55, 13.55], "#9ba776", [0, -0.28, 0], 0.3);
    box(this.root, [17.05, 0.55, 13.12], "#b6a57f", [0, -0.73, 0], 0.32);
    box(this.root, [16.3, 0.6, 12.45], "#c6b594", [0, -1.2, 0], 0.36);
    box(this.root, [15.1, 0.4, 11.6], "#cdbd9e", [0, -1.62, 0], 0.3);
    shadow(this.scene, 12, [0, -1.97, 0.5], 0.19).scale.set(1, 1, 0.75);
    const floor = new THREE.Mesh(
      new THREE.PlaneGeometry(200, 200),
      new THREE.ShadowMaterial({ color: "#738362", opacity: 0.08 }),
    );
    floor.rotation.x = -Math.PI / 2;
    floor.position.y = -1.98;
    floor.receiveShadow = true;
    this.scene.add(floor);
    // Soil bed and its little timber edging.
    box(this.root, [5.95, 0.07, 4.04], "#a59170", [-0.7, 0.215, 2.15], 0.2);
    box(this.root, [5.7, 0.035, 3.8], "#ae9975", [-0.7, 0.26, 2.15], 0.16);
    for (const z of [0.16, 4.14])
      box(this.root, [5.95, 0.14, 0.1], "#c6af84", [-0.7, 0.27, z], 0.035);
    for (const x of [-3.63, 2.23])
      box(this.root, [0.1, 0.14, 4.04], "#c6af84", [x, 0.27, 2.15], 0.035);
    for (let i = 0; i < 4; i++)
      box(
        this.root,
        [5.45, 0.02, 0.025],
        "#9c8969",
        [-0.7, 0.29, 0.8 + i * 0.9],
        0.005,
      );
    // A stepping-stone path winds across the island, rather than a perfect grid.
    for (let i = 0; i < 12; i++) {
      const z = -5.5 + i * 0.92,
        x = -2.7 - Math.sin(i * 0.43) * 1.5;
      const stone = cylinder(
        this.root,
        0.38 + random() * 0.13,
        0.4,
        0.06,
        i % 3 ? "#d9d4b6" : "#cbcbae",
        [x, 0.24, z],
        7,
      );
      stone.scale.z = 0.7;
      stone.rotation.y = random() * 3;
    }
    grass(this.root, 500);
    bench(this.root);
    planter(this.root, -4.15, 4.8);
    planter(this.root, 2.9, 4.4, "#b1b798");
    for (const [x, z, size] of [
      [-7, -5.5, 0.65],
      [0.5, -5.5, 0.5],
      [7.4, 5.3, 0.6],
      [-7.5, 1, 0.4],
      [7, -4.8, 0.5],
      [-0.2, -4, 0.35],
    ])
      shrub(this.root, x, z, size);
    for (let i = 0; i < 15; i++) {
      const x = (random() - 0.5) * 16,
        z = (random() - 0.5) * 12;
      if (Math.abs(x) < 3.7 || (x > 3 && z < 0.3 && z > -4.2)) continue;
      const stone = sphere(
        this.root,
        0.13 + random() * 0.18,
        "#c5c9ae",
        [x, 0.22, z],
        [1.4, 0.5, 0.9],
      );
      stone.rotation.y = random() * 5;
    }
    fence(this.root, -8, -6, 7);
    fence(this.root, 3.9, -6, 5);
    fence(this.root, -8.05, 1.8, 5, -Math.PI / 2);
    fence(this.root, 3.9, 5.9, 5);
    fence(this.root, -7.7, 5.9, 4);
    // Ivy and small stones break up the clean edge of the diorama.
    for (let i = 0; i < 13; i++) {
      const x = -7.3 + i * 1.2;
      sphere(this.root, 0.18, "#9aaa79", [x, -0.1, 6.82], [0.8, 0.6, 0.8]);
      if (i % 3 === 0)
        for (let j = 0; j < 4; j++)
          sphere(
            this.root,
            0.11,
            "#91a472",
            [x + Math.sin(j) * 0.08, -0.2 - j * 0.19, 6.77],
            [0.7, 1.1, 0.6],
          );
    }
    // A tiny robin watches over the first empty patch.
    const bird = group(this.root, [-7.2, 1.05, 5.9]);
    sphere(bird, 0.16, "#ad9d7b", [0, 0, 0], [1, 1, 1.25]);
    sphere(bird, 0.12, "#c2ab83", [0, 0.15, 0.09]);
    sphere(bird, 0.075, "#cfaa87", [0, 0.04, 0.16], [1, 1.1, 0.6]);
    sphere(bird, 0.018, "#494d3c", [0.07, 0.19, 0.17]);
  }
  updateProgress(state, time, celebrate) {
    const count = state.successes;
    this.successes = count;
    for (const child of [...this.plantsRoot.children]) {
      disposeOwned(child);
      this.plantsRoot.remove(child);
    }
    this.plants = layout(count).map((descriptor) =>
      plant(
        this.plantsRoot,
        descriptor,
        count,
        celebrate && descriptor.index === Math.min(71, plantCount(count) - 1)
          ? time
          : -100,
      ),
    );
    this.unlock(5, count, () => {
      for (const [i, x, z] of [
        [0, -7, 2],
        [1, -6.6, 1.8],
        [2, -7.3, 2.4],
        [3, 3.5, -4.9],
        [4, 3.9, -5.2],
        [5, 4.3, -4.9],
      ]) {
        const flower = plant(
          this.root,
          { index: 100 + i, x, z, stage: 1, type: i % 6 },
          count,
        );
        this.windObjects.push(flower);
      }
    });
    this.unlock(12, count, () => {
      this.windObjects.push(
        tree(this.root, 0.3, -4.5, 31, true, 0.95),
        tree(this.root, 7.1, -4.45, 13, true, 0.8),
      );
    });
    this.unlock(24, count, () => {
      this.water = pond(this.root);
    });
    this.unlock(40, count, () => {
      const home = cottage(this.root);
      this.windows = home.windows;
      this.effects.addSmoke(home.smokeOrigin);
    });
    this.unlock(75, count, () => greenhouse(this.root));
    this.unlock(120, count, () => (this.lights = lanterns(this.root)));
    for (let i = 0; i < 4; i++)
      this.unlock(200 + i * 160, count, () => this.satellite(i));
    if (celebrate && count > 0 && !state.reducedMotion) {
      const descriptor =
        layout(count)[(plantCount(count) - 1) % this.plants.length];
      this.effects.burst(
        new THREE.Vector3(descriptor.x, 0.6, descriptor.z),
        time,
      );
    }
  }
  unlock(at, count, build) {
    if (count >= at && !this.features.has(at)) {
      this.features.add(at);
      build();
    }
  }
  satellite(index) {
    const positions = [
        [12.2, -1.2, -3.8],
        [-11.6, -0.5, -6],
        [10.5, -0.7, 8.7],
        [-11.4, -1, 5.6],
      ],
      root = group(this.root, positions[index]);
    cylinder(root, 2.5, 1.5, 1.3, "#b9aa87", [0, -0.5, 0], 7);
    cylinder(root, 2.56, 2.5, 0.2, "#afc68e", [0, 0.2, 0], 7);
    this.windObjects.push(tree(root, -0.4, -0.2, index * 15 + 4, true, 0.65));
    for (let i = 0; i < 5; i++)
      this.windObjects.push(
        plant(
          root,
          {
            index: index * 5 + 200,
            x: Math.sin(i * 1.26) * 1.7,
            z: Math.cos(i * 1.26) * 1.7,
            stage: 1,
            type: i,
          },
          this.successes,
        ),
      );
    sphere(root, 0.23, "#d0cfb0", [1.2, 0.4, 0], [1, 0.6, 1]);
  }
  animate(time, state, dusk) {
    for (const object of [...this.plants, ...this.windObjects]) {
      const wind = state.reducedMotion
        ? 0
        : Math.sin(time * 1.1 + object.userData.wind) * 0.035;
      if (object.userData.canopy)
        object.userData.canopy.rotation.z = wind * 0.6;
      else object.rotation.z = wind;
      if (object.userData.growAt !== undefined) {
        const age = time - object.userData.growAt;
        const growth =
          state.reducedMotion || age > 2
            ? 1
            : 1 - Math.exp(-age * 4) * Math.cos(age * 7);
        object.scale.setScalar(
          Math.max(0.01, growth) * object.userData.baseScale,
        );
      }
    }
    for (const window of this.windows)
      window.emissiveIntensity = 0.1 + dusk * 1.4;
    for (const light of this.lights) light.intensity = dusk * 2;
    if (this.water) {
      this.water.uniforms.time.value = state.reducedMotion ? 0 : time;
      this.water.uniforms.dusk.value = dusk;
    }
  }
  pick(raycaster) {
    const intersection = raycaster
      .intersectObjects(this.root.children, true)
      .find((hit) => {
        let object = hit.object;
        while (object && !object.userData.plant) object = object.parent;
        return !!object?.userData.plant;
      });
    if (!intersection) return null;
    let object = intersection.object;
    while (!object.userData.plant) object = object.parent;
    return object.userData.plant;
  }
}
