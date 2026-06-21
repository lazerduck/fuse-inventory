<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'

const menuVisible = ref(false)
const scrolled = ref(false)

const navItems = [
  { label: 'Home', path: '/' },
  { label: 'Features', path: '/features' },
  { label: 'Screenshots', path: '/screenshots' }
]

const router = useRouter()

function isActive(path: string): boolean {
  if (path === '/') return router.currentRoute.value.path === '/'
  return router.currentRoute.value.path.startsWith(path)
}

function navigate(path: string) {
  router.push(path)
  menuVisible.value = false
}

function onScroll() {
  scrolled.value = window.scrollY > 10
}

onMounted(() => {
  window.addEventListener('scroll', onScroll, { passive: true })
})
onUnmounted(() => {
  window.removeEventListener('scroll', onScroll)
})
</script>

<template>
  <header :class="['marketing-header', { 'marketing-header--scrolled': scrolled }]">
    <div class="marketing-header__inner">
      <!-- Brand -->
      <a href="/" class="marketing-header__brand" @click.prevent="navigate('/')">
        <svg class="marketing-header__logo" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <rect x="2" y="2" width="20" height="20" rx="4" stroke="currentColor" stroke-width="2"/>
          <path d="M7 8h10M7 12h10M7 16h6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
        <span>Fuse Inventory</span>
      </a>

      <!-- Desktop nav links -->
      <nav class="marketing-header__links">
        <a
          v-for="item in navItems"
          :key="item.path"
          :href="item.path"
          :class="['marketing-header__link', { 'marketing-header__link--active': isActive(item.path) }]"
          @click.prevent="navigate(item.path)"
        >
          {{ item.label }}
        </a>
      </nav>

      <!-- GitHub button -->
      <a
        href="https://github.com/lazerduck/fuse-inventory"
        target="_blank"
        rel="noopener"
        class="marketing-header__github-btn"
      >
        <svg height="18" viewBox="0 0 16 16" width="18" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
          <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
        </svg>
        GitHub
      </a>

      <!-- Mobile menu toggle -->
      <button
        :class="['marketing-header__menu-btn', { 'marketing-header__menu-btn--open': menuVisible }]"
        @click="menuVisible = !menuVisible"
        :aria-label="menuVisible ? 'Close menu' : 'Open menu'"
        :aria-expanded="menuVisible"
      >
        <span></span>
        <span></span>
        <span></span>
      </button>
    </div>

    <!-- Mobile menu -->
    <transition name="marketing-header-slide">
      <div v-if="menuVisible" class="marketing-header__mobile">
        <a
          v-for="item in navItems"
          :key="item.path"
          :href="item.path"
          :class="['marketing-header__mobile-link', { 'marketing-header__mobile-link--active': isActive(item.path) }]"
          @click="navigate(item.path)"
        >
          {{ item.label }}
        </a>
        <a
          href="https://github.com/lazerduck/fuse-inventory"
          target="_blank"
          rel="noopener"
          class="marketing-header__mobile-cta"
        >
          <svg height="18" viewBox="0 0 16 16" width="18" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
          </svg>
          <span>View on GitHub</span>
        </a>
      </div>
    </transition>
  </header>
</template>

<style scoped>
.marketing-header {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 100;
  transition: box-shadow 0.3s ease;
}

.marketing-header__inner {
  padding: 0 2rem;
  height: 72px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  /* Always have a solid background bar */
  background: #0a0a18;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  /* Full width - no max-width constraint */
  width: 100%;
  box-sizing: border-box;
}

/* Scrolled state: add blur overlay on top of solid background */
.marketing-header--scrolled .marketing-header__inner {
  background: rgba(10, 10, 24, 0.92);
  backdrop-filter: blur(16px) saturate(1.5);
  -webkit-backdrop-filter: blur(16px) saturate(1.5);
  box-shadow: 0 2px 20px rgba(0, 0, 0, 0.2);
}

