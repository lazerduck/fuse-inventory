<template>
  <q-chip
    dense
    outline
    :color="chipColor"
    :text-color="chipColor"
    :size="size"
  >
    {{ label }}
  </q-chip>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { TagColor } from '../../api/client'

interface Props {
  label: string
  color?: TagColor
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
}

const props = withDefaults(defineProps<Props>(), {
  color: undefined,
  size: 'sm'
})

const chipColor = computed(() => {
  switch (props.color) {
    case TagColor.Red:
      return 'red-6'
    case TagColor.Green:
      return 'green-6'
    case TagColor.Blue:
      return 'blue-6'
    case TagColor.Yellow:
      return 'yellow-7'
    case TagColor.Purple:
      return 'purple-5'
    case TagColor.Orange:
      return 'orange-6'
    case TagColor.Teal:
      return 'teal-5'
    case TagColor.Gray:
    default:
      return 'grey-7'
  }
})
</script>
