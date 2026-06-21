<template>
  <q-page class="bg-grey-2">
    <!-- Header -->
    <section class="screenshots-hero section section--alt">
      <div class="container container--narrow">
        <q-badge color="primary" outline class="screenshots-hero__badge">Screenshots</q-badge>
        <h1 class="screenshots-hero__title">See it in action</h1>
        <p class="screenshots-hero__subtitle">
          A look at the Fuse-Inventory interface — dark mode by default, responsive, and designed for information density.
        </p>
      </div>
    </section>

    <!-- Gallery -->
    <section class="gallery-section section">
      <div class="container">
        <div class="gallery-grid">

          <div class="gallery-item gallery-item--large">
            <div class="gallery-item__frame">
              <img src="@/assets/home-dashboard.webp" alt="Dashboard overview" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Dashboard</h3>
              <p>Explore your estate at a glance — applications, platforms, environments, and external resources in a clean, filterable grid.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/dependency-graph.webp" alt="Dependency graph" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Dependency Graph</h3>
              <p>Interactive visualization of relationships between entities across environments.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/blast-radius.webp" alt="Blast radius analysis" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Blast Radius</h3>
              <p>Simulate failures to see exactly what downstream systems are affected.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/azure-integration.webp" alt="Azure Key Vault integration" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Azure Integration</h3>
              <p>Key Vault and App Configuration management from within Fuse.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/documentation-mode.webp" alt="Documentation mode" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Documentation Mode</h3>
              <p>Read-only, public-facing view with global search — no login required.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/account-edit.webp" alt="Account credentials management" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Accounts &amp; Credentials</h3>
              <p>Track credentials with database grants, secret bindings, and clone support.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/audit.webp" alt="Audit log" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Audit Log</h3>
              <p>Every change tracked with 100+ distinct action types and powerful filtering.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/risks.webp" alt="Risk management" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Risk Management</h3>
              <p>Structured risk records with impact, likelihood, status lifecycle, and ownership.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/documentation-mode.webp" alt="Documentation completeness" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Documentation Completeness</h3>
              <p>Find gaps in your infrastructure documentation at a glance.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/configuration.webp" alt="App configuration" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Configuration</h3>
              <p>Browse and manage Azure App Configuration key-values from within Fuse.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/security.webp" alt="Security settings" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Security</h3>
              <p>Three security modes, role-based access, API keys, and user management.</p>
            </div>
          </div>

          <div class="gallery-item">
            <div class="gallery-item__frame">
              <img src="@/assets/password-generator.webp" alt="Password generator" />
              <div class="gallery-item__overlay">
                <q-icon name="zoom_in" size="24px" color="white" />
              </div>
            </div>
            <div class="gallery-item__info">
              <h3>Password Generator</h3>
              <p>Built-in secure password generation with configurable character set and length.</p>
            </div>
          </div>

        </div>
      </div>
    </section>

    <!-- CTA -->
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
        <div class="hero__actions" style="justify-content: center;">
          <q-btn
            unelevated
            size="lg"
            color="white"
            text-color="primary"
            label="Get Started"
            icon="fab fa-docker"
            href="https://github.com/users/lazerduck/packages/container/package/fuse-inventory"
            target="_blank"
          />
          <q-btn
            outline
            size="lg"
            color="white"
            label="View on GitHub"
            icon="fab fa-github"
            href="https://github.com/lazerduck/fuse-inventory"
            target="_blank"
          />
        </div>
      </div>
    </section>

  </q-page>
</template>

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

/* Gallery */
.gallery-section {
  padding: 4rem 0;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 2rem;
}

.gallery-item--large {
  grid-column: span 2;
}

.gallery-item__frame {
  position: relative;
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid rgba(0,0,0,0.08);
  box-shadow: 0 4px 16px rgba(0,0,0,0.06);
  transition: transform 0.2s, box-shadow 0.2s;
  cursor: pointer;
}

.gallery-item__frame:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 32px rgba(0,0,0,0.12);
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

.gallery-item__frame:hover .gallery-item__overlay {
  opacity: 1;
}

.gallery-item__info {
  padding: 1rem 0.25rem;
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

/* CTA */
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

/* Responsive */
@media (max-width: 959px) {
  .gallery-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  .gallery-item--large {
    grid-column: span 2;
  }
}

@media (max-width: 599px) {
  .gallery-grid {
    grid-template-columns: 1fr;
  }
  .gallery-item--large {
    grid-column: span 1;
  }
  .screenshots-hero__title {
    font-size: 2rem;
  }
}
</style>