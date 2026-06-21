<template>
  <div class="bg-grey-2">
    <section class="screenshots-hero section section--alt">
      <div class="container container--screenshots-hero">
        <span class="page-badge screenshots-hero__badge">Screenshots</span>
        <h1 class="screenshots-hero__title">See it in action</h1>
        <p class="screenshots-hero__subtitle">
          A look at the Fuse-Inventory interface — dark mode by default, responsive, and designed for information density.
        </p>
      </div>
    </section>

    <section class="gallery-section section">
      <div class="container container--screenshots">
        <div class="gallery-grid">
          <div
            v-for="item in galleryItems"
            :key="item.title"
            :class="['gallery-item', { 'gallery-item--large': item.large }]"
          >
            <button
              class="gallery-item__frame"
              type="button"
              :aria-label="`Open ${item.title} screenshot`"
              @click="openLightbox(item)"
            >
              <img :src="item.src" :alt="item.alt" />
              <div class="gallery-item__overlay">
                <i class="fa-solid fa-magnifying-glass-plus gallery-item__overlay-icon"></i>
              </div>
            </button>
            <div class="gallery-item__info">
              <h3>{{ item.title }}</h3>
              <p>{{ item.description }}</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section class="cta-section section">
      <div class="container container--narrow text-center">
        <h2>Want to try it yourself?</h2>
        <p class="section__description">
          Get it running in under a minute with a single Docker command.
        </p>
        <pre class="cta-section__code"><code>docker run -d \
  --name fuse-inventory \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  ghcr.io/lazerduck/fuse-inventory:latest</code></pre>
        <div class="hero__actions hero__actions--center">
          <a class="cta-btn cta-btn--solid" href="https://github.com/users/lazerduck/packages/container/package/fuse-inventory" target="_blank">
            <i class="fab fa-docker"></i> Get Started
          </a>
          <a class="cta-btn cta-btn--outline" href="https://github.com/lazerduck/fuse-inventory" target="_blank">
            <i class="fab fa-github"></i> View on GitHub
          </a>
        </div>
      </div>
    </section>

    <teleport to="body">
      <div
        v-if="lightboxImage !== null"
        class="lightbox"
        role="dialog"
        aria-modal="true"
        tabindex="0"
        @click.self="closeLightbox"
        @keydown.esc="closeLightbox"
      >
        <button class="lightbox__close" type="button" aria-label="Close image preview" @click="closeLightbox">
          <i class="fa-solid fa-xmark"></i>
        </button>
        <figure class="lightbox__panel">
          <img :src="lightboxImage.src" :alt="lightboxImage.alt" class="lightbox__image" />
          <figcaption class="lightbox__caption">{{ lightboxImage.alt }}</figcaption>
        </figure>
      </div>
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import homeDashboard from '@/assets/home-dashboard.png'
import dependencyGraph from '@/assets/dependency-graph.png'
import blastRadius from '@/assets/blast-radius.webp'
import azureIntegration from '@/assets/azure-integration.webp'
import documentationMode from '@/assets/documentation-mode.webp'
import accountEdit from '@/assets/account-edit.webp'
import auditLog from '@/assets/audit.webp'
import risks from '@/assets/risks.webp'
import configuration from '@/assets/configuration.webp'
import security from '@/assets/security.webp'
import passwordGenerator from '@/assets/password-generator.webp'

type GalleryItem = {
  title: string
  description: string
  alt: string
  src: string
  large?: boolean
}

const galleryItems: GalleryItem[] = [
  {
    title: 'Dashboard',
    description: 'Explore your estate at a glance — applications, platforms, environments, and external resources in a clean, filterable grid.',
    alt: 'Dashboard overview',
    src: homeDashboard,
    large: true
  },
  {
    title: 'Dependency Graph',
    description: 'Interactive visualization of relationships between entities across environments.',
    alt: 'Dependency graph',
    src: dependencyGraph
  },
  {
    title: 'Blast Radius',
    description: 'Simulate failures to see exactly what downstream systems are affected.',
    alt: 'Blast radius analysis',
    src: blastRadius
  },
  {
    title: 'Azure Integration',
    description: 'Key Vault and App Configuration management from within Fuse.',
    alt: 'Azure Key Vault integration',
    src: azureIntegration
  },
  {
    title: 'Documentation Mode',
    description: 'Read-only, public-facing view with global search — no login required.',
    alt: 'Documentation mode',
    src: documentationMode
  },
  {
    title: 'Accounts & Credentials',
    description: 'Track credentials with database grants, secret bindings, and clone support.',
    alt: 'Account credentials management',
    src: accountEdit
  },
  {
    title: 'Audit Log',
    description: 'Every change tracked with 100+ distinct action types and powerful filtering.',
    alt: 'Audit log',
    src: auditLog
  },
  {
    title: 'Risk Management',
    description: 'Structured risk records with impact, likelihood, status lifecycle, and ownership.',
    alt: 'Risk management',
    src: risks
  },
  {
    title: 'Documentation Completeness',
    description: 'Find gaps in your infrastructure documentation at a glance.',
    alt: 'Documentation completeness',
    src: documentationMode
  },
  {
    title: 'Configuration',
    description: 'Browse and manage Azure App Configuration key-values from within Fuse.',
    alt: 'App configuration',
    src: configuration
  },
  {
    title: 'Security',
    description: 'Three security modes, role-based access, API keys, and user management.',
    alt: 'Security settings',
    src: security
  },
  {
    title: 'Password Generator',
    description: 'Built-in secure password generation with configurable character set and length.',
    alt: 'Password generator',
    src: passwordGenerator
  }
]

