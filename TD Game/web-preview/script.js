const battlefield = document.querySelector("#battlefield");
const buildLayer = document.querySelector("#buildLayer");
const enemyLayer = document.querySelector("#enemyLayer");
const projectileLayer = document.querySelector("#projectileLayer");
const effectLayer = document.querySelector("#effectLayer");

const ui = {
  life: document.querySelector("#lifeValue"),
  gold: document.querySelector("#goldValue"),
  wave: document.querySelector("#waveValue"),
  enemyCount: document.querySelector("#enemyCountValue"),
  towerName: document.querySelector("#towerName"),
  towerDamage: document.querySelector("#towerDamage"),
  towerSpeed: document.querySelector("#towerSpeed"),
  towerRange: document.querySelector("#towerRange"),
  towerEffect: document.querySelector("#towerEffect"),
  towerPreview: document.querySelector("#towerPreview"),
  restart: document.querySelector("#restartButton")
};

const visualScale = {
  tower: {
    standard: 100,
    large: 110
  },
  enemy: {
    small: 84,
    standard: 88,
    medium: 92,
    large: 100
  }
};

const towerTypes = {
  arrow: {
    name: "箭塔",
    image: "./assets/towers/tower-arrow.png",
    damage: 34,
    speedLabel: "快",
    attackInterval: 0.72,
    rangeLabel: "中",
    range: 205,
    effect: "追蹤箭 / 暴擊",
    projectile: "arrow",
    rangeColor: "rgba(245, 210, 102, 0.55)",
    windup: 0.18,
    size: visualScale.tower.standard
  },
  ice: {
    name: "冰塔",
    image: "./assets/towers/tower-ice.png",
    damage: 18,
    speedLabel: "中",
    attackInterval: 1.08,
    rangeLabel: "中",
    range: 195,
    effect: "冰霜緩速",
    projectile: "ice",
    rangeColor: "rgba(98, 215, 255, 0.58)",
    size: visualScale.tower.standard,
    slow: 1.3
  },
  fire: {
    name: "火焰塔",
    image: "./assets/towers/tower-fire.png",
    damage: 46,
    speedLabel: "中",
    attackInterval: 1.2,
    rangeLabel: "短",
    range: 175,
    effect: "爆燃濺射",
    projectile: "fire",
    rangeColor: "rgba(255, 136, 70, 0.58)",
    size: visualScale.tower.standard,
    splash: 42
  },
  mythic: {
    name: "神話核心塔",
    image: "./assets/towers/tower-mythic.png",
    damage: 88,
    speedLabel: "慢",
    attackInterval: 1.75,
    rangeLabel: "超遠",
    range: 280,
    effect: "星核共鳴",
    projectile: "mythic",
    rangeColor: "rgba(166, 130, 255, 0.58)",
    size: visualScale.tower.large,
    splash: 70
  }
};

const enemyTypes = {
  normal: { name: "普通怪", image: "./assets/enemies/enemy-normal.png", hp: 220, speed: 72, size: visualScale.enemy.standard, reward: 18, walk: 0.52 },
  fast: { name: "快速怪", image: "./assets/enemies/enemy-fast.png", hp: 150, speed: 108, size: visualScale.enemy.small, reward: 16, walk: 0.36 },
  tank: { name: "坦克怪", image: "./assets/enemies/enemy-tank.png", hp: 420, speed: 48, size: visualScale.enemy.large, reward: 34, walk: 0.72 },
  flying: { name: "飛行怪", image: "./assets/enemies/enemy-flying.png", hp: 185, speed: 92, size: visualScale.enemy.standard, reward: 22, walk: 0.42 },
  shield: { name: "護盾怪", image: "./assets/enemies/enemy-shield.png", hp: 310, speed: 62, size: visualScale.enemy.medium, reward: 28, walk: 0.58 }
};

const towerSlots = [
  { id: "slot-a", type: "arrow", x: 21.2, y: 30.6 },
  { id: "slot-b", type: "ice", x: 38, y: 80.5 },
  { id: "slot-c", type: "fire", x: 60, y: 18.8 },
  { id: "slot-d", type: "mythic", x: 73.8, y: 79.2 },
  { id: "slot-e", type: "arrow", x: 85.8, y: 59.7 }
];

