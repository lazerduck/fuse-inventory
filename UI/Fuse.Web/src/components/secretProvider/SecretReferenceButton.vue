<template>
  <q-btn
    flat
    dense
    round
    icon="event_note"
    color="primary"
    aria-label="Inspect secret version expiry and applications"
    @click="inspect"
  >
    <q-tooltip>Version expiry and applications</q-tooltip>
  </q-btn>
  <q-dialog v-model="open">
    <q-card style="width: 720px; max-width: 95vw">
      <q-card-section class="row items-center"
        ><h3 class="text-h6 q-ma-none">
          Secret version expiry and applications
        </h3>
        <q-space /><q-btn
          flat
          round
          dense
          icon="close"
          aria-label="Close"
          v-close-popup
      /></q-card-section>
      <q-card-section v-if="busy"><q-spinner color="primary" /></q-card-section>
      <q-card-section v-else-if="error" role="alert">{{
        error
      }}</q-card-section>
      <q-card-section v-else-if="data">
        <p>
          {{ data.name }} · {{ data.metadata.visibility }}
          <span v-if="fromConfig || version"
            >·
            {{
              data.pinnedVersion || version || "Version follows latest"
            }}</span
          >
        </p>
        <q-list bordered separator>
          <q-item v-for="v in data.metadata.versions" :key="v.version">
            <q-item-section
              ><q-item-label
                >{{ v.version }}
                <q-badge
                  v-if="v.version === (data.pinnedVersion || version)"
                  outline
                  color="primary"
                  label="Pinned" /></q-item-label
              ><q-item-label caption>{{
                v.enabled === false
                  ? "Disabled"
                  : v.enabled === true
                    ? "Enabled"
                    : "Enabled state unknown"
              }}</q-item-label></q-item-section
            >
            <q-item-section side>{{
              v.expires
                ? (Date.parse(v.expires) < Date.now()
                    ? "Expired "
                    : "Expires ") + new Date(v.expires).toLocaleDateString()
                : "No expiry recorded"
            }}</q-item-section>
          </q-item>
        </q-list>
        <div class="text-subtitle1 q-mt-md">Documented application usage</div>
        <q-list v-if="data.consumers.length" separator>
          <q-item
            v-for="c in data.consumers"
            :key="c.instanceId + c.source"
            clickable
            :to="{
              name: 'instanceEdit',
              params: {
                instanceId: c.instanceId,
                applicationId: c.applicationId,
              },
            }"
          >
            <q-item-section
              ><q-item-label>{{ c.application }} · {{ c.source }}</q-item-label
              ><q-item-label caption>{{
                c.pinnedVersion || "Version follows latest"
              }}</q-item-label></q-item-section
            >
          </q-item>
        </q-list>
        <p v-else>No visible application references.</p>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>
<script setup lang="ts">
import { ref, watch } from "vue";
import { azureRequest } from "../../api/azureDiscovery";
const props = defineProps<{
  provider: string;
  name: string;
  label?: string;
  version?: string;
  fromConfig?: boolean;
}>();
type Health = {
  name: string;
  pinnedVersion?: string;
  metadata: {
    visibility: string;
    versions: { version: string; expires?: string; enabled?: boolean }[];
  };
  consumers: {
    applicationId: string;
    application: string;
    instanceId: string;
    source: string;
    pinnedVersion?: string;
  }[];
};
const open = ref(false),
  busy = ref(false),
  error = ref(""),
  data = ref<Health | null>(null);
let sequence = 0;
watch(
  () => [props.provider, props.name, props.label],
  () => {
    ++sequence;
    open.value = false;
    data.value = null;
  },
);
async function inspect() {
  const current = ++sequence;
  open.value = true;
  busy.value = true;
  error.value = "";
  data.value = null;
  const query = new URLSearchParams({
    [props.fromConfig ? "key" : "name"]: props.name,
  });
  if (props.label) query.set("label", props.label);
  try {
    const result = await azureRequest<Health>(
      `/api/azure-secret-references/${props.fromConfig ? "configuration" : "vault"}/${props.provider}?${query}`,
      undefined,
      "GET",
    );
    if (current === sequence) data.value = result;
  } catch {
    if (current === sequence)
      error.value =
        "Reference metadata is unavailable. Check the configured vault and its metadata access.";
  } finally {
    if (current === sequence) busy.value = false;
  }
}
</script>
