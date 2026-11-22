const number = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 })

export function usedPercent(total?: number, available?: number) {
  return total && available != null ? 100 * (total - available) / total : undefined
}
export function percent(value?: number, suffix = '') {
  return value == null ? 'Unavailable' : number.format(value) + '%' + suffix
}
function bytes(value?: number) {
  if (value == null) return 'Unavailable'
  const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB']
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return number.format(value) + ' ' + units[unit]
}
export function capacity(available?: number, total?: number) {
  if (available != null && total != null) return bytes(available) + ' available of ' + bytes(total)
  if (available != null) return bytes(available) + ' available'
  return total == null ? 'Capacity unavailable' : bytes(total) + ' total'
}