const pathPercents = [
  { x: -5, y: 61 },
  { x: 15, y: 59 },
  { x: 29, y: 64 },
  { x: 43, y: 53 },
  { x: 53, y: 39 },
  { x: 65, y: 40 },
  { x: 76, y: 55 },
  { x: 87, y: 48 },
  { x: 98, y: 32 },
  { x: 107, y: 31 }
];

const state = {
  life: 20,
  gold: 680,
  selectedTowerId: "slot-a",
  waveIndex: 1,
  elapsed: 0,
  spawnTimer: 0,
  spawnIndex: 0,
  enemyId: 1,
  projectileId: 1,
  effectId: 1,
  enemies: [],
  projectiles: [],
  towers: [],
  path: [],
  totalPathLength: 0,
  wave: ["normal", "fast", "normal", "tank", "flying", "shield", "fast", "normal", "tank", "flying"]
};

function pctToPx(point) {
  const rect = battlefield.getBoundingClientRect();
  return { x: (point.x / 100) * rect.width, y: (point.y / 100) * rect.height };
}

function rebuildPath() {
  state.path = pathPercents.map(pctToPx);
  state.totalPathLength = 0;
  for (let i = 1; i < state.path.length; i += 1) {
    state.totalPathLength += distance(state.path[i - 1], state.path[i]);
  }
}

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function pointOnPath(progress) {
  let remaining = progress;
  for (let i = 1; i < state.path.length; i += 1) {
    const from = state.path[i - 1];
    const to = state.path[i];
    const length = distance(from, to);
    if (remaining <= length) {
      const t = Math.max(0, Math.min(1, remaining / length));
      return {
        x: from.x + (to.x - from.x) * t,
        y: from.y + (to.y - from.y) * t,
        dx: to.x - from.x,
        dy: to.y - from.y
      };
    }
    remaining -= length;
  }
  const last = state.path[state.path.length - 1];
  const prev = state.path[state.path.length - 2];
  return { x: last.x, y: last.y, dx: last.x - prev.x, dy: last.y - prev.y };
}

function directionFromDelta(dx, dy) {
  if (Math.abs(dx) > Math.abs(dy)) return dx >= 0 ? "right" : "left";
  return dy >= 0 ? "down" : "up";
}

function setupTowers() {
  buildLayer.innerHTML = "";
  state.towers = towerSlots.map((slot) => {
    const type = towerTypes[slot.type];
    const button = document.createElement("button");
    button.className = "tower-pad";
    button.type = "button";
    button.dataset.towerId = slot.id;
    button.dataset.towerType = slot.type;
    button.ariaLabel = `${type.name} 專屬塔位`;
    button.style.left = `${slot.x}%`;
    button.style.top = `${slot.y}%`;
    button.style.setProperty("--range-width", `${type.range * 2}px`);
    button.style.setProperty("--range-color", type.rangeColor);
    button.innerHTML = `
      <span class="range-ring"></span>
      <span class="tower-unit" style="--tower-size:${type.size}px">
        <img class="tower-img" src="${type.image}" alt="${type.name}" draggable="false" />
      </span>
    `;
    button.addEventListener("click", () => selectTower(slot.id));
    buildLayer.appendChild(button);
    return { ...slot, ...type, cooldown: Math.random() * type.attackInterval, element: button };
  });
  selectTower("slot-a");
}

function selectTower(id) {
  state.selectedTowerId = id;
  const tower = state.towers.find((item) => item.id === id);
  if (!tower) return;
  ui.towerName.textContent = tower.name;
  ui.towerDamage.textContent = tower.damage;
  ui.towerSpeed.textContent = tower.speedLabel;
  ui.towerRange.textContent = tower.rangeLabel;
  ui.towerEffect.textContent = tower.effect;
  ui.towerPreview.src = tower.image;
  ui.towerPreview.alt = tower.name;
  state.towers.forEach((item) => item.element.classList.toggle("is-selected", item.id === id));
}

