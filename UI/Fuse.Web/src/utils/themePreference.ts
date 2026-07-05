const THEME_PREFERENCE_KEY = 'fuse-theme-preference'

export type ThemePreference = 'light' | 'dark'

export function getInitialTheme(): boolean | 'auto' {
  try {
    const preference = localStorage.getItem(THEME_PREFERENCE_KEY)
    if (preference === 'dark') return true
    if (preference === 'light') return false
  } catch {
    // Fall back to the operating-system preference when storage is unavailable.
  }

  return 'auto'
}

export function saveThemePreference(preference: ThemePreference): void {
  try {
    localStorage.setItem(THEME_PREFERENCE_KEY, preference)
  } catch {
    // The selected theme still applies for this page even if storage is unavailable.
  }
}
