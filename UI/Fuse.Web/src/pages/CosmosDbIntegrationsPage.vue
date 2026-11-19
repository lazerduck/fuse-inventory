<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Azure Cosmos DB Integrations</h1>
        <p class="subtitle">
          Inspect NoSQL databases, container partition keys and indexing
          policies.
        </p>
      </div>
      <q-btn
        v-if="allowed('create')"
        color="primary"
        label="Add integration"
        :disable="busy"
        @click="edit()"
      />
    </div>
    <q-banner v-if="error" class="error q-mb-md" role="alert">{{
      error
    }}</q-banner>
    <q-linear-progress v-if="busy" indeterminate aria-label="Loading" />
    <q-list bordered separator>
      <q-item v-for="item in items" :key="item.id" class="integration">
        <q-item-section
          ><q-item-label>{{ item.name }}</q-item-label
          ><q-item-label caption>{{
            item.accountResourceId
          }}</q-item-label></q-item-section
        >
        <q-item-section side
          ><div>
            <q-btn
              flat
              label="Inspect"
              :disable="busy"
              @click="inspect(item)"
            />
            <q-btn
              v-if="allowed('validate')"
              flat
              label="Test connection"
              :disable="busy"
              @click="probe(item)"
            />
            <q-btn
              v-if="allowed('update')"
              flat
              icon="edit"
              aria-label="Edit integration"
              :disable="busy"
              @click="edit(item)"
            />
            <q-btn
              v-if="allowed('delete')"
              flat
              icon="delete"
              aria-label="Delete integration"
              :disable="busy"
              @click="remove(item)"
            /></div
        ></q-item-section>
      </q-item>
      <q-item v-if="!items.length && !busy"
        ><q-item-section
          >No Azure Cosmos DB integrations configured.</q-item-section
        ></q-item
      >
    </q-list>
    <p v-if="connection" aria-live="polite" class="q-my-md">{{ connection }}</p>
    <section v-if="discovery" class="q-mt-lg">
      <h2>{{ selectedName }}</h2>
      <p>
        {{ discovery.accountResourceId }} ·
        {{ discovery.observedAt?.toLocaleString() }}
      </p>
      <p aria-live="polite">Visibility: {{ discovery.visibility }}</p>
      <q-form @submit="lookup" class="lookup">
        <q-input
          v-model="database"
          label="Database name"
          hint="Look up a known database even when account listing is restricted"
        />
        <q-input v-model="container" label="Container name (optional)" />
        <q-btn
          type="submit"
          flat
          label="Look up"
          :disable="busy || !database"
        />
        <q-btn
          flat
          label="Show all visible"
          :disable="busy"
          @click="inspect(selected!)"
        />
      </q-form>
      <p v-if="!discovery.databases?.length">
        No database or container metadata was visible for this request.
      </p>
      <section v-for="db in discovery.databases" :key="db.name">
        <h3>{{ db.name }}</h3>
        <p>Container visibility: {{ db.containerVisibility }}</p>
        <q-list bordered separator v-if="db.containers?.length">
          <q-expansion-item
            v-for="row in db.containers"
            :key="row.name"
            :label="row.name"
            :caption="
              row.partitionKey?.paths?.join(', ') || 'Partition key unavailable'
            "
          >
            <div class="q-pa-md">
              <p>
                Partition key: {{ row.partitionKey?.kind || "Unknown" }} ·
                {{ row.partitionKey?.paths?.join(", ") || "Unknown" }}
                <span v-if="row.partitionKey?.version">
                  · Version {{ row.partitionKey.version }}</span
                >
              </p>
              <template v-if="row.indexingPolicy">
                <p>
                  Indexing mode: {{ row.indexingPolicy.mode ?? "Unknown" }} ·
                  Automatic:
                  {{
                    row.indexingPolicy.automatic == null
                      ? "Unknown"
                      : row.indexingPolicy.automatic
                        ? "Yes"
                        : "No"
                  }}
                </p>
                <p>
                  Included paths:
                  {{
                    row.indexingPolicy.includedPaths?.join(", ") ||
                    "None reported"
                  }}
                </p>
                <p>
                  Excluded paths:
                  {{
                    row.indexingPolicy.excludedPaths?.join(", ") ||
                    "None reported"
                  }}
                </p>
                <details v-if="row.indexingPolicy.policyJson">
                  <summary>Full indexing policy</summary>
                  <pre>{{ formatPolicy(row.indexingPolicy.policyJson) }}</pre>
                </details>
              </template>
              <p v-else>Indexing policy unavailable.</p>
            </div>
          </q-expansion-item>
        </q-list>
      </section>
    </section>
    <q-dialog v-model="dialog" :persistent="saving"
      ><q-card class="editor"
        ><q-form @submit="save">
          <q-card-section>
            <h2>{{ form.id ? "Edit" : "Add" }} Azure Cosmos DB integration</h2>
            <q-input v-model="form.name" label="Name" :rules="[required]" />
            <q-select
              v-model="form.dataStoreId"
              :options="datastores"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              label="Azure Cosmos DB data store"
              :rules="[required]"
            >
              <template #no-option
                ><q-item
                  ><q-item-section
                    >Create a data store with kind Azure Cosmos DB in Data
                    Stores.</q-item-section
                  ></q-item
                ></template
              >
            </q-select>
            <q-input
              v-model="form.accountResourceId"
              label="Account resource ID"
              hint="Copy the full resource ID from the Azure Cosmos DB account Properties page."
              :rules="[required]"
            />
            <q-select
              v-model="form.authMode"
              :options="authMethods"
              label="Azure credentials"
              emit-value
              map-options
            />
            <q-banner v-if="formError" class="error q-mt-md" role="alert">{{
              formError
            }}</q-banner>
          </q-card-section>
          <q-card-actions align="right"
            ><q-btn v-close-popup flat label="Cancel" :disable="saving" /><q-btn
              type="submit"
              color="primary"
              label="Save"
              :loading="saving"
          /></q-card-actions> </q-form></q-card
    ></q-dialog>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from "vue";
