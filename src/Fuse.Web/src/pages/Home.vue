<template>
  <q-page class="q-pa-md">
    <div class="text-h3 q-mb-md">Welcome to Fuse Inventory</div>
    <p class="text-h6">Dashboard</p>
    
    <div class="row q-col-gutter-md q-mt-md">
      <div class="col-12 col-md-4">
        <q-card>
          <q-card-section>
            <div class="text-h6">Total Services</div>
            <div class="text-h4 text-primary">0</div>
          </q-card-section>
        </q-card>
      </div>
      
      <div class="col-12 col-md-4">
        <q-card>
          <q-card-section>
            <div class="text-h6">Active</div>
            <div class="text-h4 text-positive">0</div>
          </q-card-section>
        </q-card>
      </div>
      
      <div class="col-12 col-md-4">
        <q-card>
          <q-card-section>
            <div class="text-h6">Inactive</div>
            <div class="text-h4 text-negative">0</div>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">

</script>