function spawnEnemy(typeId) {
  const type = enemyTypes[typeId];
  const enemy = {
    id: state.enemyId++,
    typeId,
    ...type,
    hp: type.hp,
    maxHp: type.hp,
    progress: 0,
    slowTimer: 0,
    x: 0,
    y: 0
  };
  const node = document.createElement("div");
  node.className = "enemy-unit";
  node.style.setProperty("--enemy-size", `${type.size}px`);
  node.style.setProperty("--walk-duration", `${type.walk}s`);
  node.innerHTML = `
    <div class="hp-bar"><div class="hp-fill"></div></div>
    <span class="enemy-shadow"></span>
    <span class="enemy-body">
      <img class="enemy-sprite" src="${type.image}" alt="${type.name}" draggable="false" />
      <span class="step-dust step-dust-a"></span>
      <span class="step-dust step-dust-b"></span>
    </span>
  `;
  enemy.node = node;
  enemy.hpFill = node.querySelector(".hp-fill");
  enemyLayer.appendChild(node);
  state.enemies.push(enemy);
}

function fireTower(tower, target) {
  const travelSpeed = tower.projectile === "mythic" ? 480 : 580;
  const towerPoint = pctToPx({ x: tower.x, y: tower.y });
  const towerUnit = tower.element.querySelector(".tower-unit");
  const attackDuration = Math.max(170, tower.attackInterval * 380);
  towerUnit.style.setProperty("--attack-duration", `${attackDuration}ms`);
  towerUnit.classList.add("is-attacking");
  if (tower.projectile === "arrow") towerUnit.classList.add("is-drawing");
  window.setTimeout(() => {
    towerUnit?.classList.remove("is-attacking");
    towerUnit?.classList.remove("is-drawing");
  }, attackDuration);

  const node = document.createElement("span");
  node.className = `projectile ${tower.projectile}`;
  if (tower.projectile === "arrow") {
    node.innerHTML = `<span class="arrow-tail"></span><span class="arrow-head"></span>`;
  } else if (tower.projectile === "ice") {
    node.innerHTML = `<span class="ice-core"></span><span class="ice-ring"></span>`;
  } else if (tower.projectile === "fire") {
    node.innerHTML = `<span class="fire-core"></span><span class="fire-flame"></span>`;
  } else if (tower.projectile === "mythic") {
    node.innerHTML = `<span class="mythic-core"></span><span class="mythic-ring"></span>`;
  }
  projectileLayer.appendChild(node);
  const targetPoint = { x: target.x, y: target.y - 34 };
  const initialDistance = distance({ x: towerPoint.x, y: towerPoint.y - 45 }, targetPoint);
  state.projectiles.push({
    id: state.projectileId++,
    towerId: tower.id,
    kind: tower.projectile,
    damage: tower.damage,
    splash: tower.splash || 0,
    slow: tower.slow || 0,
    targetId: target.id,
    speed: travelSpeed,
    x: towerPoint.x,
    y: towerPoint.y - 45,
    startX: towerPoint.x,
    startY: towerPoint.y - 45,
    prevX: towerPoint.x,
    prevY: towerPoint.y - 45,
    t: 0,
    duration: Math.max(0.22, initialDistance / travelSpeed),
    arcHeight: tower.projectile === "arrow" ? Math.min(92, Math.max(46, initialDistance * 0.22)) : 0,
    node
  });
}

function applyDamage(enemy, amount, kind) {
  enemy.hp = Math.max(0, enemy.hp - amount);
  enemy.hpFill.style.width = `${(enemy.hp / enemy.maxHp) * 100}%`;
  enemy.node.classList.add("is-hit");
  showDamageNumber(enemy.x, enemy.y - 78, amount);
  window.setTimeout(() => enemy.node?.classList.remove("is-hit"), 140);
  createImpact(enemy.x, enemy.y - 32, kind);
  if (enemy.hp <= 0) {
    state.gold += enemy.reward;
    createImpact(enemy.x, enemy.y - 38, "mythic");
    enemy.node.remove();
    state.enemies = state.enemies.filter((item) => item.id !== enemy.id);
  }
}