import { useQuasar } from "quasar";
import {
  CreateCosmosDbIntegration,
  UpdateCosmosDbIntegration,
  type CosmosDbDiscoveryResponse,
  type CosmosDbConnection,
  type DataStore,
  SecretProviderAuthMode,
} from "../api/client";
import { useFuseClient } from "../composables/useFuseClient";
import { useFuseStore } from "../stores/FuseStore";

const client = useFuseClient(),
  store = useFuseStore(),
  q = useQuasar();
const controller = new AbortController(),
  signal = controller.signal;
onBeforeUnmount(() => controller.abort());
const allowed = (action: string) =>
  store.hasPermission("cosmosdbintegrations:" + action);
const items = ref<CosmosDbConnection[]>([]),
  discovery = ref<CosmosDbDiscoveryResponse>();
const busy = ref(false),
  saving = ref(false),
  dialog = ref(false);
const connection = ref(""),
  selectedName = ref(""),
  error = ref(""),
  formError = ref("");
const datastores = ref<DataStore[]>([]);
const authMethods = [
  {
    label: "Shared Azure client secret",
    value: SecretProviderAuthMode.ClientSecret,
  },
  { label: "Managed identity", value: SecretProviderAuthMode.ManagedIdentity },
];
const empty = () => ({
  id: "",
  name: "",
  dataStoreId: "",
  accountResourceId: "",
  authMode: SecretProviderAuthMode.ClientSecret,
});
const form = ref(empty());
const required = (value: string) => !!value?.trim() || "Required";
function message(e: unknown) {
  if (
    e &&
    typeof e === "object" &&
    "response" in e &&
    typeof e.response === "string"
  ) {
    try {
      const detail = JSON.parse(e.response);
      if (typeof detail.error === "string") return detail.error;
    } catch {
      /* Non-JSON error response */
    }
  }
  return e instanceof Error ? e.message : "Request failed";
}
async function load() {
  items.value = await client.cosmosDbIntegrationAll(signal);
}
async function run(action: () => Promise<void>) {
  busy.value = true;
  error.value = "";
  connection.value = "";
  discovery.value = undefined;
  try {
    await action();
  } catch (e) {
    if (!signal.aborted) error.value = message(e);
  } finally {
    busy.value = false;
  }
}
watch(
  () => allowed("read"),
  (canRead) => {
    if (canRead) void run(load);
    else {
      items.value = [];
      discovery.value = undefined;
    }
  },
  { immediate: true },
);
const selected = ref<CosmosDbConnection>();
const database = ref(""),
  container = ref("");
