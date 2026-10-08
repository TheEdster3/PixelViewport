import { fit, constrain, zoomAt, toImage } from './viewport.mjs';

const width = 960, height = 600;
const canvas = document.querySelector('#demo-canvas');
const hero = document.querySelector('#hero-canvas');
const context = canvas.getContext('2d');
const heroContext = hero.getContext('2d');
const sourceCanvas = document.createElement('canvas');
sourceCanvas.width = width; sourceCanvas.height = height;
const sourceContext = sourceCanvas.getContext('2d');
const image = sourceContext.createImageData(width, height);
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
let paused = reducedMotion.matches, sequence = 0, mode = 'inspection', state = { zoom: 1, x: 0, y: 0 };
let pointer = null, drag = null, fitted = true, lastTick = 0, dirty = true;
const sourceSelect = document.querySelector('#source');
const overlays = document.querySelector('#overlays');
const pauseButton = document.querySelector('#pause');
const pixelReadout = document.querySelector('#pixel-readout');
const shell = canvas.parentElement;
const visible = { hero: true, demo: true };
const observer = new IntersectionObserver(entries => {
  for (const entry of entries) visible[entry.target === hero ? 'hero' : 'demo'] = entry.isIntersecting;
});
observer.observe(hero); observer.observe(canvas);

