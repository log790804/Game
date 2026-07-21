const data = window.TD_HERO_ANIMATIONS;

const elements = {
  heroSelect: document.querySelector("#heroSelect"),
  actionList: document.querySelector("#actionList"),
  heroName: document.querySelector("#heroName"),
  actionName: document.querySelector("#actionName"),
  frameCount: document.querySelector("#frameCount"),
  frameSize: document.querySelector("#frameSize"),
  spritePlayer: document.querySelector("#spritePlayer"),
  playToggle: document.querySelector("#playToggle"),
  fpsInput: document.querySelector("#fpsInput"),
  fpsValue: document.querySelector("#fpsValue"),
  zoomInput: document.querySelector("#zoomInput"),
  zoomValue: document.querySelector("#zoomValue")
};

const state = {
  hero: null,
  action: null,
  frame: 0,
  playing: true,
  fps: 12,
  lastTime: performance.now(),
  accumulator: 0
};

function setHero(heroId) {
  state.hero = data.heroes.find((hero) => hero.id === heroId) || data.heroes[0];
  state.action = state.hero.actions[0];
  state.frame = 0;
  renderHeroOptions();
  renderActionList();
  applyAction();
}

function setAction(actionId) {
  state.action = state.hero.actions.find((action) => action.id === actionId) || state.hero.actions[0];
  state.frame = 0;
  renderActionList();
  applyAction();
}

function renderHeroOptions() {
  elements.heroSelect.innerHTML = data.heroes
    .map((hero) => `<option value="${hero.id}">${hero.name}</option>`)
    .join("");
  elements.heroSelect.value = state.hero.id;
}

function renderActionList() {
  elements.actionList.innerHTML = state.hero.actions
    .map(
      (action) => `
        <button class="action-button${action.id === state.action.id ? " is-active" : ""}" type="button" data-action="${action.id}">
          <span>${action.label}</span>
          <strong>${action.frameCount}</strong>
        </button>
      `
    )
    .join("");
}

function applyAction() {
  const action = state.action;
  elements.heroName.textContent = state.hero.name;
  elements.actionName.textContent = action.label;
  elements.frameCount.textContent = `${action.frameCount} frames`;
  elements.frameSize.textContent = `${action.frameWidth} x ${action.frameHeight}`;
  elements.fpsInput.value = action.fps;
  state.fps = Number(action.fps);
  elements.fpsValue.textContent = String(state.fps);

  elements.spritePlayer.style.width = `${action.frameWidth}px`;
  elements.spritePlayer.style.height = `${action.frameHeight}px`;
  elements.spritePlayer.style.backgroundImage = `url("./${action.sheet}")`;
  elements.spritePlayer.style.backgroundSize = `${action.frameWidth * action.frameCount}px ${action.frameHeight}px`;
  updateScale();
  paintFrame();
}

function paintFrame() {
  if (!state.action) return;
  const offsetX = state.frame * state.action.frameWidth;
  elements.spritePlayer.style.backgroundPosition = `-${offsetX}px 0`;
}

function updateScale() {
  const zoom = Number(elements.zoomInput.value);
  elements.zoomValue.textContent = `${zoom}x`;
  elements.spritePlayer.style.setProperty("--sprite-scale", zoom);
}

function updatePlayButton() {
  elements.playToggle.textContent = state.playing ? "暫停" : "播放";
}

function tick(now) {
  const delta = now - state.lastTime;
  state.lastTime = now;
  if (state.playing && state.action) {
    state.accumulator += delta;
    const frameDuration = 1000 / state.fps;
    while (state.accumulator >= frameDuration) {
      state.accumulator -= frameDuration;
      state.frame = (state.frame + 1) % state.action.frameCount;
      paintFrame();
    }
  }
  requestAnimationFrame(tick);
}

elements.heroSelect.addEventListener("change", (event) => setHero(event.target.value));
elements.actionList.addEventListener("click", (event) => {
  const button = event.target.closest("[data-action]");
  if (button) setAction(button.dataset.action);
});
elements.playToggle.addEventListener("click", () => {
  state.playing = !state.playing;
  updatePlayButton();
});
elements.fpsInput.addEventListener("input", () => {
  state.fps = Number(elements.fpsInput.value);
  elements.fpsValue.textContent = String(state.fps);
});
elements.zoomInput.addEventListener("input", updateScale);

if (!data?.heroes?.length) {
  elements.actionName.textContent = "尚未產生動畫資料";
} else {
  setHero(data.heroes[0].id);
  updatePlayButton();
  requestAnimationFrame(tick);
}
