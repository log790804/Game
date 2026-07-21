import * as THREE from "../../vendor/three/three.module.js";

const canvas = document.querySelector("#game-canvas");
const timeLabel = document.querySelector("#time-label");
const questLabel = document.querySelector("#quest-label");
const inventoryLabel = document.querySelector("#inventory-label");
const promptEl = document.querySelector("#prompt");
const dialogueEl = document.querySelector("#dialogue");
const dialogueSpeaker = document.querySelector("#dialogue-speaker");
const dialogueLine = document.querySelector("#dialogue-line");

const keys = new Set();
const worldBounds = 18;
const clock = new THREE.Clock();

const state = {
  timeMinutes: 6 * 60,
  inventory: { wood: 0, berry: 0 },
  questDone: false,
  nearby: null,
  dialogueOpen: false,
  lastInteraction: "",
  player: {
    position: new THREE.Vector3(0, 0, 4),
    direction: new THREE.Vector3(0, 0, -1),
    speed: 5.2,
  },
  items: [
    { id: "wood-1", type: "wood", label: "木頭", position: new THREE.Vector3(-5, 0, 1), picked: false },
    { id: "wood-2", type: "wood", label: "木頭", position: new THREE.Vector3(4, 0, -2), picked: false },
    { id: "wood-3", type: "wood", label: "木頭", position: new THREE.Vector3(7, 0, 5), picked: false },
    { id: "berry-1", type: "berry", label: "莓果", position: new THREE.Vector3(-8, 0, -4), picked: false },
  ],
  npcs: [
    {
      id: "mira",
      name: "米拉",
      position: new THREE.Vector3(2, 0, -6),
      lines: [
        "早安，島主！如果你能撿 3 個木頭，我就能幫大家修好公告牌。",
        "木頭通常散在樹旁，靠近後按 E 就能撿起來。",
      ],
      doneLine: "太好了！3 個木頭都帶來了，村子的第一個任務完成。",
    },
  ],
};

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x8ed7f5);
scene.fog = new THREE.Fog(0x8ed7f5, 26, 58);

const camera = new THREE.PerspectiveCamera(50, 1, 0.1, 120);
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;

const sun = new THREE.DirectionalLight(0xfff4cc, 2.2);
sun.position.set(10, 16, 8);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -24;
sun.shadow.camera.right = 24;
sun.shadow.camera.top = 24;
sun.shadow.camera.bottom = -24;
scene.add(sun);
scene.add(new THREE.HemisphereLight(0xbdefff, 0x6f8458, 1.3));

const player = createCharacter(0x376dff, 0xffd7b5);
player.group.position.copy(state.player.position);
scene.add(player.group);

const itemMeshes = new Map();
const npcMeshes = new Map();

createWorld();
state.items.forEach((item) => {
  const mesh = createItem(item);
  itemMeshes.set(item.id, mesh);
  scene.add(mesh);
});
state.npcs.forEach((npc) => {
  const npcObj = createNpc(npc);
  npcMeshes.set(npc.id, npcObj);
  scene.add(npcObj.group);
});

function createWorld() {
  const ground = new THREE.Mesh(
    new THREE.CircleGeometry(23, 96),
    new THREE.MeshStandardMaterial({ color: 0x70b96b, roughness: 0.95 })
  );
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  const path = new THREE.Mesh(
    new THREE.RingGeometry(3.6, 4.7, 80, 1, 0, Math.PI * 2),
    new THREE.MeshStandardMaterial({ color: 0xd7b879, roughness: 1 })
  );
  path.rotation.x = -Math.PI / 2;
  path.position.y = 0.012;
  scene.add(path);

  addHouse(-7, -7);
  addHouse(8, -8);
  addPond(-8, 7);

  [
    [-10, -2],
    [-12, 4],
    [-4, 8],
    [5, 8],
    [10, 2],
    [12, -4],
    [-2, -10],
  ].forEach(([x, z], index) => addTree(x, z, index));

  for (let i = 0; i < 34; i += 1) {
    const angle = (i / 34) * Math.PI * 2;
    const radius = 15 + Math.sin(i * 1.7) * 2.1;
    addFlower(Math.cos(angle) * radius, Math.sin(angle) * radius, i);
  }
}

