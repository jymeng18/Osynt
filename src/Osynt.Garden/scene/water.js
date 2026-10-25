import * as THREE from "three";
import { box, group, sphere } from "./primitives.js";
import { seeded } from "./progression.js";

export function pond(parent) {
  const root = group(parent, [5.1, 0.24, -1.9]);
  const geometry = new THREE.CircleGeometry(1.7, 64);
  geometry.rotateX(-Math.PI / 2);
  geometry.scale(1.15, 1, 0.87);
  const uniforms = { time: { value: 0 }, dusk: { value: 0 } };
  const material = new THREE.ShaderMaterial({
    uniforms,
    transparent: true,
    vertexShader: `varying vec3 vPosition; uniform float time; void main(){vec3 p=position;p.y+=sin(p.x*4.+time)*cos(p.z*3.+time*.8)*.012;vPosition=p;gl_Position=projectionMatrix*modelViewMatrix*vec4(p,1.);}`,
    fragmentShader:
      `varying vec3 vPosition;uniform float time;uniform float dusk;void main(){vec2 p=vPosition.xz;float wave=sin(length(p-vec2(.4,-.2))*20.-time*1.5);float caustic=pow(max(0.,sin(p.x*8.+sin(p.y*8.+time*.3))*cos(p.y*9.+time*.4)),6.);vec3 base=mix(vec3(.42,.65,.59),vec3(.17,.37,.37),dusk);vec3 col=base+caustic*.08+wave*.012;float glint=pow(max(0.,sin(p.x*20.+p.y*11.+time*.7)),35.)*.09;col+=glint;gl_FragColor=vec4(col,.94);#include <tonemapping_fragment> #include <colorspace_fragment>}`.replaceAll(
        "#include",
        "\n#include",
      ),
  });
  const water = new THREE.Mesh(geometry, material);
  water.position.y = 0.04;
  root.add(water);
  const random = seeded(3891);
  for (let i = 0; i < 29; i++) {
    const angle = (i / 29) * Math.PI * 2;
    const rock = sphere(
      root,
      0.19 + random() * 0.08,
      ["#c7ccb0", "#d9d8bd", "#b5bfa4"][i % 3],
      [Math.cos(angle) * 2.02, 0.065, Math.sin(angle) * 1.58],
      [1.3, 0.65, 0.9],
    );
    rock.rotation.y = angle;
  }
  for (const [x, z] of [
    [-0.6, -0.6],
    [0.65, 0.7],
    [0.95, -0.45],
  ]) {
    const pad = new THREE.Mesh(
      new THREE.CircleGeometry(0.22, 12, 0.3, 5.8),
      new THREE.MeshStandardMaterial({
        color: "#9aae70",
        side: THREE.DoubleSide,
        roughness: 0.7,
      }),
    );
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(x, 0.07, z);
    root.add(pad);
    if (x < 0) {
      for (let i = 0; i < 5; i++)
        sphere(
          root,
          0.08,
          "#efc2ba",
          [x + Math.cos(i * 1.26) * 0.07, 0.13, z + Math.sin(i * 1.26) * 0.07],
          [0.7, 0.7, 1.3],
        );
      sphere(root, 0.045, "#e3c28b", [x, 0.19, z]);
    }
  }
  for (let i = 0; i < 5; i++)
    box(
      root,
      [0.4, 0.1, 1.14],
      "#c3ae87",
      [1.54 + i * 0.14, 0.17 + Math.sin((i / 4) * Math.PI) * 0.18, 0],
      0.03,
    );
  for (const z of [-0.54, 0.54])
    for (let i = 0; i < 3; i++)
      box(
        root,
        [0.055, 0.47, 0.055],
        "#a79370",
        [1.56 + i * 0.28, 0.42, z],
        0.015,
      );
  return { root, uniforms };
}
