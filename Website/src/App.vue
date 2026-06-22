<script setup lang="ts">
import { RouterView } from 'vue-router'
import Navbar from '@/components/Navbar.vue'
import fuseLogo from '@/assets/fuse-logo.png'
</script>

<template>
  <div id="app-root">
    <Navbar />
    <main>
      <RouterView />
    </main>
    <footer class="marketing-footer">
      <div class="marketing-footer__inner">
        <div class="marketing-footer__brand">
          <img :src="fuseLogo" alt="Fuse Inventory logo" class="marketing-footer__logo" />
          <div>
            <span class="marketing-footer__title">Fuse Inventory</span>
            <span class="marketing-footer__tagline">Self-hosted infrastructure inventory</span>
          </div>
        </div>
        <div class="marketing-footer__links">
          <a
            href="https://github.com/lazerduck/fuse-inventory"
            target="_blank"
            rel="noopener"
          >
            <svg height="18" viewBox="0 0 16 16" width="18" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
              <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
            </svg>
            GitHub
          </a>
          <a
            href="https://github.com/users/lazerduck/packages/container/package/fuse-inventory"
            target="_blank"
            rel="noopener"
          >
            Container Registry
          </a>
          <a
            href="https://github.com/lazerduck/fuse-inventory/blob/main/README.md"
            target="_blank"
            rel="noopener"
          >
            Documentation
          </a>
        </div>
      </div>
    </footer>
  </div>
</template>

<style>
html, body {
  margin: 0;
  padding: 0;
  height: 100%;
  overflow-x: hidden;
}
#app {
  height: 100%;
}

.marketing-footer {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
  padding: 3rem 2rem;
  margin-top: 4rem;
}

.marketing-footer__inner {
  max-width: 1200px;
  margin: 0 auto;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 2rem;
}

.marketing-footer__brand {
  display: inline-flex;
  align-items: center;
  gap: 0.9rem;
}

.marketing-footer__logo {
  width: 46px;
  height: 46px;
  object-fit: contain;
  padding: 0.35rem;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.14);
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.22);
}

.marketing-footer__title {
  display: block;
  font-size: 1.25rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
  color: white;
}

.marketing-footer__tagline {
  display: block;
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.7);
}

.marketing-footer__links {
  display: flex;
  gap: 1.5rem;
  align-items: center;
}

.marketing-footer__links a {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  color: white;
  text-decoration: none;
  font-weight: 500;
  font-size: 0.95rem;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  transition: all 0.3s ease;
  border: 1px solid transparent;
}

.marketing-footer__links a:hover {
  background: rgba(255, 255, 255, 0.1);
  border-color: rgba(255, 255, 255, 0.2);
  transform: translateY(-1px);
}

.marketing-footer__links svg {
  opacity: 0.9;
  transition: opacity 0.3s ease;
}

.marketing-footer__links a:hover svg {
  opacity: 1;
}

@media (max-width: 768px) {
  .marketing-footer__inner {
    flex-direction: column;
    align-items: flex-start;
  }

  .marketing-footer__logo {
    width: 42px;
    height: 42px;
  }
  
  .marketing-footer__links {
    flex-wrap: wrap;
  }
}
</style>