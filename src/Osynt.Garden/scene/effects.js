import * as THREE from "three";
import { group, sphere } from "./primitives.js";
import { seeded } from "./progression.js";

function glowTexture() {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 64;
  const context = canvas.getContext("2d"),
    gradient = context.createRadialGradient(32, 32, 0, 32, 32, 32);
  gradient.addColorStop(0, "#ffffe8");
  gradient.addColorStop(0.15, "#fff5b3");
  gradient.addColorStop(0.4, "#f5de7a66");
  gradient.addColorStop(1, "#efdb6e00");
  context.fillStyle = gradient;
  context.fillRect(0, 0, 64, 64);
  return new THREE.CanvasTexture(canvas);
}

export class GardenEffects {
  constructor(scene) {
    this.scene = scene;
    this.texture = glowTexture();
    this.bursts = [];
    const random = seeded(13),
      count = 65;
    this.dustPositions = new Float32Array(count * 3);
    this.dustSeeds = [];
    for (let i = 0; i < count; i++)
      this.dustSeeds.push({
        x: (random() - 0.5) * 22,
        y: 0.6 + random() * 6,
        z: (random() - 0.5) * 17,
        phase: random() * 6.28,
      });
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute(
      "position",
      new THREE.BufferAttribute(this.dustPositions, 3),
    );
    this.dust = new THREE.Points(
      geometry,
      new THREE.PointsMaterial({
        color: "#f4d591",
        map: this.texture,
        transparent: true,
        opacity: 0.45,
        size: 0.14,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      }),
    );
    scene.add(this.dust);
    this.butterflies = [];
    for (let i = 0; i < 5; i++) {
      const root = group(scene);
      const wings = [];
      sphere(root, 0.04, "#a18e5e", [0, 0, 0], [0.45, 0.5, 2]);
      for (const side of [-1, 1]) {
        const wing = group(root);
        wing.position.x = side * 0.02;
        sphere(
          wing,
          0.13,
          ["#e8cea0", "#eadac0", "#c9b7ce", "#edc1b5", "#e2d28e"][i],
          [side * 0.1, 0, 0.01],
          [1.1, 0.12, 1.4],
        );
        sphere(wing, 0.08, "#f0e6c7", [side * 0.07, 0, -0.13], [1, 0.12, 1]);
        wings.push(wing);
      }
      this.butterflies.push({ root, wings, phase: random() * 10 });
    }
    this.smoke = null;
  }
  burst(position, time) {
    const count = 52,
      positions = new Float32Array(count * 3),
      velocities = [],
      random = seeded(Math.floor(time * 1000) + 187);
    for (let i = 0; i < count; i++) {
      positions.set([position.x, position.y + 0.2, position.z], i * 3);
      const angle = random() * 6.28;
      velocities.push(
        new THREE.Vector3(
          Math.cos(angle) * (0.3 + random()),
          1 + random() * 1.5,
          Math.sin(angle) * (0.3 + random()),
        ),
      );
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    const points = new THREE.Points(
      geometry,
      new THREE.PointsMaterial({
        color: "#ffdd8e",
        map: this.texture,
        size: 0.19,
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      }),
    );
    this.scene.add(points);
    this.bursts.push({
      points,
      velocities,
      origin: position.clone(),
      born: time,
    });
  }
  addSmoke(origin) {
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 64;
    const ctx = canvas.getContext("2d"),
      gradient = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
    gradient.addColorStop(0, "#fff9e95e");
    gradient.addColorStop(1, "#fff9e900");
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, 64, 64);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute(
      "position",
      new THREE.BufferAttribute(new Float32Array(36), 3),
    );
    this.smoke = new THREE.Points(
      geometry,
      new THREE.PointsMaterial({
        map: new THREE.CanvasTexture(canvas),
        size: 0.9,
        transparent: true,
        depthWrite: false,
        opacity: 0.55,
      }),
    );
    this.smokeOrigin = origin;
    this.scene.add(this.smoke);
  }
  update(time, delta, state, dusk) {
    const t = state.reducedMotion ? 3 : time;
    for (let i = 0; i < this.dustSeeds.length; i++) {
      const seed = this.dustSeeds[i];
      this.dustPositions.set(
        [
          seed.x + Math.sin(t * 0.15 + seed.phase) * 0.6,
          seed.y + Math.sin(t * 0.3 + seed.phase) * 0.4,
          seed.z + Math.cos(t * 0.13 + seed.phase) * 0.4,
        ],
        i * 3,
      );
    }
    this.dust.geometry.attributes.position.needsUpdate = true;
    this.dust.material.opacity = 0.25 + dusk * 0.5;
    this.dust.material.size = 0.1 + dusk * 0.18;
    for (let i = 0; i < this.butterflies.length; i++) {
      const butterfly = this.butterflies[i];
      butterfly.root.visible =
        state.successes >= 12 &&
        i < Math.min(5, 1 + Math.floor(state.successes / 24));
      const phase = t * 0.3 + butterfly.phase;
      butterfly.root.position.set(
        Math.sin(phase) * (3 + i * 0.7),
        1.3 + Math.sin(phase * 2.4) * 0.6 + i * 0.17,
        Math.cos(phase * 0.8) * 3,
      );
      butterfly.root.rotation.y = -phase;
      butterfly.wings.forEach(
        (wing, side) =>
          (wing.rotation.z = (side ? 1 : -1) * Math.sin(t * 16 + i) * 0.8),
      );
    }
    for (let i = this.bursts.length - 1; i >= 0; i--) {
      const burst = this.bursts[i],
        age = time - burst.born;
      if (age > 2.7 || state.reducedMotion) {
        this.scene.remove(burst.points);
        burst.points.geometry.dispose();
        burst.points.material.dispose();
        this.bursts.splice(i, 1);
        continue;
      }
      const positions = burst.points.geometry.attributes.position;
      burst.velocities.forEach((velocity, index) =>
        positions.setXYZ(
          index,
          burst.origin.x + velocity.x * age,
          burst.origin.y + velocity.y * age - 0.55 * age * age,
          burst.origin.z + velocity.z * age,
        ),
      );
      positions.needsUpdate = true;
      burst.points.material.opacity = Math.max(0, 1 - age / 2.7);
      burst.points.material.size = 0.15 * (1 - age / 3);
    }
    if (this.smoke) {
      const positions = this.smoke.geometry.attributes.position;
      for (let i = 0; i < positions.count; i++) {
        const age = (t * 0.15 + i / positions.count) % 1;
        positions.setXYZ(
          i,
          this.smokeOrigin.x + age * 0.9 + Math.sin(age * 5 + i) * 0.08,
          this.smokeOrigin.y + age * 2.3,
          this.smokeOrigin.z + age * 0.35,
        );
      }
      positions.needsUpdate = true;
    }
  }
  dispose() {
    this.texture.dispose();
    this.dust.geometry.dispose();
    this.dust.material.dispose();
    for (const burst of this.bursts) {
      burst.points.geometry.dispose();
      burst.points.material.dispose();
    }
    if (this.smoke) {
      this.smoke.geometry.dispose();
      this.smoke.material.map.dispose();
      this.smoke.material.dispose();
    }
  }
}