function showDamageNumber(x, y, amount) {
  const node = document.createElement("span");
  node.className = "damage-number";
  node.textContent = `-${Math.round(amount)}`;
  node.style.left = `${x}px`;
  node.style.top = `${y}px`;
  effectLayer.appendChild(node);
  window.setTimeout(() => node.remove(), 680);
}

function createImpact(x, y, kind) {
  const node = document.createElement("span");
  node.className = `impact ${kind}`;
  node.style.left = `${x}px`;
  node.style.top = `${y}px`;
  effectLayer.appendChild(node);
  window.setTimeout(() => node.remove(), 450);
}

function castSkill(skill) {
  const center = pctToPx(skill === "meteor" ? { x: 59, y: 50 } : skill === "frost" ? { x: 45, y: 55 } : { x: 74, y: 47 });
  const radius = skill === "meteor" ? 150 : 170;
  const node = document.createElement("span");
  node.className = `skill-effect ${skill}`;
  node.style.left = `${center.x}px`;
  node.style.top = `${center.y}px`;
  node.style.setProperty("--skill-size", `${radius * 2}px`);
  effectLayer.appendChild(node);
  window.setTimeout(() => node.remove(), 700);
  state.enemies.forEach((enemy) => {
    if (distance(center, enemy) <= radius) {
      if (skill === "meteor") applyDamage(enemy, 95, "fire");
      if (skill === "frost") {
        enemy.slowTimer = 2.2;
        enemy.node.classList.add("is-slowed");
        applyDamage(enemy, 24, "ice");
      }
      if (skill === "time") {
        enemy.slowTimer = 3.2;
        enemy.node.classList.add("is-slowed");
        applyDamage(enemy, 16, "mythic");
      }
    }
  });
}

function update(dt) {
  state.elapsed += dt;
  if (state.spawnIndex < state.wave.length) {
    state.spawnTimer -= dt;
    if (state.spawnTimer <= 0) {
      spawnEnemy(state.wave[state.spawnIndex]);
      state.spawnIndex += 1;
      state.spawnTimer = 1.25;
    }
  }

  state.enemies.forEach((enemy) => {
    enemy.slowTimer = Math.max(0, enemy.slowTimer - dt);
    if (enemy.slowTimer <= 0) enemy.node.classList.remove("is-slowed");
    const slowFactor = enemy.slowTimer > 0 ? 0.45 : 1;
    enemy.progress += enemy.speed * slowFactor * dt;
    const pos = pointOnPath(enemy.progress);
    enemy.x = pos.x;
    enemy.y = pos.y;
    enemy.direction = directionFromDelta(pos.dx, pos.dy);
    if (enemy.progress >= state.totalPathLength) {
      state.life = Math.max(0, state.life - 1);
      enemy.node.remove();
      enemy.remove = true;
    }
  });
  state.enemies = state.enemies.filter((enemy) => !enemy.remove);

  state.towers.forEach((tower) => {
    tower.cooldown -= dt;
    if (tower.cooldown > 0) return;
    const towerPoint = pctToPx({ x: tower.x, y: tower.y });
    let target = null;
    let bestProgress = -1;
    state.enemies.forEach((enemy) => {
      const d = distance(towerPoint, enemy);
      if (d <= tower.range && enemy.progress > bestProgress) {
        target = enemy;
        bestProgress = enemy.progress;
      }
    });
    if (target) {
      fireTower(tower, target);
      tower.cooldown = tower.attackInterval;
    }
  });

  state.projectiles.forEach((projectile) => {
    const target = state.enemies.find((enemy) => enemy.id === projectile.targetId);
    if (!target) {
      projectile.node.remove();
      projectile.remove = true;
      return;
    }
    const targetPoint = { x: target.x, y: target.y - 34 };
    let impactReady = false;
    if (projectile.kind === "arrow") {
      projectile.t = Math.min(1, projectile.t + dt / projectile.duration);
      const t = projectile.t;
      const midX = (projectile.startX + targetPoint.x) / 2;
      const midY = (projectile.startY + targetPoint.y) / 2 - projectile.arcHeight;
      projectile.prevX = projectile.x;
      projectile.prevY = projectile.y;
      projectile.x = (1 - t) * (1 - t) * projectile.startX + 2 * (1 - t) * t * midX + t * t * targetPoint.x;
      projectile.y = (1 - t) * (1 - t) * projectile.startY + 2 * (1 - t) * t * midY + t * t * targetPoint.y;
      impactReady = t >= 1 || distance(projectile, targetPoint) <= Math.max(18, target.size * 0.22);
    } else {
      const d = distance(projectile, targetPoint);
      const move = projectile.speed * dt;
      impactReady = d <= move + Math.max(18, target.size * 0.22);
      if (!impactReady) {
        projectile.prevX = projectile.x;
        projectile.prevY = projectile.y;
        projectile.x += ((targetPoint.x - projectile.x) / d) * move;
        projectile.y += ((targetPoint.y - projectile.y) / d) * move;
      }
    }
    if (impactReady) {
      if (projectile.splash > 0) {
        state.enemies.forEach((enemy) => {
          if (distance(target, enemy) <= projectile.splash) applyDamage(enemy, projectile.damage * 0.72, projectile.kind);
        });
      } else {
        applyDamage(target, projectile.damage, projectile.kind);
      }
      if (projectile.slow > 0 && target.hp > 0) {
        target.slowTimer = projectile.slow;
        target.node.classList.add("is-slowed");
      }
      projectile.node.remove();
      projectile.remove = true;
      return;
    }
  });
  state.projectiles = state.projectiles.filter((projectile) => !projectile.remove);

  render();
}

