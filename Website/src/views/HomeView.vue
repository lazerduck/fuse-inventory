<template>
  <div class="bg-white">

    <!-- Hero Section -->
    <section class="hero">
      <div class="hero__content container">
        <span class="page-badge hero__badge">⚡ Open Source &amp; Self-Hosted</span>
        <h1 class="hero__title">
          Know exactly what you've got deployed.
        </h1>
        <p class="hero__subtitle">
          Fuse-Inventory is a self-hosted configuration management database that gives DevOps teams a single, living map of their entire infrastructure — applications, dependencies, credentials, and risk.
        </p>
        <div class="hero__actions">
          <button class="cta-btn cta-btn--solid" type="button" @click="scrollTo('features')">
            Get Started
          </button>
          <a class="cta-btn cta-btn--outline" href="https://github.com/lazerduck/fuse-inventory" target="_blank">
            <i class="fab fa-github"></i> View on GitHub
          </a>
        </div>
      </div>
      <div class="hero__hero-image">
        <img src="@/assets/home-dashboard.png" alt="Fuse-Inventory dashboard showing infrastructure overview" class="hero__image" />
      </div>
    </section>

    <!-- Problem Section -->
    <section class="problem-section section">
      <div class="container">
        <h2 class="section__title">
          Your infrastructure is complex.<br/>
          <span class="text-primary">Your documentation shouldn't be.</span>
        </h2>
        <p class="section__description">
          Spreadsheets go stale. Confluence pages rot. Chat messages bury critical details. Teams lose visibility into what they've deployed, where it lives, and what it depends on.
        </p>
      </div>
    </section>

    <!-- Features Grid -->
    <section id="features" class="features-section section section--alt">
      <div class="container">
        <h2 class="section__title">
          Everything you need to stay in control.
        </h2>
        <p class="section__description">
          One tool. Full visibility. Zero overhead.
        </p>
        <div class="features-grid">

          <div class="feature-card">
            <div class="feature-card__icon bg-primary-soft">
              <i class="fa-solid fa-display" style="font-size: 32px; color: #1976d2;"></i>
            </div>
            <h3 class="feature-card__title">Live Dashboard</h3>
            <p>
              Explore your entire estate at a glance — applications, platforms, environments, and external resources in a clean, filterable grid.
            </p>
            <img src="@/assets/home-dashboard.png" alt="Dashboard view" class="feature-card__img" />
          </div>

          <div class="feature-card">
            <div class="feature-card__icon bg-primary-soft">
              <i class="fa-solid fa-network-wired" style="font-size: 32px; color: #1976d2;"></i>
            </div>
            <h3 class="feature-card__title">Dependency Graph</h3>
            <p>
              Visualize every connection in your stack with interactive, color-coded node diagrams. See how your services, databases, and external APIs relate.
            </p>
            <img src="@/assets/dependency-graph.png" alt="Dependency graph visualization" class="feature-card__img" />
          </div>

          <div class="feature-card">
            <div class="feature-card__icon bg-primary-soft">
              <i class="fa-solid fa-bolt" style="font-size: 32px; color: #1976d2;"></i>
            </div>
            <h3 class="feature-card__title">Blast Radius</h3>
            <p>
              Simulate failures instantly. Pick any resource and see exactly what breaks — direct and indirect impact across your entire environment.
            </p>
            <img src="@/assets/blast-radius.webp" alt="Blast radius simulation" class="feature-card__img" />
          </div>

          <div class="feature-card">
            <div class="feature-card__icon bg-primary-soft">
              <i class="fa-solid fa-shield-halved" style="font-size: 32px; color: #1976d2;"></i>
            </div>
            <h3 class="feature-card__title">SQL Drift Detection</h3>
            <p>
              Connect to your databases and detect permission drift between expected grants and actual database ACLs. Fix it in one click.
            </p>
            <img src="@/assets/security.webp" alt="SQL drift detection view" class="feature-card__img" />
          </div>

          <div class="feature-card">
            <div class="feature-card__icon bg-primary-soft">
              <i class="fa-solid fa-vault" style="font-size: 32px; color: #1976d2;"></i>
            </div>
            <h3 class="feature-card__title">Azure Key Vault</h3>
            <p>
              Browse, create, and rotate secrets directly from Fuse. Reference Key Vault secrets from your account configurations — no manual copy-paste.
            </p>
            <img src="@/assets/azure-integration.webp" alt="Azure Key Vault integration" class="feature-card__img" />
          </div>

          <div class="feature-card">
            <div class="feature-card__icon bg-primary-soft">
              <i class="fa-solid fa-book" style="font-size: 32px; color: #1976d2;"></i>
            </div>
            <h3 class="feature-card__title">Documentation Mode</h3>
            <p>
              Share infrastructure knowledge without exposing credentials. Read-only, public-facing pages — no login required.
            </p>
            <img src="@/assets/documentation-mode.webp" alt="Documentation mode" class="feature-card__img" />
          </div>

        </div>
      </div>
    </section>

    <!-- Architecture Section -->
    <section class="architecture-section section">
      <div class="container container--narrow">
        <h2 class="section__title">
          Built for how real teams work.
        </h2>
        <div class="architecture-grid">
          <div class="arch-item">
            <i class="fa-solid fa-check feature-check-icon"></i>
            <span>Self-hosted — your data stays on your disk</span>
          </div>
          <div class="arch-item">
            <i class="fa-solid fa-check feature-check-icon"></i>
            <span>Single Docker container — deploy in one command</span>
          </div>
          <div class="arch-item">
            <i class="fa-solid fa-check feature-check-icon"></i>
            <span>YAML or JSON export — portability without lock-in</span>
          </div>
          <div class="arch-item">
            <i class="fa-solid fa-check feature-check-icon"></i>
            <span>Full audit log — every change tracked</span>
          </div>
          <div class="arch-item">
            <i class="fa-solid fa-check feature-check-icon"></i>
            <span>Three security modes — open, read-only, or locked down</span>
          </div>
          <div class="arch-item">
            <i class="fa-solid fa-check feature-check-icon"></i>
            <span>Open source — MPL 2.0 licensed</span>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA Section -->
    <section class="cta-section section">
      <div class="container container--narrow text-center">
        <h2 class="cta-section__title">
          Ready to see everything at once?
        </h2>
        <p class="cta-section__subtitle">
          Get started in under a minute with a single Docker command.
        </p>
        <pre class="cta-section__code"><code>docker run -d \
  --name fuse-inventory \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  ghcr.io/lazerduck/fuse-inventory:latest</code></pre>
        <div class="hero__actions hero__actions--center">
          <a class="cta-btn cta-btn--solid" href="https://github.com/users/lazerduck/packages/container/package/fuse-inventory" target="_blank">
            <i class="fab fa-docker"></i> Get Started on Docker Hub
          </a>
          <a class="cta-btn cta-btn--outline" href="https://github.com/lazerduck/fuse-inventory" target="_blank">
            <i class="fab fa-github"></i> View on GitHub
          </a>
        </div>
      </div>
    </section>

  </div>
