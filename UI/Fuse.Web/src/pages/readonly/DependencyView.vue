<template>
  <ReadonlyPlaceholder
    icon="link"
    entity-type="Dependency"
    :entity-id="id"
  />
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import ReadonlyPlaceholder from '../../components/readonly/ReadonlyPlaceholder.vue'

const route = useRoute()
const id = computed(() => route.params.id as string)
</script>
