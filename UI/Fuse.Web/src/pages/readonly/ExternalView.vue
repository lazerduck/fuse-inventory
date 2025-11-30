<template>
  <ReadOnlyShell
    :title="`External Resource: ${id}`"
    :higher="[]"
    :lower="[]"
  >
    <ReadOnlyPlaceholderContent icon="hub" :entity-id="id" />
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import ReadOnlyPlaceholderContent from '../../components/readonly/ReadOnlyPlaceholderContent.vue'

const route = useRoute()
const id = computed(() => route.params.id as string)
</script>
