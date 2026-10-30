import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { EffectComposer } from "three/addons/postprocessing/EffectComposer.js";
import { RenderPass } from "three/addons/postprocessing/RenderPass.js";
import { UnrealBloomPass } from "three/addons/postprocessing/UnrealBloomPass.js";
import { OutputPass } from "three/addons/postprocessing/OutputPass.js";
import { GardenWorld } from "./world.js";
import { GardenEffects } from "./effects.js";
import { normalizeState } from "./progression.js";
import { createUi } from "./ui.js";

const canvas = document.getElementById("scene"),
  container = document.getElementById("garden");
let renderer,
  controls,
  composer,
  world,
  effects,
  ui,
  frame = 0,
  lastFrame = 0,
  elapsed = 0,
  dusk = 0;
let state = normalizeState(),
  active = true,
  hasState = false,
  frozen = false;
const scene = new THREE.Scene(),
  camera = new THREE.OrthographicCamera(-13, 13, 10, -10, 0.1, 150);
const dayColor = new THREE.Color("#e5ecde"),
  duskColor = new THREE.Color("#354b51");
scene.background = dayColor.clone();
scene.fog = new THREE.Fog(dayColor.clone(), 38, 85);
const sun = new THREE.DirectionalLight("#fff1ce", 2.4);
sun.position.set(-12, 24, 9);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -22;
sun.shadow.camera.right = 22;
sun.shadow.camera.top = 20;
sun.shadow.camera.bottom = -20;
sun.shadow.camera.near = 0.1;
sun.shadow.camera.far = 55;
sun.shadow.normalBias = 0.025;
sun.shadow.bias = -0.00005;
sun.shadow.radius = 3;
scene.add(sun);
scene.add(sun.target);
const sky = new THREE.HemisphereLight("#fcf4dc", "#a1b590", 1.65);
scene.add(sky);
const fill = new THREE.DirectionalLight("#c4dec5", 0.7);
fill.position.set(10, 8, -12);
scene.add(fill);
const raycaster = new THREE.Raycaster(),
  mouse = new THREE.Vector2();

function resetCamera() {
  // Flush pending orbit inertia before restoring the view, or it keeps moving after reset.
  if (controls) {
    const damping = controls.enableDamping;
    controls.enableDamping = false;
    controls.update();
    controls.enableDamping = damping;
  }
  camera.position.set(21, 23, 28);
  camera.zoom = 1.12;
  camera.lookAt(0, 0.2, 0);
  controls?.target.set(0, 0.2, 0);
  camera.updateProjectionMatrix();
  controls?.update();
}
function resize() {
  if (!renderer) return;
  const width = Math.max(1, container.clientWidth),
    height = Math.max(1, container.clientHeight),
    aspect = width / height;
  camera.left = -10.5 * aspect;
  camera.right = 10.5 * aspect;
  camera.top = 10.5;
  camera.bottom = -10.5;
  camera.updateProjectionMatrix();
  renderer.setSize(width, height, false);
  composer.setSize(width, height);
  renderOnce();
}
function setPreferences(atmosphere, reducedMotion) {
  receive({ type: "state", ...state, atmosphere, reducedMotion });
  ui.notifyHost({ type: "preferences", atmosphere, reducedMotion });
}
function receive(message) {
  if (message.type === "visibility") {
    active = message.visible !== false;
    schedule();
    return;
  }
  if (message.type !== "state") return;
  const next = normalizeState(message),
    changed = !hasState || next.successes !== state.successes;
  const celebrate = hasState && next.successes > state.successes;
  state = next;
  hasState = true;
  if (changed && world) world.updateProgress(state, elapsed, celebrate);
  ui?.update(state, celebrate);
  schedule();
}
function updateLighting(delta) {
  const target = state.atmosphere === "dusk" ? 1 : 0;
  dusk =
    state.reducedMotion || frozen
      ? target
      : THREE.MathUtils.damp(dusk, target, 2, delta);
  scene.background.copy(dayColor).lerp(duskColor, dusk);
  scene.fog.color.copy(scene.background);
  sun.intensity = 2.4 - dusk * 1.8;
  sky.intensity = 1.65 - dusk * 0.75;
  fill.intensity = 0.7 - dusk * 0.3;
  sun.color.set(dusk > 0.4 ? "#edc397" : "#fff1ce");
  renderer.toneMappingExposure = 1.04 - dusk * 0.12;
}
function renderOnce(delta = 0.016) {
  if (!renderer) return;
  updateLighting(delta);
  controls.update();
  world.animate(elapsed, state, dusk);
  effects.update(elapsed, delta, state, dusk);
  composer.render();
}
function animate(now) {
  frame = 0;
  if (!active || document.hidden || frozen) return;
  const delta = Math.min(0.05, Math.max(0.001, (now - lastFrame) / 1000));
  lastFrame = now;
  elapsed += delta;
  renderOnce(delta);
  schedule();
}
function schedule() {
  if (active && !document.hidden && !frozen && !frame && renderer)
    frame = requestAnimationFrame(animate);
}

