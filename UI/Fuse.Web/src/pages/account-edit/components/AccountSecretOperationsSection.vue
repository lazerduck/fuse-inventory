<template>
  <q-card-section>
    <div class="text-h6 q-mb-md">Secret Operations</div>
    <div class="q-gutter-sm">
      <q-btn
        v-if="canRotate"
        flat
        icon="autorenew"
        label="Rotate Secret"
        color="primary"
        @click="$emit('rotate')"
      />
      <q-btn
        v-if="canReveal"
        flat
        icon="visibility"
        label="Reveal Secret"
        color="orange"
        @click="$emit('reveal')"
      />
    </div>
  </q-card-section>
</template>

<script setup lang="ts">
interface Props {
  canRotate: boolean
  canReveal: boolean
}

defineProps<Props>()

defineEmits<{
  (e: 'rotate'): void
  (e: 'reveal'): void
}>()
</script>