function addHouse(x, z) {
  const group = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.BoxGeometry(3.4, 2.4, 3),
    new THREE.MeshStandardMaterial({ color: 0xf1d79d, roughness: 0.78 })
  );
  body.position.y = 1.2;
  body.castShadow = true;
  body.receiveShadow = true;
  const roof = new THREE.Mesh(
    new THREE.ConeGeometry(2.7, 1.4, 4),
    new THREE.MeshStandardMaterial({ color: 0xbc5d4d, roughness: 0.74 })
  );
  roof.rotation.y = Math.PI / 4;
  roof.position.y = 3.1;
  roof.castShadow = true;
  const door = new THREE.Mesh(
    new THREE.BoxGeometry(0.75, 1.2, 0.08),
    new THREE.MeshStandardMaterial({ color: 0x7a5435, roughness: 0.9 })
  );
  door.position.set(0, 0.65, 1.54);
  group.add(body, roof, door);
  group.position.set(x, 0, z);
  scene.add(group);
}

function addTree(x, z, index) {
  const group = new THREE.Group();
  const trunk = new THREE.Mesh(
    new THREE.CylinderGeometry(0.22, 0.32, 1.5, 10),
    new THREE.MeshStandardMaterial({ color: 0x8a5a32, roughness: 0.85 })
  );
  trunk.position.y = 0.75;
  trunk.castShadow = true;
  const crown = new THREE.Mesh(
    new THREE.DodecahedronGeometry(1.15 + (index % 3) * 0.12, 0),
    new THREE.MeshStandardMaterial({ color: index % 2 ? 0x3c9f5a : 0x2f8449, roughness: 0.9 })
  );
  crown.position.y = 2;
  crown.castShadow = true;
  group.add(trunk, crown);
  group.position.set(x, 0, z);
  scene.add(group);
}

function addFlower(x, z, index) {
  const color = [0xffd85f, 0xff8ab3, 0xe8f6ff, 0x9de089][index % 4];
  const flower = new THREE.Mesh(
    new THREE.SphereGeometry(0.12, 8, 8),
    new THREE.MeshStandardMaterial({ color, roughness: 0.65 })
  );
  flower.position.set(x, 0.13, z);
  scene.add(flower);
}

function addPond(x, z) {
  const pond = new THREE.Mesh(
    new THREE.CircleGeometry(2.6, 48),
    new THREE.MeshStandardMaterial({ color: 0x5cb7d7, roughness: 0.35, metalness: 0.05 })
  );
  pond.rotation.x = -Math.PI / 2;
  pond.position.set(x, 0.018, z);
  scene.add(pond);
}

function createCharacter(bodyColor, faceColor) {
  const group = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.CapsuleGeometry(0.44, 0.65, 6, 16),
    new THREE.MeshStandardMaterial({ color: bodyColor, roughness: 0.62 })
  );
  body.position.y = 0.9;
  body.castShadow = true;
  const head = new THREE.Mesh(
    new THREE.SphereGeometry(0.42, 24, 18),
    new THREE.MeshStandardMaterial({ color: faceColor, roughness: 0.55 })
  );
  head.position.y = 1.58;
  head.castShadow = true;
  const nose = new THREE.Mesh(
    new THREE.SphereGeometry(0.08, 12, 8),
    new THREE.MeshStandardMaterial({ color: 0x473026, roughness: 0.5 })
  );
  nose.position.set(0, 1.56, -0.4);
  group.add(body, head, nose);
  return { group, body, head };
}

function createNpc(npc) {
  const character = createCharacter(0xed8c53, 0xf6c28d);
  character.group.position.copy(npc.position);
  character.group.lookAt(0, 0, 0);
  return character;
}