ui = createUi({
  preferences: setPreferences,
  reset: resetCamera,
  zoom: (factor) => {
    camera.zoom = THREE.MathUtils.clamp(camera.zoom * factor, 0.55, 2.1);
    camera.updateProjectionMatrix();
  },
});
try {
  renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: false,
    powerPreference: "high-performance",
  });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.75));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.dampingFactor = 0.055;
  controls.enablePan = false;
  controls.minPolarAngle = 0.35;
  controls.maxPolarAngle = Math.PI / 2.45;
  controls.minZoom = 0.55;
  controls.maxZoom = 2.1;
  controls.rotateSpeed = 0.55;
  controls.zoomSpeed = 0.6;
  resetCamera();
  effects = new GardenEffects(scene);
  world = new GardenWorld(scene, effects);
  const target = new THREE.WebGLRenderTarget(1, 1, {
    type: THREE.HalfFloatType,
    samples: 4,
  });
  composer = new EffectComposer(renderer, target);
  composer.addPass(new RenderPass(scene, camera));
  const bloom = new UnrealBloomPass(new THREE.Vector2(1, 1), 0.24, 0.55, 1.05);
  composer.addPass(bloom);
  composer.addPass(new OutputPass());
  let down;
  canvas.addEventListener(
    "pointerdown",
    (event) => (down = { x: event.clientX, y: event.clientY }),
  );
  canvas.addEventListener("pointerup", (event) => {
    if (!down || Math.hypot(event.clientX - down.x, event.clientY - down.y) > 6)
      return;
    const bounds = canvas.getBoundingClientRect();
    mouse.set(
      ((event.clientX - bounds.left) / bounds.width) * 2 - 1,
      (-(event.clientY - bounds.top) / bounds.height) * 2 + 1,
    );
    raycaster.setFromCamera(mouse, camera);
    ui.select(world.pick(raycaster));
  });
  canvas.addEventListener("webglcontextlost", (event) => {
    event.preventDefault();
    active = false;
    ui.error(
      new Error(
        "The graphics context was lost. Reopen the garden to reconnect.",
      ),
    );
  });
  new ResizeObserver(resize).observe(container);
  document.addEventListener("visibilitychange", schedule);
  window.chrome?.webview?.addEventListener("message", (event) =>
    receive(event.data),
  );
  window.garden = {
    receive,
    info: () => ({
      renderer: renderer
        .getContext()
        .getParameter(renderer.getContext().VERSION),
      successes: state.successes,
      plants: world.plants.length,
      features: [...world.features],
      frames: renderer.info.render.frame,
      drawCalls: renderer.info.render.calls,
      particles: effects.bursts.length,
      camera: camera.position.toArray(),
      zoom: camera.zoom,
      atmosphere: state.atmosphere,
      reducedMotion: state.reducedMotion,
    }),
    freeze: (at = 5) => {
      frozen = true;
      cancelAnimationFrame(frame);
      frame = 0;
      elapsed = at;
      renderOnce();
    },
    resume: () => {
      frozen = false;
      lastFrame = performance.now();
      schedule();
    },
  };
  world.updateProgress(state, elapsed, false);
  ui.update(state);
  resize();
  schedule();
  // Compile the real materials before the loading curtain fades away.
  renderer.compileAsync(scene, camera).then(() => {
    renderOnce();
    ui.ready();
  });
} catch (error) {
  console.error(error);
  ui.error(error);
}
