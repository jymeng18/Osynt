import * as THREE from "three";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";

const geometries = new Map(),
  materials = new Map();
export function material(color, extra = {}) {
  const key = `${color}:${JSON.stringify(extra)}`;
  if (!materials.has(key))
    materials.set(
      key,
      new THREE.MeshStandardMaterial({ color, roughness: 0.86, ...extra }),
    );
  return materials.get(key);
}
export function mesh(
  parent,
  geometry,
  color,
  position = [0, 0, 0],
  extra = {},
) {
  const object = new THREE.Mesh(
    geometry,
    typeof color === "object" ? color : material(color, extra),
  );
  object.position.set(...position);
  object.castShadow = true;
  object.receiveShadow = true;
  parent.add(object);
  return object;
}
export function box(
  parent,
  size,
  color,
  position = [0, 0, 0],
  radius = 0.06,
  extra = {},
) {
  const key = `b:${size.join(",")}:${radius}`;
  if (!geometries.has(key))
    geometries.set(
      key,
      radius
        ? new RoundedBoxGeometry(...size, 2, radius)
        : new THREE.BoxGeometry(...size),
    );
  return mesh(parent, geometries.get(key), color, position, extra);
}
export function sphere(
  parent,
  radius,
  color,
  position = [0, 0, 0],
  scale = [1, 1, 1],
  extra = {},
) {
  const key = `s:${radius}`;
  if (!geometries.has(key))
    geometries.set(key, new THREE.IcosahedronGeometry(radius, 1));
  const object = mesh(parent, geometries.get(key), color, position, {
    flatShading: true,
    ...extra,
  });
  object.scale.set(...scale);
  return object;
}
export function cylinder(
  parent,
  top,
  bottom,
  height,
  color,
  position = [0, 0, 0],
  sides = 8,
  extra = {},
) {
  const key = `c:${top}:${bottom}:${height}:${sides}`;
  if (!geometries.has(key))
    geometries.set(key, new THREE.CylinderGeometry(top, bottom, height, sides));
  return mesh(parent, geometries.get(key), color, position, extra);
}
export function stem(parent, from, to, radius, color) {
  const a = new THREE.Vector3(...from),
    b = new THREE.Vector3(...to),
    delta = b.clone().sub(a);
  const object = cylinder(
    parent,
    radius * 0.8,
    radius,
    delta.length(),
    color,
    a.clone().add(b).multiplyScalar(0.5).toArray(),
    6,
  );
  object.quaternion.setFromUnitVectors(
    new THREE.Vector3(0, 1, 0),
    delta.normalize(),
  );
  return object;
}
export function group(parent, position = [0, 0, 0]) {
  const object = new THREE.Group();
  object.position.set(...position);
  parent.add(object);
  return object;
}
export function shadow(
  parent,
  radius,
  position = [0, 0.23, 0],
  opacity = 0.14,
) {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 128;
  const context = canvas.getContext("2d"),
    gradient = context.createRadialGradient(64, 64, 5, 64, 64, 64);
  gradient.addColorStop(0, `rgba(40,58,24,${opacity})`);
  gradient.addColorStop(1, "rgba(40,58,24,0)");
  context.fillStyle = gradient;
  context.fillRect(0, 0, 128, 128);
  const texture = new THREE.CanvasTexture(canvas);
  const plane = new THREE.Mesh(
    new THREE.PlaneGeometry(radius * 2, radius * 2),
    new THREE.MeshBasicMaterial({
      map: texture,
      transparent: true,
      depthWrite: false,
      toneMapped: false,
    }),
  );
  plane.material.userData.owned = true;
  plane.userData.ownedGeometry = true;
  plane.rotation.x = -Math.PI / 2;
  plane.position.set(...position);
  parent.add(plane);
  return plane;
}
export function roof(parent, width, height, depth, color, position) {
  const shape = new THREE.Shape();
  shape.moveTo(-width / 2, 0);
  shape.lineTo(0, height);
  shape.lineTo(width / 2, 0);
  shape.closePath();
  const geometry = new THREE.ExtrudeGeometry(shape, {
    depth,
    bevelEnabled: true,
    bevelSegments: 1,
    steps: 1,
    bevelSize: 0.04,
    bevelThickness: 0.04,
  });
  geometry.translate(0, 0, -depth / 2);
  return mesh(parent, geometry, color, position, { flatShading: true });
}
export function disposeOwned(root) {
  // Shared mesh geometries/materials live for the scene's lifetime. Only owned textures are disposed here.
  root.traverse((object) => {
    if (object.material?.userData?.owned) {
      object.material.map?.dispose();
      object.material.dispose();
    }
    if (object.userData.ownedGeometry) object.geometry?.dispose();
  });
}