</template>

<script setup lang="ts">
function scrollTo(id: string) {
  document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' })
}
</script>

<style scoped>
/* Hero */
.hero {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4rem;
  padding: 5rem 0;
  align-items: center;
  min-height: 80vh;
}

.hero__content {
  padding-right: 2rem;
}

.hero__badge {
  font-size: 0.9rem;
  margin-bottom: 1.5rem;
}

.page-badge {
  display: inline-block;
  font-size: 0.85rem;
  font-weight: 600;
  color: #1976d2;
  border: 1px solid #1976d2;
  border-radius: 4px;
  padding: 0.2em 0.65em;
  letter-spacing: 0.03em;
  text-transform: uppercase;
}

.hero__title {
  font-size: 3.5rem;
  font-weight: 800;
  line-height: 1.1;
  margin: 0 0 1.5rem 0;
  letter-spacing: -0.02em;
  color: #1a1a2e;
}

.hero__subtitle {
  font-size: 1.25rem;
  line-height: 1.7;
  color: #4a4a6a;
  margin-bottom: 2rem;
  max-width: 540px;
}

.hero__actions {
  display: flex;
  gap: 1rem;
  align-items: center;
  flex-wrap: wrap;
}

.hero__actions--center {
  justify-content: center;
}

.cta-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.875rem 2rem;
  font-size: 1.05rem;
  font-weight: 600;
  border-radius: 10px;
  text-decoration: none;
  cursor: pointer;
  transition: all 0.3s ease;
  border: 2px solid transparent;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.15);
}