/* Brand */
.marketing-header__brand {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  text-decoration: none;
  color: white;
  transition: opacity 0.2s;
}

.marketing-header__brand:hover {
  opacity: 0.85;
}

.marketing-header__logo {
  width: 28px;
  height: 28px;
  flex-shrink: 0;
}

.marketing-header__brand span {
  font-size: 1.1rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  white-space: nowrap;
}

/* Desktop nav links */
.marketing-header__links {
  display: flex;
  align-items: center;
  gap: 0.2rem;
}

.marketing-header__link {
  padding: 0.45rem 0.85rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.7);
  text-decoration: none;
  border-radius: 8px;
  transition: color 0.2s ease, background-color 0.2s ease;
  cursor: pointer;
}

.marketing-header__link:hover {
  color: rgba(255, 255, 255, 1);
  background: rgba(255, 255, 255, 0.08);
}

.marketing-header__link--active {
  color: white;
  background: rgba(255, 255, 255, 0.1);
}

/* GitHub button */
.marketing-header__github-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.4rem 0.85rem;
  font-size: 0.825rem;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.85);
  background: rgba(255, 255, 255, 0.07);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 8px;
  text-decoration: none;
  transition: background 0.2s ease, border-color 0.2s ease, color 0.2s ease;
}

.marketing-header__github-btn:hover {
  background: rgba(255, 255, 255, 0.13);
  border-color: rgba(255, 255, 255, 0.22);
  color: white;
}

/* Mobile menu button */
.marketing-header__menu-btn {
  display: none;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  gap: 5px;
  width: 36px;
  height: 36px;
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px;
  border-radius: 8px;
  transition: background-color 0.2s ease;
}

.marketing-header__menu-btn:hover {
  background: rgba(255, 255, 255, 0.08);
}

.marketing-header__menu-btn span {
  display: block;
  width: 18px;
  height: 2px;
  background: rgba(255, 255, 255, 0.75);
  border-radius: 2px;
  transition: all 0.3s ease;
  transform-origin: center;
}

.marketing-header__menu-btn--open span:nth-child(1) {
  transform: translateY(7px) rotate(45deg);
}

.marketing-header__menu-btn--open span:nth-child(2) {
  opacity: 0;
  transform: scaleX(0);
}

.marketing-header__menu-btn--open span:nth-child(3) {
  transform: translateY(-7px) rotate(-45deg);
}

/* Mobile menu dropdown */
.marketing-header__mobile {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: rgba(10, 10, 24, 0.97);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border-top: 1px solid rgba(255, 255, 255, 0.06);
  padding: 0.75rem 0;
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.marketing-header__mobile-link {
  display: block;
  padding: 0.8rem 2rem;
  font-size: 1rem;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.7);
  text-decoration: none;
  transition: color 0.2s, background-color 0.2s;
}

.marketing-header__mobile-link:hover,
.marketing-header__mobile-link--active {
  color: white;
  background: rgba(255, 255, 255, 0.06);
}

.marketing-header__mobile-cta {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  margin: 0.5rem 1.25rem;
  padding: 0.75rem 1rem;
  font-size: 0.9rem;
  font-weight: 600;
  color: white;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 10px;
  text-decoration: none;
  transition: background 0.2s;
}

.marketing-header__mobile-cta:hover {
  background: rgba(255, 255, 255, 0.14);
}

/* Slide transition */
.marketing-header-slide-enter-active,
.marketing-header-slide-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.marketing-header-slide-enter-from,
.marketing-header-slide-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

/* Responsive */
@media (max-width: 768px) {
  .marketing-header__inner {
    padding: 0 1.25rem;
    height: 64px;
  }

  .marketing-header__links,
  .marketing-header__github-btn {
    display: none;
  }

  .marketing-header__menu-btn {
    display: flex;
  }

  .marketing-header__mobile {
    padding: 0.25rem 0;
  }

  .marketing-header__brand span {
    font-size: 1rem;
  }
}
</style>