function createItem(item) {
  const group = new THREE.Group();
  if (item.type === "wood") {
    const log = new THREE.Mesh(
      new THREE.CylinderGeometry(0.18, 0.18, 0.9, 14),
      new THREE.MeshStandardMaterial({ color: 0x9b6738, roughness: 0.82 })
    );
    log.rotation.z = Math.PI / 2;
    log.castShadow = true;
    group.add(log);
  } else {
    const berry = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 18, 14),
      new THREE.MeshStandardMaterial({ color: 0xc33b58, roughness: 0.55 })
    );
    berry.castShadow = true;
    group.add(berry);
  }
  group.position.copy(item.position);
  group.position.y = 0.38;
  return group;
}

function resize() {
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  renderer.setSize(width, height, false);
  camera.aspect = width / Math.max(1, height);
  camera.updateProjectionMatrix();
}

function update(dt) {
  state.timeMinutes = (state.timeMinutes + dt * 6) % (24 * 60);
  updateLighting();
  updatePlayer(dt);
  updateInteractions();

  state.items.forEach((item, index) => {
    const mesh = itemMeshes.get(item.id);
    if (!mesh || item.picked) return;
    mesh.rotation.y += dt * 1.8;
    mesh.position.y = 0.38 + Math.sin(performance.now() / 450 + index) * 0.06;
  });

  const npcObj = npcMeshes.get("mira");
  if (npcObj) {
    npcObj.group.rotation.y = Math.sin(performance.now() / 900) * 0.08;
  }
}

function updatePlayer(dt) {
  if (state.dialogueOpen) return;

  const move = new THREE.Vector3();
  if (keys.has("KeyW")) move.z -= 1;
  if (keys.has("KeyS")) move.z += 1;
  if (keys.has("KeyA")) move.x -= 1;
  if (keys.has("KeyD")) move.x += 1;

  if (move.lengthSq() > 0) {
    move.normalize();
    state.player.direction.copy(move);
    state.player.position.addScaledVector(move, state.player.speed * dt);
    state.player.position.x = THREE.MathUtils.clamp(state.player.position.x, -worldBounds, worldBounds);
    state.player.position.z = THREE.MathUtils.clamp(state.player.position.z, -worldBounds, worldBounds);
    player.group.position.copy(state.player.position);
    player.group.rotation.y = Math.atan2(move.x, move.z);
    player.body.position.y = 0.9 + Math.sin(performance.now() / 90) * 0.025;
  } else {
    player.body.position.y = 0.9;
  }
}

function updateInteractions() {
  const playerPos = state.player.position;
  let nearest = null;
  let nearestDistance = Infinity;

  state.items.forEach((item) => {
    if (item.picked) return;
    const distance = item.position.distanceTo(playerPos);
    if (distance < 1.85 && distance < nearestDistance) {
      nearest = { kind: "item", id: item.id };
      nearestDistance = distance;
    }
  });

  state.npcs.forEach((npc) => {
    const distance = npc.position.distanceTo(playerPos);
    if (distance < 2.2 && distance < nearestDistance) {
      nearest = { kind: "npc", id: npc.id };
      nearestDistance = distance;
    }
  });

  state.nearby = nearest;
  renderHud();
}

function interact() {
  if (state.dialogueOpen) {
    closeDialogue();
    return;
  }

  if (!state.nearby) return;

  if (state.nearby.kind === "item") {
    const item = state.items.find((entry) => entry.id === state.nearby.id);
    if (!item || item.picked) return;
    item.picked = true;
    state.inventory[item.type] += 1;
    state.lastInteraction = `picked:${item.type}`;
    const mesh = itemMeshes.get(item.id);
    if (mesh) mesh.visible = false;
  }

  if (state.nearby.kind === "npc") {
    const npc = state.npcs.find((entry) => entry.id === state.nearby.id);
    const hasEnoughWood = state.inventory.wood >= 3;
    if (hasEnoughWood) state.questDone = true;
    openDialogue(npc.name, hasEnoughWood ? npc.doneLine : npc.lines[state.inventory.wood % npc.lines.length]);
    state.lastInteraction = `talk:${npc.id}`;
  }

  renderHud();
}