const formatPolicy = (value: string) =>
  JSON.stringify(JSON.parse(value), null, 2);
function show(
  item: CosmosDbConnection,
  databaseName?: string,
  containerName?: string,
) {
  void run(async () => {
    selected.value = item;
    selectedName.value = item.name || "";
    discovery.value = await client.cosmosDbIntegrationDiscovery(
      item.id!,
      databaseName,
      containerName,
      signal,
    );
  });
}
function inspect(item: CosmosDbConnection) {
  database.value = "";
  container.value = "";
  show(item);
}
function lookup() {
  if (selected.value && database.value)
    show(selected.value, database.value, container.value || undefined);
}
function probe(item: CosmosDbConnection) {
  void run(async () => {
    const result = await client.cosmosDbIntegrationValidateSaved(
      item.id!,
      signal,
    );
    connection.value = item.name + " · " + result.status;
  });
}
async function edit(item?: CosmosDbConnection) {
  formError.value = "";
  form.value = item
    ? {
        id: item.id!,
        name: item.name!,
        dataStoreId: item.dataStoreId!,
        accountResourceId: item.accountResourceId!,
        authMode: item.authMode!,
      }
    : empty();
  dialog.value = true;
  try {
    datastores.value = (await client.dataStoreAll(signal)).filter(
      (b) => b.kind?.toLowerCase() === "azure cosmos db",
    );
  } catch (e) {
    formError.value = message(e);
  }
}
async function save() {
  saving.value = true;
  formError.value = "";
  try {
    const { id, name, dataStoreId, accountResourceId, authMode } = form.value;
    if (id)
      await client.cosmosDbIntegrationPUT(
        id,
        new UpdateCosmosDbIntegration({
          id,
          name,
          dataStoreId,
          accountResourceId,
          authMode,
        }),
        signal,
      );
    else
      await client.cosmosDbIntegrationPOST(
        new CreateCosmosDbIntegration({
          name,
          dataStoreId,
          accountResourceId,
          authMode,
        }),
        signal,
      );
    dialog.value = false;
    await run(load);
  } catch (e) {
    formError.value = message(e);
  } finally {
    saving.value = false;
  }
}
function remove(item: CosmosDbConnection) {
  q.dialog({
    title: "Delete Azure Cosmos DB integration?",
    message: "Remove " + item.name + " from Fuse?",
    cancel: true,
  }).onOk(() =>
    run(async () => {
      await client.cosmosDbIntegrationDELETE(item.id!, signal);
      await load();
    }),
  );
}
</script>

<style scoped src="../styles/pages.css"></style>
<style scoped>
h2,
h3 {
  margin: 1.5rem 0 0.75rem;
  line-height: 1.4;
}
h2 {
  font-size: 1.5rem;
}
h3 {
  font-size: 1.2rem;
}
.lookup {
  display: flex;
  gap: 1rem;
  align-items: center;
  flex-wrap: wrap;
}
pre {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
.editor {
  width: 34rem;
  max-width: 95vw;
}
.error {
  background: var(--fuse-status-error-bg);
  color: var(--fuse-text);
}
section,
.integration {
  overflow-wrap: anywhere;
}
@media (max-width: 700px) {
  .integration {
    flex-direction: column;
    align-items: stretch;
  }
  .integration .q-item__section--side {
    padding: 0.5rem 0 0;
  }
}
</style>
