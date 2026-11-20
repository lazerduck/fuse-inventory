<template>
  <q-card-section>
    <h3 class="text-h6 q-my-sm">Entra workload identity</h3>
    <div class="row items-center q-gutter-sm">
      <q-select
        v-model="profile"
        outlined
        dense
        label="Directory lookup profile"
        emit-value
        map-options
        option-label="name"
        option-value="id"
        :options="profiles"
        class="col-grow"
      />
      <q-btn
        outline
        color="primary"
        label="Inspect credentials"
        :disable="!profile"
        :loading="busy"
        @click="inspect"
      />
    </div>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="result" class="q-mt-sm">
      <p>
        {{ result.name || "Service principal" }} ·
        {{ result.principalVisibility }} · Application registration
        {{ result.applicationVisibility }}
      </p>
      <q-list bordered separator v-if="result.credentials.length">
        <q-item v-for="c in result.credentials" :key="c.source + c.keyId">
          <q-item-section
            >{{ c.source }} · {{ c.kind }} · {{ c.keyId }}</q-item-section
          >
          <q-item-section side>{{
            c.expiresAt
              ? "Expires " + new Date(c.expiresAt).toLocaleDateString()
              : "Expiry unknown"
          }}</q-item-section>
        </q-item>
      </q-list>
      <p v-else>No credential expirations observed.</p>
      <div v-if="result.applications?.length" class="q-mt-sm">
        <div class="text-subtitle2">Applications using this identity</div>
        <q-list dense
          ><q-item
            v-for="a in result.applications"
            :key="a.instanceId"
            clickable
            :to="{
              name: 'instanceEdit',
              params: {
                applicationId: a.applicationId,
                instanceId: a.instanceId,
              },
            }"
          >
            <q-item-section>{{ a.name }}</q-item-section></q-item
          ></q-list
        >
      </div>
    </div>
  </q-card-section>
</template>
<script setup lang="ts">
import { ref, watch } from "vue";
import { azureRequest, type AzureProfile } from "../../api/azureDiscovery";
const props = defineProps<{ identity: string; tenant: string }>();
type Observation = {
  name?: string;
  principalVisibility: string;
  applicationVisibility: string;
  applications?: { applicationId: string; name: string; instanceId: string }[];
  credentials: {
    source: string;
    kind: string;
    keyId: string;
    expiresAt?: string;
  }[];
};
const profiles = ref<AzureProfile[]>([]),
  profile = ref<string | null>(null),
  result = ref<Observation | null>(null);
const busy = ref(false),
  error = ref("");
watch(
  () => [props.identity, props.tenant],
  async (_, __, cleanup) => {
    const ctl = new AbortController();
    cleanup(() => ctl.abort());
    profile.value = null;
    profiles.value = [];
    error.value = "";
    try {
      profiles.value = (
        await azureRequest<AzureProfile[]>(
          "/api/azure-credential-profiles",
          undefined,
          "GET",
          ctl.signal,
        )
      ).filter(
        (p) =>
          p.directoryLookup &&
          p.tenantId.toLowerCase() === props.tenant.toLowerCase(),
      );
      if (profiles.value.length === 1) profile.value = profiles.value[0]!.id;
    } catch {
      if (!ctl.signal.aborted)
        error.value = "Credential profiles could not be loaded.";
    }
  },
  { immediate: true },
);
let sequence = 0;
watch([profile, () => props.identity], () => {
  ++sequence;
  result.value = null;
  busy.value = false;
});
async function inspect() {
  const current = ++sequence;
  busy.value = true;
  error.value = "";
  try {
    const observation = await azureRequest<Observation>(
      `/api/azure-workloads/${props.identity}?profileId=${profile.value}`,
      undefined,
      "GET",
    );
    if (current === sequence) result.value = observation;
  } catch {
    if (current === sequence)
      error.value = "The workload inspection could not be completed.";
  } finally {
    if (current === sequence) busy.value = false;
  }
}
</script>
