<template>
  <div class="bg-white">
    <!-- Hero -->
    <section class="features-hero section section--alt">
      <div class="container container--narrow">
        <span class="page-badge">Features</span>
        <h1 class="features-hero__title">
          Built for teams who need visibility, not overhead.
        </h1>
        <p class="features-hero__subtitle">
          Fuse-Inventory is a full-featured CMDB that fits in a single Docker container. Here's what you get.
        </p>
      </div>
    </section>

    <!-- Feature sections -->
    <div class="container container--feature">

      <!-- Inventory Management -->
      <section class="feature-split">
        <div class="feature-split__text">
          <i class="fa-solid fa-grip feature-split__icon" style="font-size: 36px; color: #1976d2;"></i>
          <h2>Complete Inventory Model</h2>
          <p>
            Track everything from high-level applications down to individual credentials. Fuse-Inventory models your infrastructure the way real teams think about it.
          </p>
          <ul class="feature-list">
            <li><strong>Applications</strong> — name, version, owner, framework, repository, custom icons</li>
            <li><strong>Instances</strong> — deployed versions, URLs, health checks, OpenAPI endpoints</li>
            <li><strong>Environments</strong> — dev, test, production with URI templates for auto-generated URLs</li>
            <li><strong>Platforms</strong> — servers, clusters, serverless, container hosts</li>
            <li><strong>Tags</strong> — colored labels for filtering across everything</li>
          </ul>
        </div>
        <div class="feature-split__image">
          <img src="@/assets/home-dashboard.png" alt="Inventory dashboard" />
        </div>
      </section>

      <!-- Dependencies & Graph -->
      <section class="feature-split feature-split--reverse">
        <div class="feature-split__image">
          <img src="@/assets/dependency-graph.png" alt="Dependency graph" />
        </div>
        <div class="feature-split__text">
          <i class="fa-solid fa-circle-nodes feature-split__icon" style="font-size: 36px; color: #1976d2;"></i>
          <h2>Dependency Mapping &amp; Graph</h2>
          <p>
            See every connection in your stack. Fuse-Inventory tracks dependencies between instances, data stores, external services, and message brokers — then renders them as an interactive, filterable graph.
          </p>
          <ul class="feature-list">
            <li>Force-directed layout with Cytoscape.js</li>
            <li>Click nodes to highlight all relationships</li>
            <li>Focus modes: direct, full chain, critical path</li>
            <li>Environment containers to compare dev vs production</li>
            <li>Risk overlay for visual impact assessment</li>
          </ul>
        </div>
      </section>

      <!-- Blast Radius -->
      <section class="feature-split">
        <div class="feature-split__text">
          <i class="fa-solid fa-bolt feature-split__icon" style="font-size: 36px; color: #f59e0b;"></i>
          <h2>Blast Radius Analysis</h2>
          <p>
            What breaks if this service dies? Fuse-Inventory simulates failures across your dependency graph to show you exactly which systems are affected — directly and indirectly.
          </p>
          <ul class="feature-list">
            <li>Simulate failure of any resource type</li>
            <li>See direct and indirect impact at a glance</li>
            <li>Jump to affected systems with one click</li>
            <li>Plan redundancy and mitigation proactively</li>
          </ul>
        </div>
        <div class="feature-split__image">
          <img src="@/assets/blast-radius.webp" alt="Blast radius analysis" />
        </div>
      </section>

      <!-- Credentials & Accounts -->
      <section class="feature-split feature-split--reverse">
        <div class="feature-split__image">
          <img src="@/assets/account-edit.webp" alt="Account management" />
        </div>
        <div class="feature-split__text">
          <i class="fa-solid fa-key feature-split__icon" style="font-size: 36px; color: #1976d2;"></i>
          <h2>Credential &amp; Access Management</h2>
          <p>
            Track every credential your systems need — API keys, passwords, certificates, connection strings. Link them to specific targets with database grants and role-based access.
          </p>
          <ul class="feature-list">
            <li>8 authentication kinds: API Key, Password, Bearer Token, Basic Auth, Connection String, Client Credentials, Certificate, Custom</li>
            <li>Database-level grants with schema and privilege detail</li>
            <li>Secret bindings via Azure Key Vault or plain text references</li>
            <li>Identities: managed identities, service accounts, IAM roles</li>
            <li>Clone accounts and identities across targets</li>
          </ul>
        </div>
      </section>

      <!-- Integrations -->
      <section class="feature-split">
        <div class="feature-split__text">
          <i class="fa-solid fa-link feature-split__icon" style="font-size: 36px; color: #1976d2;"></i>
          <h2>Deep Integrations</h2>
          <p>
            Fuse-Inventory doesn't just store your data — it connects to the systems you already use.
          </p>
          <ul class="feature-list">
            <li><strong>Azure Key Vault</strong> — browse, create, rotate secrets. Reference vault entries from account configs.</li>
            <li><strong>Azure App Configuration</strong> — browse key-values, identify Key Vault references, associate configs with instances.</li>
            <li><strong>Uptime Kuma</strong> — pull real-time health data and display it on the dashboard.</li>
            <li><strong>SQL Permission Management</strong> — detect drift between expected and actual database permissions. Create missing accounts and bulk-resolve in one click.</li>
          </ul>
        </div>
        <div class="feature-split__image">
          <img src="@/assets/azure-integration.webp" alt="Azure integrations" />
        </div>
      </section>

      <!-- SQL Drift Detection -->
      <section class="feature-split feature-split--reverse">
        <div class="feature-split__image">
          <img src="@/assets/SQL_integration.png" alt="SQL drift detection dashboard" />
        </div>
        <div class="feature-split__text">
          <i class="fa-solid fa-shield-halved feature-split__icon" style="font-size: 36px; color: #1976d2;"></i>
          <h2>SQL Permission Drift Detection</h2>
          <p>
            Connect Fuse-Inventory to your SQL instances and continuously compare what your inventory says against what the database actually has. Instantly spot missing accounts, extra permissions, and grants that have drifted from your defined model.
          </p>
          <ul class="feature-list">
            <li>Compares inventory account permissions against live database ACLs</li>
            <li>Detects missing principals, extra grants, and permission mismatches</li>
            <li>Import unmanaged SQL principals directly into your inventory in one click</li>
            <li>Push permissions from Fuse to the database — grant what you've defined without leaving the UI</li>
            <li>Flags accounts with no SQL principal configured so nothing slips through</li>
          </ul>
        </div>
      </section>

      <!-- Risk & Governance -->
      <section class="feature-split feature-split--reverse">
        <div class="feature-split__image">
          <img src="@/assets/risks.webp" alt="Risk tracking" />
        </div>
        <div class="feature-split__text">
          <i class="fa-solid fa-triangle-exclamation feature-split__icon" style="font-size: 36px; color: #dc2626;"></i>
          <h2>Risk &amp; Governance</h2>
          <p>
            Track risks, responsibilities, and compliance gaps. Link them to specific resources so nothing falls through the cracks.
          </p>
          <ul class="feature-list">
            <li>Structured risk records: impact, likelihood, status lifecycle</li>
            <li>Assign owners and approvers by position</li>
            <li>Documentation completeness tracking — find what's missing</li>
            <li>Full audit trail with 100+ distinct action types</li>
          </ul>
        </div>
      </section>

      <!-- Documentation Mode -->
      <section class="feature-split">
        <div class="feature-split__text">
          <i class="fa-solid fa-book-open feature-split__icon" style="font-size: 36px; color: #1976d2;"></i>
          <h2>Documentation Mode</h2>
          <p>
            Share infrastructure knowledge without exposing credentials. Documentation Mode provides read-only, public-facing pages with global search — no login required.
          </p>
          <ul class="feature-list">
            <li>Public shareable URLs for every entity type</li>
            <li>Global search across all entity types</li>
            <li>Safe display that excludes sensitive values</li>
            <li>Covers: applications, instances, data stores, accounts, identities, external resources, message brokers, platforms, dependencies</li>
          </ul>
        </div>
        <div class="feature-split__image">
          <img src="@/assets/documentation-mode.webp" alt="Documentation mode" />
        </div>
      </section>

    </div>

    <!-- CTA -->
    <section class="cta-section section">
      <div class="container container--narrow text-center">
        <h2>Still curious? See it in action.</h2>
        <p class="section__description">
          Browse through real screenshots or fire up a container and try it yourself.
        </p>
        <div class="hero__actions" style="justify-content: center;">
          <a class="cta-btn cta-btn--solid" href="/screenshots">View Screenshots</a>
          <a class="cta-btn cta-btn--outline" href="https://github.com/users/lazerduck/packages/container/package/fuse-inventory" target="_blank">
            <i class="fab fa-docker"></i> Get Started
          </a>
        </div>
      </div>
    </section>

  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
