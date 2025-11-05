<template>
  <div class="page-container">
    <h1>Welcome to Fuse Inventory</h1>
    <p>Manage your applications, servers, and environments all in one place.</p>
    
    <div class="cards">
      <q-card class="info-card">
        <q-card-section>
          <div class="text-h6">Applications</div>
          <div class="text-subtitle2">Track and manage your applications</div>
        </q-card-section>
      </q-card>
      
      <q-card class="info-card">
        <q-card-section>
          <div class="text-h6">Servers</div>
          <div class="text-subtitle2">Monitor your server infrastructure</div>
        </q-card-section>
      </q-card>
      
      <q-card class="info-card">
        <q-card-section>
          <div class="text-h6">Environments</div>
          <div class="text-subtitle2">Manage deployment environments</div>
        </q-card-section>
      </q-card>
    </div>
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
.page-container {
  padding: 2rem;
}

.cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1rem;
  margin-top: 2rem;
}

.info-card {
  cursor: pointer;
  transition: transform 0.2s;
}

.info-card:hover {
  transform: translateY(-4px);
}
</style>