const lightboxImage = ref<GalleryItem | null>(null)

function openLightbox(item: GalleryItem) {
  lightboxImage.value = item
}

function closeLightbox() {
  lightboxImage.value = null
}
</script>

<style scoped>
.screenshots-hero {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
  text-align: center;
  padding: 5rem 0;
}

.screenshots-hero__badge {
  font-size: 0.9rem;
  margin-bottom: 1.5rem;
}

.screenshots-hero__title {
  font-size: 2.75rem;
  font-weight: 700;
  margin: 0 0 1rem 0;
  line-height: 1.2;
  color: white;
}

.screenshots-hero__subtitle {
  font-size: 1.15rem;
  color: rgba(255,255,255,0.7);
  max-width: 650px;
  margin: 0 auto;
  line-height: 1.6;
}

.container--screenshots-hero {
  max-width: 960px;
}

.container--screenshots {
  max-width: 1440px;
}

.gallery-section {
  padding: 4rem 0;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1.5rem;
}

.gallery-item--large {
  grid-column: span 2;
}

.gallery-item {
  background: white;
  border: 1px solid rgba(26, 26, 46, 0.08);
  border-radius: 22px;
  padding: 1rem;
  box-shadow: 0 18px 48px rgba(26, 26, 46, 0.08);
}

.gallery-item__frame {
  position: relative;
  width: 100%;
  border: 0;
  padding: 0;
  display: block;
  background: transparent;
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid rgba(0,0,0,0.06);
  box-shadow: 0 4px 16px rgba(0,0,0,0.06);
  transition: transform 0.2s, box-shadow 0.2s;
  cursor: pointer;
}

.gallery-item__frame:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 32px rgba(0,0,0,0.12);
}

.gallery-item__frame:focus-visible {
  outline: 3px solid rgba(25, 118, 210, 0.35);
  outline-offset: 3px;
}

.gallery-item__frame img {
  width: 100%;
  display: block;
  aspect-ratio: 16 / 10;
  object-fit: cover;
}

.gallery-item__overlay {
  position: absolute;
  inset: 0;
  background: rgba(0,0,0,0.3);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s;
}

.gallery-item__overlay-icon {
  color: white;
  font-size: 24px;
}

.gallery-item__frame:hover .gallery-item__overlay {
  opacity: 1;
}

.gallery-item__info {
  padding: 1rem 0.25rem 0.25rem;
}

.gallery-item__info h3 {
  font-size: 1.05rem;
  font-weight: 600;
  color: #1a1a2e;
  margin: 0 0 0.35rem 0;
}

.gallery-item__info p {
  font-size: 0.9rem;
  color: #4a4a6a;
  margin: 0;
  line-height: 1.4;
}

.cta-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.75rem;
  font-size: 1rem;
  font-weight: 600;
  border-radius: 6px;
  text-decoration: none;
  cursor: pointer;
  transition: opacity 0.2s;
}

.cta-btn:hover {
  opacity: 0.85;
}

.cta-btn--solid {
  background: white;
  color: #1976d2;
  border: none;
}

.cta-btn--outline {
  background: transparent;
  color: white;
  border: 2px solid rgba(255,255,255,0.7);
}

.hero__actions--center {
  justify-content: center;
}

.lightbox {
  position: fixed;
  inset: 0;
  z-index: 2000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  background: rgba(10, 12, 25, 0.86);
  backdrop-filter: blur(12px);
}

.lightbox__panel {
  width: min(1200px, 100%);
  margin: 0;
}

.lightbox__image {
  width: 100%;
  max-height: 80vh;
  object-fit: contain;
  display: block;
  border-radius: 18px;
  box-shadow: 0 30px 80px rgba(0, 0, 0, 0.45);
  background: #0b1020;
}

.lightbox__caption {
  margin-top: 0.75rem;
  text-align: center;
  color: rgba(255, 255, 255, 0.85);
  font-size: 0.95rem;
}

.lightbox__close {
  position: fixed;
  top: 1.25rem;
  right: 1.25rem;
  width: 3rem;
  height: 3rem;
  border: 0;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.12);
  color: white;
  cursor: pointer;
  display: grid;
  place-items: center;
  font-size: 1.15rem;
  transition: background 0.2s, transform 0.2s;
}

.lightbox__close:hover {
  background: rgba(255, 255, 255, 0.2);
  transform: scale(1.04);
}

.cta-section {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
}

.cta-section__code {
  background: rgba(0,0,0,0.3);
  border-radius: 12px;
  padding: 1.5rem 2rem;
  font-size: 0.95rem;
  overflow-x: auto;
  margin-bottom: 2rem;
  border: 1px solid rgba(255,255,255,0.1);
  max-width: 600px;
  margin-left: auto;
  margin-right: auto;
}

.cta-section__code code {
  color: #a0ffa0;
  font-family: 'Fira Code', 'JetBrains Mono', monospace;
}

@media (max-width: 959px) {
  .container--screenshots,
  .container--screenshots-hero {
    max-width: 960px;
  }

  .gallery-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .gallery-item--large {
    grid-column: span 2;
  }
}

@media (max-width: 599px) {
  .container--screenshots,
  .container--screenshots-hero {
    max-width: 100%;
  }

  .gallery-grid {
    grid-template-columns: 1fr;
  }

  .gallery-item--large {
    grid-column: span 1;
  }

  .screenshots-hero__title {
    font-size: 2rem;
  }

  .lightbox {
    padding: 1rem;
  }

  .lightbox__close {
    top: 0.75rem;
    right: 0.75rem;
  }
}
</style>