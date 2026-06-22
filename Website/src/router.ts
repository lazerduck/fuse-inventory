import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

type SeoMeta = {
  title: string
  description: string
  canonicalPath: string
}

const SITE_URL = 'https://fuse-inventory.dev'

const defaultSeo: SeoMeta = {
  title: 'Fuse Inventory — Self-Hosted Infrastructure Inventory & Dependency Mapping',
  description:
    'Map applications, environments, dependencies, and credentials in a single self-hosted tool. Visualize your infrastructure with interactive graphs and blast-radius simulation.',
  canonicalPath: '/'
}

function upsertMetaTag(attribute: 'name' | 'property', key: string, content: string) {
  let tag = document.head.querySelector(`meta[${attribute}="${key}"]`)
  if (!tag) {
    tag = document.createElement('meta')
    tag.setAttribute(attribute, key)
    document.head.appendChild(tag)
  }
  tag.setAttribute('content', content)
}

function upsertCanonicalLink(href: string) {
  let canonical = document.head.querySelector('link[rel="canonical"]')
  if (!canonical) {
    canonical = document.createElement('link')
    canonical.setAttribute('rel', 'canonical')
    document.head.appendChild(canonical)
  }
  canonical.setAttribute('href', href)
}

function applySeoMeta(meta: SeoMeta) {
  const canonicalUrl = `${SITE_URL}${meta.canonicalPath}`
  const previewImageUrl = `${SITE_URL}/icons/favicon-512x512.png`

  document.title = meta.title
  upsertMetaTag('name', 'description', meta.description)

  upsertMetaTag('property', 'og:title', meta.title)
  upsertMetaTag('property', 'og:description', meta.description)
  upsertMetaTag('property', 'og:url', canonicalUrl)
  upsertMetaTag('property', 'og:type', 'website')
  upsertMetaTag('property', 'og:site_name', 'Fuse Inventory')
  upsertMetaTag('property', 'og:image', previewImageUrl)
  upsertMetaTag('property', 'og:image:alt', 'Fuse Inventory logo')

  upsertMetaTag('name', 'twitter:card', 'summary_large_image')
  upsertMetaTag('name', 'twitter:title', meta.title)
  upsertMetaTag('name', 'twitter:description', meta.description)
  upsertMetaTag('name', 'twitter:image', previewImageUrl)

  upsertCanonicalLink(canonicalUrl)
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
      meta: {
        seo: {
          title: 'Fuse Inventory — Self-Hosted Infrastructure Inventory & Dependency Mapping',
          description:
            'Map applications, environments, dependencies, and credentials in a single self-hosted tool. Visualize your infrastructure with interactive graphs and blast-radius simulation.',
          canonicalPath: '/'
        }
      }
    },
    {
      path: '/features',
      name: 'features',
      // route level code-splitting — this gets split into its own chunk
      component: () => import('@/views/FeaturesView.vue'),
      meta: {
        seo: {
          title: 'Features | Fuse Inventory',
          description:
            'Explore Fuse Inventory features: inventory modeling, dependency graphing, blast radius analysis, SQL permission drift detection, Azure integrations, and documentation mode.',
          canonicalPath: '/features'
        }
      }
    },
    {
      path: '/screenshots',
      name: 'screenshots',
      component: () => import('@/views/ScreenshotsView.vue'),
      meta: {
        seo: {
          title: 'Screenshots | Fuse Inventory',
          description:
            'View screenshots of the Fuse Inventory interface, including dashboard, dependency graph, blast radius analysis, integrations, audit logs, and security views.',
          canonicalPath: '/screenshots'
        }
      }
    }
  ]
})

router.afterEach((to) => {
  const seoMeta = (to.meta.seo as SeoMeta | undefined) ?? defaultSeo
  applySeoMeta(seoMeta)
})

export default router