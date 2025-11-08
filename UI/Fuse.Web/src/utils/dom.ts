export function waitForElement<T extends Element = Element>(
  selector: string,
  timeout = 10000,
  interval = 100
): Promise<T> {
  if (typeof document === 'undefined') {
    return Promise.reject(new Error('waitForElement can only be used in a browser environment.'))
  }

  const existing = document.querySelector<T>(selector)
  if (existing) {
    return Promise.resolve(existing)
  }

  return new Promise<T>((resolve, reject) => {
    const start = Date.now()

    const intervalId = window.setInterval(() => {
      const element = document.querySelector<T>(selector)
      if (element) {
        window.clearInterval(intervalId)
        window.clearTimeout(timeoutId)
        resolve(element)
        return
      }

      if (Date.now() - start >= timeout) {
        window.clearInterval(intervalId)
        window.clearTimeout(timeoutId)
        reject(new Error(`Element matching selector "${selector}" was not found within ${timeout}ms.`))
      }
    }, interval)

    const timeoutId = window.setTimeout(() => {
      window.clearInterval(intervalId)
      reject(new Error(`Element matching selector "${selector}" was not found within ${timeout}ms.`))
    }, timeout)
  })
}
