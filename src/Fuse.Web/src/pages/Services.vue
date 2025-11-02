<template>
  <q-page class="q-pa-md">
    <div class="text-h4 q-mb-md">Services</div>
    <div class="q-pa-md">
      <q-input
        filled
        v-model="filter"
        label="Search services"
        class="q-mb-md"
        debounce="300"
      />

      <q-table
        :rows="filteredServices"
        :columns="columns"
        row-key="id"
        flat
        bordered
      >
      <template v-slot:body-cell-actions="props">
        <q-td align="right">
          <q-btn
            dense
            flat
            icon="visibility"
            @click="viewService(props.row)"
            color="primary"
          />
        </q-td>
      </template>
      </q-table>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useServicesStore } from '../stores/ServicesStore';
const servicesStore = useServicesStore();

const filter = ref('');

const columns = [
  { name: 'name', label: 'Name', field: 'name', sortable: true },
  { name: 'type', label: 'Type', field: 'type', sortable: true },
  { name: 'author', label: 'Author', field: 'author', sortable: true },
  { name: 'updatedAt', label: 'Updated At', field: 'updatedAt', sortable: true },
  { name: 'actions', label: 'Actions', field: 'actions' }
]

const filteredServices = computed(() => {
  if (!filter.value) {
    return servicesStore.services;
  }
  return servicesStore.services.filter(service =>
    service.name.toLowerCase().includes(filter.value.toLowerCase()) ||
    service.type.toLowerCase().includes(filter.value.toLowerCase()) ||
    service.author?.toLowerCase().includes(filter.value.toLowerCase())
  );
});

function viewService(service: number) {
  // custom action, e.g. navigate to detail view
  console.log('Viewing service', service)
}

onMounted(async () => {
  await servicesStore.loadAll();
});

</script>