function openDialogue(speaker, line) {
  state.dialogueOpen = true;
  dialogueSpeaker.textContent = speaker;
  dialogueLine.textContent = line;
  dialogueEl.classList.remove("hidden");
}

function closeDialogue() {
  state.dialogueOpen = false;
  dialogueEl.classList.add("hidden");
}

function updateLighting() {
  const dayT = state.timeMinutes / (24 * 60);
  const daylight = Math.max(0.18, Math.sin(dayT * Math.PI * 2 - Math.PI / 2) * 0.75 + 0.35);
  sun.intensity = 0.9 + daylight * 1.8;
  sun.position.set(Math.cos(dayT * Math.PI * 2) * 16, 9 + daylight * 12, Math.sin(dayT * Math.PI * 2) * 16);
  const sky = new THREE.Color().lerpColors(new THREE.Color(0x223e5c), new THREE.Color(0x8ed7f5), daylight);
  scene.background = sky;
  scene.fog.color.copy(sky);
}

function updateCamera() {
  const target = state.player.position;
  const cameraTarget = new THREE.Vector3(target.x + 5.8, target.y + 6.3, target.z + 8.2);
  camera.position.lerp(cameraTarget, 0.08);
  camera.lookAt(target.x, target.y + 0.8, target.z);
}

function renderHud() {
  const hours = Math.floor(state.timeMinutes / 60);
  const minutes = Math.floor(state.timeMinutes % 60);
  timeLabel.textContent = `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
  questLabel.textContent = state.questDone ? "公告牌修好了" : `木頭 ${state.inventory.wood} / 3`;

  const inventoryParts = [];
  if (state.inventory.wood) inventoryParts.push(`木頭 x${state.inventory.wood}`);
  if (state.inventory.berry) inventoryParts.push(`莓果 x${state.inventory.berry}`);
  inventoryLabel.textContent = inventoryParts.length ? inventoryParts.join("、") : "空";

  if (state.dialogueOpen || !state.nearby) {
    promptEl.classList.add("hidden");
    return;
  }

  if (state.nearby.kind === "item") {
    const item = state.items.find((entry) => entry.id === state.nearby.id);
    promptEl.textContent = `按 E 撿起 ${item.label}`;
  } else {
    const npc = state.npcs.find((entry) => entry.id === state.nearby.id);
    promptEl.textContent = `按 E 與 ${npc.name} 對話`;
  }
  promptEl.classList.remove("hidden");
}

function render() {
  updateCamera();
  renderer.render(scene, camera);
}

function animate() {
  const dt = Math.min(clock.getDelta(), 0.05);
  update(dt);
  render();
  requestAnimationFrame(animate);
}

function toggleFullscreen() {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen?.();
  } else {
    document.exitFullscreen?.();
  }
}

function renderGameToText() {
  const visibleItems = state.items
    .filter((item) => !item.picked)
    .map((item) => ({
      id: item.id,
      type: item.type,
      x: Number(item.position.x.toFixed(2)),
      z: Number(item.position.z.toFixed(2)),
    }));

  return JSON.stringify({
    note: "Coordinates use x/z ground plane, y up. Origin is island center.",
    player: {
      x: Number(state.player.position.x.toFixed(2)),
      z: Number(state.player.position.z.toFixed(2)),
    },
    inventory: state.inventory,
    questDone: state.questDone,
    nearby: state.nearby,
    dialogueOpen: state.dialogueOpen,
    lastInteraction: state.lastInteraction,
    visibleItems,
  });
}

window.render_game_to_text = renderGameToText;
window.advanceTime = (ms) => {
  const steps = Math.max(1, Math.round(ms / (1000 / 60)));
  for (let i = 0; i < steps; i += 1) update(1 / 60);
  render();
};

window.addEventListener("resize", resize);
document.addEventListener("fullscreenchange", resize);
window.addEventListener("keydown", (event) => {
  if (event.code === "KeyE") interact();
  if (event.code === "KeyF") toggleFullscreen();
  keys.add(event.code);
});
window.addEventListener("keyup", (event) => keys.delete(event.code));

resize();
renderHud();
animate();
