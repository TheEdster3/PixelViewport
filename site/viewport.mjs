// Browser illustration geometry. The .NET SDK remains the source of truth.
export function fit(width, height, imageWidth, imageHeight) {
  const zoom = Math.min(width / imageWidth, height / imageHeight);
  return { zoom, x: (width - imageWidth * zoom) / 2, y: (height - imageHeight * zoom) / 2 };
}
export function constrain(state, width, height, imageWidth, imageHeight) {
  const axis = (position, size, extent) => size <= extent ? (extent - size) / 2 : Math.min(0, Math.max(extent - size, position));
  return { zoom: state.zoom, x: axis(state.x, imageWidth * state.zoom, width), y: axis(state.y, imageHeight * state.zoom, height) };
}
export function zoomAt(state, zoom, anchor, width, height, imageWidth, imageHeight) {
  zoom = Math.min(16, Math.max(.05, zoom));
  const point = toImage(state, anchor);
  return constrain({ zoom, x: anchor.x - point.x * zoom, y: anchor.y - point.y * zoom }, width, height, imageWidth, imageHeight);
}
export function toImage(state, point) { return { x: (point.x - state.x) / state.zoom, y: (point.y - state.y) / state.zoom }; }
