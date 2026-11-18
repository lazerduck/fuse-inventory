// jsdom has no layout engine. Quasar uses these browser APIs while rendering
// tabs and scrolling select options; actual layout is covered in Playwright.
globalThis.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

Element.prototype.scrollTo = function (options?: ScrollToOptions | number, y?: number) {
  if (typeof options === 'number') {
    this.scrollLeft = options
    this.scrollTop = y ?? 0
  } else {
    if (options?.left !== undefined) this.scrollLeft = options.left
    if (options?.top !== undefined) this.scrollTop = options.top
  }
}

// jsdom parses calc-size(), but cannot lay out or animate intrinsic heights.
// Keep Quasar on its measured transition fallback; Playwright covers animation.
if (typeof CSS !== 'undefined' && typeof CSS.supports === 'function' && !Element.prototype.animate) {
  const supports = CSS.supports.bind(CSS)
  CSS.supports = (property: string, value?: string) => {
    if (property === 'height' && value === 'calc-size(auto, size)') return false
    return value === undefined ? supports(property) : supports(property, value)
  }
}