function bounds() { return { width: shell.clientWidth, height: shell.clientHeight }; }
function setFit() { const size = bounds(); state = fit(size.width, size.height, width, height); fitted = true; render(); }
function changeZoom(factor, anchor) {
  const size = bounds();
  state = zoomAt(state, state.zoom * factor, anchor ?? { x: size.width / 2, y: size.height / 2 }, size.width, size.height, width, height);
  fitted = false; render();
}
function sample(x, y, frame = sequence, source = mode) {
  if (source === 'gray16') {
    const distance = ((x - 500) ** 2 + (y - 280) ** 2) / 110000;
    const value = Math.round(Math.min(65535, Math.max(0, 1500 + 53000 * Math.exp(-distance) + 4800 * Math.sin(x / 48 + frame / 15) + 2200 * Math.cos(y / 31))));
    const gray = value >>> 8;
    return { r: gray, g: gray, b: gray, raw: value };
  }
  const movingX = (frame * 5) % (width - 180), movingY = 160 + Math.floor(70 * Math.sin(frame * .08));
  const radial = Math.abs(x - width / 2) + Math.abs(y - height / 2);
  const texture = 24 + ((x * 7 + y * 3 + frame) & 15);
  const defect = x >= movingX && x < movingX + 150 && y >= movingY && y < movingY + 110;
  return { r: defect ? 218 : (x + frame * 4) % 180 < 3 ? 52 : texture + 8, g: defect ? 84 : Math.max(18, texture - Math.floor(radial / 160)), b: defect ? 45 : texture };
}
function makeSource() {
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const p = sample(x, y), offset = (y * width + x) * 4;
    image.data[offset] = p.r; image.data[offset + 1] = p.g; image.data[offset + 2] = p.b; image.data[offset + 3] = 255;
  }
  sourceContext.putImageData(image, 0, 0);
}
function drawOverlay(target, transform, frame = sequence) {
  const rectangles = [{ x: (frame * 5) % (width - 180), y: 160 + Math.floor(70 * Math.sin(frame * .08)), w: 150, h: 110, color: '#ffc066', text: 'SURFACE ANOMALY' }, { x: 610, y: 310, w: 115, h: 85, color: '#5ae8bb', text: 'REFERENCE' }];
  for (const rect of rectangles) {
    const x = rect.x * transform.zoom + transform.x, y = rect.y * transform.zoom + transform.y;
    target.strokeStyle = rect.color; target.lineWidth = 1.5;
    target.strokeRect(x, y, rect.w * transform.zoom, rect.h * transform.zoom);
    target.font = '12px Consolas, monospace';
    const textWidth = target.measureText(rect.text).width;
    target.fillStyle = '#08111ae8'; target.fillRect(x, y - 24, textWidth + 12, 22);
    target.fillStyle = rect.color; target.fillText(rect.text, x + 6, y - 9);
  }
}
function inspect() {
  if (!pointer) { pixelReadout.textContent = 'MOVE OVER THE IMAGE TO INSPECT'; return; }
  const p = toImage(state, pointer), x = Math.floor(p.x), y = Math.floor(p.y);
  if (x < 0 || x >= width || y < 0 || y >= height) { pixelReadout.textContent = 'OUTSIDE IMAGE'; return; }
  const value = sample(x, y);
  pixelReadout.textContent = mode === 'gray16' ? `X ${x}  Y ${y}  RAW ${value.raw} / 65535` : `X ${x}  Y ${y}  RGB ${value.r} / ${value.g} / ${value.b}`;
}
function render() {
  const size = bounds(), ratio = Math.min(devicePixelRatio || 1, 2);
  const physicalWidth = Math.round(size.width * ratio), physicalHeight = Math.round(size.height * ratio);
  if (canvas.width !== physicalWidth || canvas.height !== physicalHeight) { canvas.width = physicalWidth; canvas.height = physicalHeight; }
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  context.clearRect(0, 0, size.width, size.height);
  context.imageSmoothingEnabled = false;
  context.drawImage(sourceCanvas, state.x, state.y, width * state.zoom, height * state.zoom);
  if (mode === 'inspection' && overlays.checked) drawOverlay(context, state);
  if (pointer) {
    context.strokeStyle = '#5ae8bb70'; context.lineWidth = 1;
    context.beginPath(); context.moveTo(pointer.x, 0); context.lineTo(pointer.x, size.height); context.moveTo(0, pointer.y); context.lineTo(size.width, pointer.y); context.stroke();
  }
  document.querySelector('#zoom-readout').textContent = `ZOOM ${Math.round(state.zoom * 100)}%`;
  document.querySelector('#frame-readout').textContent = `FRAME ${sequence} · ${paused ? 'PAUSED' : 'SYNTHETIC'}`;
  inspect();
}
function point(event) { const rect = canvas.getBoundingClientRect(); return { x: event.clientX - rect.left, y: event.clientY - rect.top }; }
canvas.addEventListener('wheel', event => { event.preventDefault(); pointer = point(event); changeZoom(Math.exp(-Math.sign(event.deltaY) * .18), pointer); }, { passive: false });
canvas.addEventListener('pointerdown', event => { if (event.button !== 0) return; pointer = point(event); drag = { ...pointer }; canvas.setPointerCapture(event.pointerId); canvas.focus({ preventScroll: true }); });
canvas.addEventListener('pointermove', event => {
  pointer = point(event);
  if (drag) { const size = bounds(); state = constrain({ ...state, x: state.x + pointer.x - drag.x, y: state.y + pointer.y - drag.y }, size.width, size.height, width, height); drag = { ...pointer }; fitted = false; }
  render();
});
for (const name of ['pointerup', 'pointercancel', 'lostpointercapture']) canvas.addEventListener(name, () => { drag = null; });
canvas.addEventListener('pointerleave', () => { if (!drag) { pointer = null; render(); } });
canvas.addEventListener('keydown', event => {
  if (event.key === '+' || event.key === '=') changeZoom(1.2);
  else if (event.key === '-') changeZoom(1 / 1.2);
  else if (event.key.toLowerCase() === 'f') setFit();
  else if (event.key.startsWith('Arrow')) { const size = bounds(); state = constrain({ ...state, x: state.x + (event.key === 'ArrowLeft' ? 30 : event.key === 'ArrowRight' ? -30 : 0), y: state.y + (event.key === 'ArrowUp' ? 30 : event.key === 'ArrowDown' ? -30 : 0) }, size.width, size.height, width, height); fitted = false; render(); }
  else return;
  event.preventDefault();
});
document.querySelector('#fit').addEventListener('click', setFit);
document.querySelector('#actual').addEventListener('click', () => { const size = bounds(); state = constrain({ zoom: 1, x: (size.width - width) / 2, y: (size.height - height) / 2 }, size.width, size.height, width, height); fitted = false; render(); });
document.querySelector('#plus').addEventListener('click', () => changeZoom(1.2));
document.querySelector('#minus').addEventListener('click', () => changeZoom(1 / 1.2));
function updatePause() { pauseButton.textContent = paused ? 'Resume' : 'Pause'; pauseButton.setAttribute('aria-pressed', String(paused)); render(); }
pauseButton.addEventListener('click', () => { paused = !paused; updatePause(); });
sourceSelect.addEventListener('change', () => { mode = sourceSelect.value; dirty = true; makeSource(); render(); });
overlays.addEventListener('change', render);
new ResizeObserver(() => { if (fitted) setFit(); else { const size = bounds(); state = constrain(state, size.width, size.height, width, height); render(); } }).observe(shell);
makeSource(); setFit(); updatePause();
function tick(time) {
  if (!document.hidden && (visible.hero || visible.demo) && (dirty || (!paused && time - lastTick >= 100))) {
    if (!paused && !dirty) sequence++;
    lastTick = time; makeSource(); dirty = false;
    if (visible.demo) render();
    if (visible.hero) {
      heroContext.clearRect(0, 0, width, height); heroContext.drawImage(sourceCanvas, 0, 0);
      if (mode === 'inspection') drawOverlay(heroContext, { zoom: 1, x: 0, y: 0 });
    }
  }
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);
for (const button of document.querySelectorAll('[data-copy]')) button.addEventListener('click', async () => {
  try { await navigator.clipboard.writeText(button.dataset.copy); button.textContent = 'Copied'; document.querySelector('#copy-status').textContent = 'Command copied.'; setTimeout(() => { button.textContent = 'Copy'; }, 1800); }
  catch { document.querySelector('#copy-status').textContent = 'Clipboard unavailable. Select the command to copy it manually.'; button.textContent = 'Select command'; }
});
function selectTab(id, focus = false) {
  for (const name of ['raw', 'opencv']) {
    const tab = document.querySelector(`#tab-${name}`), selected = id === name;
    tab.setAttribute('aria-selected', String(selected)); tab.tabIndex = selected ? 0 : -1;
    document.querySelector(`#code-${name}`).hidden = !selected;
    if (selected && focus) tab.focus();
  }
}
for (const name of ['raw', 'opencv']) {
  const tab = document.querySelector(`#tab-${name}`);
  tab.addEventListener('click', () => selectTab(name));
  tab.addEventListener('keydown', event => { if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) { event.preventDefault(); selectTab(event.key === 'Home' ? 'raw' : event.key === 'End' ? 'opencv' : name === 'raw' ? 'opencv' : 'raw', true); } });
}
if (document.modelContext?.registerTool) {
  const lifecycle = new AbortController();
  const register = tool => { try { Promise.resolve(document.modelContext.registerTool(tool, { signal: lifecycle.signal })).catch(() => {}); } catch { /* Optional browser capability. */ } };
  register({ name: 'read_viewport_demo', description: 'Read the browser illustration state. This is not SDK performance evidence.', inputSchema: { type: 'object', properties: {}, additionalProperties: false }, annotations: { readOnlyHint: true }, execute: () => ({ source: mode, paused, zoom: state.zoom, frame: sequence, illustrationOnly: true }) });
  register({ name: 'configure_viewport_demo', description: 'Set the synthetic browser demo source and pause state using the same controls as the visible page.', inputSchema: { type: 'object', properties: { source: { type: 'string', enum: ['inspection', 'gray16'] }, paused: { type: 'boolean' } }, required: ['source', 'paused'], additionalProperties: false }, annotations: { readOnlyHint: false }, execute: input => {
    if (!input || !['inspection', 'gray16'].includes(input.source) || typeof input.paused !== 'boolean' || Object.keys(input).some(key => !['source', 'paused'].includes(key))) throw new Error('Invalid demo configuration.');
    mode = input.source; sourceSelect.value = mode; paused = input.paused; dirty = true; makeSource(); updatePause();
    return { source: mode, paused, illustrationOnly: true };
  } });
  addEventListener('pagehide', () => lifecycle.abort(), { once: true });
}
