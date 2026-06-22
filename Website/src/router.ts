import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

type SeoMeta = {
  title: string
  description: string
  canonicalPath: string
  ogImage?: string
  ogImageAlt?: string
}

const SITE_URL = 'https://fuse-inventory.dev'
const OG_IMAGE_WIDTH = 1200
const OG_IMAGE_HEIGHT = 630
const DEFAULT_OG_IMAGE = `${SITE_URL}/og-image.png`

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
  const ogImage = meta.ogImage ?? DEFAULT_OG_IMAGE
  const ogAlt = meta.ogImageAlt ?? meta.title

  document.title = meta.title
  upsertMetaTag('name', 'description', meta.description)

  // Open Graph
  upsertMetaTag('property', 'og:title', meta.title)
  upsertMetaTag('property', 'og:description', meta.description)
  upsertMetaTag('property', 'og:url', canonicalUrl)
  upsertMetaTag('property', 'og:type', 'website')
  upsertMetaTag('property', 'og:site_name', 'Fuse Inventory')
  upsertMetaTag('property', 'og:image', ogImage)
  upsertMetaTag('property', 'og:image:alt', ogAlt)
  upsertMetaTag('property', 'og:image:type', 'image/png')
  upsertMetaTag('property', 'og:image:width', String(OG_IMAGE_WIDTH))
  upsertMetaTag('property', 'og:image:height', String(OG_IMAGE_HEIGHT))

  // Twitter
  upsertMetaTag('name', 'twitter:card', 'summary_large_image')
  upsertMetaTag('name', 'twitter:title', meta.title)
  upsertMetaTag('name', 'twitter:description', meta.description)
  upsertMetaTag('name', 'twitter:image', ogImage)
  upsertMetaTag('name', 'twitter:image:alt', ogAlt)

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
          canonicalPath: '/',
          ogImageAlt: 'Fuse Inventory dashboard overview showing infrastructure inventory'
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
          title: 'Features | Fuse Inventory — CMDB, Dependency Mapping, Blast Radius & More',
          description:
            'Explore Fuse Inventory features: inventory modeling, dependency graphing, blast radius analysis, SQL permission drift detection, Azure integrations, and documentation mode.',
          canonicalPath: '/features',
          ogImageAlt: 'Fuse Inventory features — CMDB, dependency mapping, blast radius analysis, and more'
        }
      }
    },
    {
      path: '/screenshots',
      name: 'screenshots',
      component: () => import('@/views/ScreenshotsView.vue'),
      meta: {
        seo: {
          title: 'Screenshots | Fuse Inventory — See It in Action',
          description:
            'View real screenshots of the Fuse Inventory dark-mode interface — dashboard, dependency graph, blast radius, Azure integration, audit logs, and security views.',
          canonicalPath: '/screenshots',
          ogImageAlt: 'Fuse Inventory interface — dark mode dashboard, dependency graphs, and blast radius simulation'
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