.cta-btn:hover {
  transform: translateY(-3px);
  box-shadow: 0 12px 24px rgba(0, 0, 0, 0.25);
}

.cta-btn--solid {
  background: linear-gradient(135deg, #1976d2 0%, #1565c0 100%);
  color: white;
}

.cta-btn--solid:hover {
  background: linear-gradient(135deg, #1565c0 0%, #1454aa 100%);
}

.cta-btn--outline {
  background: white;
  color: #1976d2;
  border: 2px solid white;
  font-weight: 700;
  box-shadow: none;
}

.cta-btn--outline:hover {
  background: rgba(255, 255, 255, 0.9);
  color: #1976d2;
  box-shadow: 0 12px 24px rgba(255, 255, 255, 0.3);
}

.hero__hero-image {
  padding-right: 2rem;
}

.hero__image {
  width: 100%;
  border-radius: 12px;
  box-shadow: 0 25px 60px rgba(0,0,0,0.15);
  border: 1px solid rgba(0,0,0,0.08);
}

/* Sections */
.section {
  padding: 5rem 0;
}

.section--alt {
  background: #f8f9fc;
}

.section__title {
  font-size: 2.5rem;
  font-weight: 700;
  text-align: center;
  margin: 0 0 1rem 0;
  color: #1a1a2e;
  line-height: 1.2;
}

.section__description {
  font-size: 1.15rem;
  text-align: center;
  color: #4a4a6a;
  max-width: 650px;
  margin: 0 auto 3.5rem;
  line-height: 1.6;
}

/* Problem */
.problem-section {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
}

.problem-section .section__title {
  color: white;
}

.problem-section .section__description {
  color: rgba(255,255,255,0.75);
  font-size: 1.2rem;
}

/* Feature Cards */
.features-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 2rem;
}

.feature-card {
  background: white;
  border-radius: 16px;
  padding: 2rem;
  border: 1px solid rgba(0,0,0,0.06);
  transition: transform 0.2s, box-shadow 0.2s;
}

.feature-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 32px rgba(0,0,0,0.1);
}

.feature-card__icon {
  width: 64px;
  height: 64px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 1.25rem;
}

.feature-card__title {
  font-size: 1.2rem;
  font-weight: 700;
  margin: 0 0 0.75rem 0;
  color: #1a1a2e;
}

.feature-card__img {
  width: 100%;
  margin-top: 1.25rem;
  border-radius: 8px;
  border: 1px solid rgba(0,0,0,0.06);
}

/* Architecture */
.architecture-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
  max-width: 700px;
  margin: 0 auto;
}

.arch-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 1.05rem;
  color: #3a3a5a;
}

.feature-check-icon {
  color: #00c853;
}

/* CTA */
.cta-section {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
}

.cta-section__title {
  font-size: 2.5rem;
  font-weight: 700;
  text-align: center;
  margin: 0 0 1rem 0;
  color: white;
}

.cta-section__subtitle {
  font-size: 1.15rem;
  text-align: center;
  color: rgba(255,255,255,0.7);
  margin: 0 0 2.5rem;
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

/* Container */
.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 2rem;
}

.container--narrow {
  max-width: 800px;
}

.text-center {
  text-align: center;
}

/* Responsive */
@media (max-width: 959px) {
  .hero {
    grid-template-columns: 1fr;
    padding: 5rem 0 3rem 0;
    min-height: auto;
  }
  .hero__content {
    padding-right: 0;
  }
  .hero__title {
    font-size: 2.5rem;
  }
  .features-grid {
    grid-template-columns: 1fr 1fr;
  }
  .architecture-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 599px) {
  .hero {
    padding: 6rem 0 3rem 0;
  }
  .hero__title {
    font-size: 2rem;
  }
  .section__title,
  .cta-section__title {
    font-size: 1.75rem;
  }
  .features-grid {
    grid-template-columns: 1fr;
  }
  .hero__actions {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>