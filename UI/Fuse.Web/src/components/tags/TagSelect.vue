<template>
  <q-select
    v-model="model"
    :label="label"
    dense
    outlined
    use-chips
    multiple
    use-input
    input-debounce="0"
    emit-value
    map-options
    :options="filteredOptions"
    :loading="isLoading"
    @filter="onFilter"
    @new-value="onNewValue"
  >
    <template #option="scope">
      <q-item v-bind="scope.itemProps">
        <q-item-section side>
          <div class="tag-color-indicator" :style="{ backgroundColor: getTagCssColor(scope.opt.color) }" />
        </q-item-section>
        <q-item-section>
          <q-item-label>{{ scope.opt.label }}</q-item-label>
        </q-item-section>
      </q-item>
    </template>
    <template #selected-item="scope">
      <q-chip
        removable
        dense
        :tabindex="scope.tabindex"
        class="q-ma-xs"
        @remove="scope.removeAtIndex(scope.index)"
      >
        <div class="tag-color-indicator q-mr-xs" :style="{ backgroundColor: getSelectedTagColor(scope.opt.value) }" />
        {{ scope.opt.label }}
      </q-chip>
    </template>
    <template #no-option>
      <q-item>
        <q-item-section class="text-grey">
          Type to search or create a new tag
        </q-item-section>
      </q-item>
    </template>
  </q-select>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { useTags } from '../../composables/useTags'
import { useFuseClient } from '../../composables/useFuseClient'
import { CreateTag, TagColor, type Tag } from '../../api/client'
import { getErrorMessage } from '../../utils/error'

interface TagOption {
  label: string
  value: string
  color: TagColor | undefined
}

interface Props {
  label?: string
}

withDefaults(defineProps<Props>(), {
  label: 'Tags'
})

const model = defineModel<string[]>({ default: () => [] })

const client = useFuseClient()
const queryClient = useQueryClient()
const tagsStore = useTags()

const filterText = ref('')

const tagOptions = computed<TagOption[]>(() =>
  (tagsStore.data.value ?? [])
    .filter((tag): tag is Tag & { id: string } => !!tag.id)
    .map((tag) => ({
      label: tag.name ?? tag.id,
      value: tag.id,
      color: tag.color
    }))
)

const filteredOptions = computed(() => {
  const needle = filterText.value.toLowerCase()
  if (!needle) {
    return tagOptions.value
  }
  return tagOptions.value.filter((opt) => opt.label.toLowerCase().includes(needle))
})

const isLoading = computed(() => tagsStore.isLoading.value || createMutation.isPending.value)

const availableColors = Object.values(TagColor)

function getUnusedColor(): TagColor {
  const usedColors = new Set((tagsStore.data.value ?? []).map((tag) => tag.color))
  const unused = availableColors.find((color) => !usedColors.has(color))
  if (unused) {
    return unused
  }
  const randomIndex = Math.floor(Math.random() * availableColors.length)
  return availableColors[randomIndex] ?? TagColor.Gray
}

const createMutation = useMutation({
  mutationFn: (payload: CreateTag) => client.tagPOST(payload),
  onSuccess: (newTag) => {
    queryClient.invalidateQueries({ queryKey: ['tags'] })
    if (newTag.id) {
      model.value = [...model.value, newTag.id]
    }
    Notify.create({ type: 'positive', message: `Tag "${newTag.name}" created` })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create tag') })
  }
})

function onFilter(val: string, update: (fn: () => void) => void) {
  update(() => {
    filterText.value = val
  })
}

function onNewValue(
  val: string,
  done: (item?: TagOption, mode?: 'add' | 'add-unique' | 'toggle') => void
) {
  const trimmed = val.trim()
  if (!trimmed) {
    done()
    return
  }

  const existingTag = tagOptions.value.find(
    (opt) => opt.label.toLowerCase() === trimmed.toLowerCase()
  )
  if (existingTag) {
    done(existingTag, 'add-unique')
    return
  }

  const payload = Object.assign(new CreateTag(), {
    name: trimmed,
    color: getUnusedColor()
  })
  createMutation.mutate(payload)
  done()
}

function getTagCssColor(color: TagColor | undefined): string {
  switch (color) {
    case TagColor.Red:
      return '#e53935'
    case TagColor.Green:
      return '#43a047'
    case TagColor.Blue:
      return '#1e88e5'
    case TagColor.Yellow:
      return '#fdd835'
    case TagColor.Purple:
      return '#8e24aa'
    case TagColor.Orange:
      return '#fb8c00'
    case TagColor.Teal:
      return '#00897b'
    case TagColor.Gray:
    default:
      return '#757575'
  }
}

function getSelectedTagColor(tagId: string): string {
  const tag = (tagsStore.data.value ?? []).find((t) => t.id === tagId)
  return getTagCssColor(tag?.color)
}
</script>

<style scoped>
.tag-color-indicator {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  flex-shrink: 0;
}
</style>
