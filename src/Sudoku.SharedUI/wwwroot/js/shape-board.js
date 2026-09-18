window.sudokuShape = {
  norm(el, x, y) {
    const r = el.getBoundingClientRect();
    if (!r.width || !r.height) return null;
    return [(x - r.left) / r.width, (y - r.top) / r.height];
  }
};
