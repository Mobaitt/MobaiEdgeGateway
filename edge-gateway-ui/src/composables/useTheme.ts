import { computed, ref } from 'vue'

export type ThemeMode = 'dark' | 'light'

const STORAGE_KEY = 'edge-gateway-theme'
const theme = ref<ThemeMode>('dark')
let initialized = false

function applyTheme(nextTheme: ThemeMode) {
  theme.value = nextTheme
  document.documentElement.setAttribute('data-theme', nextTheme)
  window.localStorage.setItem(STORAGE_KEY, nextTheme)
}

function resolveInitialTheme(): ThemeMode {
  const stored = window.localStorage.getItem(STORAGE_KEY)
  if (stored === 'dark' || stored === 'light')
    return stored

  return 'dark'
}

export function initializeTheme() {
  if (initialized)
    return

  initialized = true
  applyTheme(resolveInitialTheme())
}

export function useTheme() {
  const isDark = computed(() => theme.value === 'dark')

  const toggleTheme = () => {
    applyTheme(theme.value === 'dark' ? 'light' : 'dark')
  }

  const setTheme = (nextTheme: ThemeMode) => {
    applyTheme(nextTheme)
  }

  return {
    theme,
    isDark,
    toggleTheme,
    setTheme
  }
}