function render() {
  state.enemies.forEach((enemy) => {
    enemy.node.style.left = `${enemy.x}px`;
    enemy.node.style.top = `${enemy.y}px`;
    enemy.node.dataset.dir = enemy.direction || "right";
  });
  state.projectiles.forEach((projectile) => {
    projectile.node.style.left = `${projectile.x}px`;
    projectile.node.style.top = `${projectile.y}px`;
    const angle = Math.atan2(projectile.y - projectile.prevY, projectile.x - projectile.prevX);
    projectile.node.style.setProperty("--projectile-angle", `${angle}rad`);
  });
  ui.life.textContent = state.life;
  ui.gold.textContent = state.gold;
  ui.wave.textContent = `${state.waveIndex} / 1`;
  ui.enemyCount.textContent = state.enemies.length;
}

function resetGame() {
  enemyLayer.innerHTML = "";
  projectileLayer.innerHTML = "";
  effectLayer.innerHTML = "";
  state.life = 20;
  state.gold = 680;
  state.elapsed = 0;
  state.spawnTimer = 0;
  state.spawnIndex = 0;
  state.enemyId = 1;
  state.projectileId = 1;
  state.enemies = [];
  state.projectiles = [];
  state.towers.forEach((tower) => {
    tower.cooldown = Math.random() * tower.attackInterval;
  });
  render();
}

let lastTime = performance.now();
function loop(now) {
  const dt = Math.min(0.05, (now - lastTime) / 1000);
  lastTime = now;
  update(dt);
  requestAnimationFrame(loop);
}

document.querySelectorAll(".skill").forEach((button) => {
  button.addEventListener("click", () => castSkill(button.dataset.skill));
});
ui.restart.addEventListener("click", resetGame);
window.addEventListener("resize", () => {
  rebuildPath();
  render();
});

window.render_game_to_text = () => JSON.stringify({
  coordinateSystem: "origin top-left, x right, y down, units CSS px",
  life: state.life,
  gold: state.gold,
  selectedTowerId: state.selectedTowerId,
  enemies: state.enemies.map((enemy) => ({
    id: enemy.id,
    type: enemy.typeId,
    hp: Math.round(enemy.hp),
    maxHp: enemy.maxHp,
    x: Math.round(enemy.x),
    y: Math.round(enemy.y)
  })),
  projectiles: state.projectiles.length
});

window.advanceTime = (ms) => {
  const steps = Math.max(1, Math.round(ms / (1000 / 60)));
  for (let i = 0; i < steps; i += 1) update(1 / 60);
};

rebuildPath();
setupTowers();
resetGame();
requestAnimationFrame(loop);
