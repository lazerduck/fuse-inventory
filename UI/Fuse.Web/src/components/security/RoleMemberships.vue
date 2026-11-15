<template>
  <div>
    <q-chip
      clickable
      @click="membership.roleId && $emit('select', membership.roleId)"
      v-for="membership in memberships"
      :key="membership.roleId"
      dense
      size="sm"
      :color="membership.direct ? 'primary' : 'teal'"
      text-color="white"
    >
      {{ membership.roleName }} · {{ membershipSource(membership) }}
      <q-tooltip>
        <div v-if="membership.direct">Direct assignment — remove in Fuse.</div>
        <div v-for="group in membership.entraGroups" :key="group.groupId">
          Via {{ group.displayName }} — manage membership in Entra, or unlink the group from this role for everyone.
        </div>
      </q-tooltip>
    </q-chip>
    <span v-if="!memberships?.length" class="text-grey">No roles assigned</span>
  </div>
</template>
<script setup lang="ts">
import type { IRoleMembership } from 'api/client'

interface Props {
  memberships?: IRoleMembership[]
}

defineProps<Props>()
defineEmits<{ select: [roleId: string] }>()

function membershipSource(membership: IRoleMembership) {
  if (membership.direct && membership.entraGroups?.length) return 'Direct + Via Entra'
  return membership.direct ? 'Direct' : 'Via Entra'
}
</script>
