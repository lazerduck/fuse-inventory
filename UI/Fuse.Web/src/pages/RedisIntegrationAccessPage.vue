<template>
  <div class="page-container">
    <q-btn flat icon="arrow_back" label="Redis Integrations" :to="{ name: 'redisIntegrations' }" class="q-mb-md" />
    <div class="page-header">
      <div><h1>Redis Access</h1><p class="subtitle">{{ integration.data.value?.name || 'Redis integration' }}<span v-if="integration.data.value?.endpoint"> · {{ integration.data.value.endpoint }}</span></p></div>
      <q-btn label="Refresh inspection" color="primary" icon="refresh" :loading="discovery.isFetching.value"
        :disable="!canRead || !integration.data.value || !!integration.error.value" @click="discovery.refetch()" />
    </div>
    <q-banner v-if="!canRead" dense class="bg-orange-1 text-orange-9">You do not have permission to inspect Redis integrations.</q-banner>
    <template v-else>
      <q-banner v-if="error" dense class="bg-red-1 text-negative q-mb-md">{{ error }}</q-banner>
      <q-linear-progress v-if="integration.isFetching.value || discovery.isFetching.value" indeterminate class="q-mb-md" />
      <RedisAccessResults v-if="discovery.data.value && !error" :discovery="discovery.data.value" />
      <p v-else-if="!error" class="text-grey-7">Reading Redis metadata and ACL rules…</p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { Permission } from 'permissions'
import { useFuseStore } from '../stores/FuseStore'
import { getRedisIntegration, discoverRedisAccess } from '../api/redisIntegrations'
import { getErrorMessage } from '../utils/error'
import RedisAccessResults from '../components/redisIntegration/RedisAccessResults.vue'

const route = useRoute()
const store = useFuseStore()
const id = computed(() => String(route.params.id ?? ''))
const canRead = computed(() => store.hasPermission(Permission.RedisIntegrationsRead))
const integration = useQuery({ queryKey: ['redisIntegration', id], queryFn: ({ signal }) => getRedisIntegration(id.value, signal),
  enabled: computed(() => canRead.value && !!id.value), retry: false, refetchOnWindowFocus: false })
const discovery = useQuery({ queryKey: ['redisDiscovery', id], queryFn: ({ signal }) => discoverRedisAccess(id.value, signal),
  enabled: computed(() => canRead.value && !!integration.data.value && !integration.error.value),
  retry: false, staleTime: 0, refetchOnWindowFocus: false })
const error = computed(() => {
  const value = integration.error.value || discovery.error.value
  return value ? getErrorMessage(value, 'Unable to inspect Redis access') : null
})
</script>