.features-hero {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
  text-align: center;
  padding: 6rem 0;
}

.features-hero__badge {
  font-size: 0.9rem;
  margin-bottom: 1.5rem;
}

.features-hero__title {
  font-size: 2.75rem;
  font-weight: 700;
  margin: 0 0 1rem 0;
  line-height: 1.2;
  color: white;
}

.features-hero__subtitle {
  font-size: 1.15rem;
  color: rgba(255,255,255,0.7);
  max-width: 650px;
  margin: 0 auto;
  line-height: 1.6;
}

.container--feature {
  max-width: 1280px;
}

/* CTA buttons */
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
.feature-split {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4rem;
  align-items: center;
  padding: 2.5rem;
  margin-bottom: 2rem;
  background: white;
  border: 1px solid rgba(26, 26, 46, 0.08);
  border-radius: 24px;
  box-shadow: 0 20px 60px rgba(26, 26, 46, 0.08);
}

.feature-split:not(:first-child) {
  border-top: 1px solid rgba(0,0,0,0.06);
}

.feature-split--reverse {
  direction: rtl;
}

.feature-split--reverse > * {
  direction: ltr;
}

.feature-split__icon {
  margin-bottom: 1rem;
}

.feature-split__text h2 {
  font-size: 1.75rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 1rem 0;
}

.feature-split__text p {
  font-size: 1.05rem;
  color: #4a4a6a;
  line-height: 1.6;
  margin-bottom: 1.25rem;
}

.feature-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.feature-list li {
  padding: 0.4rem 0;
  padding-left: 1.5rem;
  position: relative;
  font-size: 1rem;
  color: #3a3a5a;
  line-height: 1.5;
}

.feature-list li::before {
  content: '✓';
  position: absolute;
  left: 0;
  color: #00c853;
  font-weight: bold;
}

.feature-split__image img {
  width: 100%;
  border-radius: 16px;
  box-shadow: 0 12px 32px rgba(0,0,0,0.08);
  border: 1px solid rgba(0,0,0,0.06);
}

@media (max-width: 959px) {
  .container--feature {
    max-width: 960px;
  }

  .feature-split {
    grid-template-columns: 1fr;
    gap: 2rem;
    padding: 2rem;
  }
}

@media (max-width: 599px) {
  .container--feature {
    max-width: 100%;
  }

  .feature-split {
    padding: 1.5rem;
  }
}

/* CTA */
</style>