<template>
  <q-select
    v-if="
      authMode === SecretProviderAuthMode.ClientSecret &&
      (canReadProfiles || profileId)
    "
    v-model="profileId"
    :options="availableProfiles"
    option-label="name"
    option-value="id"
    emit-value
    map-options
    clearable
    label="Credential profile"
    hint="Uses the shared Azure credential when empty"
  />
  <q-expansion-item
    v-if="canDiscover"
    dense
    icon="travel_explore"
    label="Find an Azure resource"
  >
    <div class="q-pa-sm">
      <q-input v-model="subscription" label="Subscription ID" dense />
      <q-input v-model="group" label="Resource group (optional)" dense />
      <q-btn
        flat
        label="Find resources"
        :loading="busy"
        :disable="!subscription.trim()"
        @click="find"
      />
      <q-list v-if="resources.length" dense bordered
        ><q-item
          v-for="r in resources"
          :key="r.resourceId"
          clickable
          @click="resourceId = r.resourceId"
        >
          <q-item-section
            ><q-item-label>{{ r.name }}</q-item-label
            ><q-item-label caption>{{
              r.resourceGroup
            }}</q-item-label></q-item-section
          >
        </q-item></q-list
      >
      <p v-if="notice" aria-live="polite">{{ notice }}</p>
    </div>
  </q-expansion-item>
</template>
<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useFuseStore } from "../../stores/FuseStore";
import {
  azureRequest,
  discoverAzureResources,
  type AzureProfile,
  type DiscoveredAzureResource,
} from "../../api/azureDiscovery";
import { SecretProviderAuthMode } from "../../api/client";
const props = defineProps<{
  kind: "CosmosDb" | "ServiceBus";
  authMode: SecretProviderAuthMode;
}>();
const resourceId = defineModel<string>("resourceId", { required: true });
const profileId = defineModel<string | null>("profileId", { required: true });
const store = useFuseStore();
const canReadProfiles = computed(() =>
  store.hasPermission("secretproviders:read"),
);
const canDiscover = computed(
  () => store.hasPermission("secretproviders:create") && !profileId.value,
);
const profiles = ref<AzureProfile[]>([]);
const availableProfiles = computed(() =>
  profiles.value.filter(
    (p) =>
      p.resourceId.toLowerCase() ===
      resourceId.value.replace(/\/$/, "").toLowerCase(),
  ),
);
const subscription = ref(""),
  group = ref(""),
  busy = ref(false),
  notice = ref("");
const resources = ref<DiscoveredAzureResource[]>([]);
onMounted(async () => {
  if (canReadProfiles.value) {
    try {
      profiles.value = await azureRequest(
        "/api/azure-credential-profiles",
        undefined,
        "GET",
      );
    } catch {
      notice.value = "Credential profile names could not be loaded.";
    }
  }
});
watch(
  () => props.authMode,
  (mode) => {
    if (mode !== SecretProviderAuthMode.ClientSecret) profileId.value = null;
  },
);
watch(resourceId, (next, old) => {
  if (next.toLowerCase() !== old.toLowerCase()) profileId.value = null;
});
async function find() {
  busy.value = true;
  notice.value = "";
  resources.value = [];
  try {
    const response = await discoverAzureResources({
      authMode: props.authMode,
      kinds: [props.kind],
      subscriptionIds: [subscription.value.trim()],
      resourceGroups: group.value.trim() ? [group.value.trim()] : [],
    });
    resources.value = response.resources.filter((r) => !r.ignored);
    notice.value =
      response.warnings.join(" ") ||
      (resources.value.length ? "" : "No visible resources in this scope.");
  } catch (e) {
    notice.value =
      (e as { error?: string })?.error || "Azure resource discovery failed.";
  } finally {
    busy.value = false;
  }
}
</script>
