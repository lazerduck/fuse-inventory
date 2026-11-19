<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Azure Service Bus Integrations</h1>
        <p class="subtitle">
          Inspect queues and shared access policies in an Azure Service Bus
          namespace.
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
            item.namespaceResourceId
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
          >No Azure Service Bus integrations configured.</q-item-section
        ></q-item
      >
    </q-list>
    <p v-if="connection" aria-live="polite" class="q-my-md">{{ connection }}</p>
    <section v-if="discovery" class="q-mt-lg">
      <h2>{{ selectedName }}</h2>
      <p>
        {{ discovery.namespaceResourceId }} ·
        {{ discovery.observedAt?.toLocaleString() }}
      </p>
      <h3>Namespace policies</h3>
      <p>Visibility: {{ discovery.namespaceAuthorizationRules?.visibility }}</p>
      <p
        v-for="rule in discovery.namespaceAuthorizationRules?.policies"
        :key="rule.name"
      >
        {{ rule.name }} · {{ rule.rights?.join(", ") }}
      </p>
      <p v-if="discovery.namespaceAuthorizationRules?.visibility === 'unknown'">
        No namespace policies were visible to this identity.
      </p>
      <h3>Queues</h3>
      <p>Queue listing: {{ discovery.queueVisibility }}</p>
      <q-banner
        v-if="discovery.queueVisibility !== 'visible'"
        class="warning q-mb-md"
      >
        The namespace listing is incomplete. Unobserved inventory queues have
        unknown status.
      </q-banner>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Queue</th>
            <th>Status</th>
            <th>Queue policies</th>
            <th>Policy visibility</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="queue in discovery.queues" :key="queue.name">
            <td>{{ queue.name }}</td>
            <td>{{ queue.status || "Unknown" }}</td>
            <td>
              <div
                v-for="rule in queue.authorizationRules?.policies"
                :key="rule.name"
              >
                {{ rule.name }} · {{ rule.rights?.join(", ") }}
              </div>
              <span v-if="!queue.authorizationRules?.policies?.length">{{
                queue.authorizationRules?.visibility === "visible"
                  ? "None observed"
                  : "Unknown"
              }}</span>
            </td>
            <td>{{ queue.authorizationRules?.visibility }}</td>
          </tr>
          <tr v-if="!discovery.queues?.length">
            <td colspan="4">No queues observed.</td>
          </tr>
        </tbody>
      </q-markup-table>
      <template v-if="discovery.inventory?.length">
        <h3>Broker inventory</h3>
        <q-list bordered separator
          ><q-item v-for="row in discovery.inventory" :key="row.queueId">
            <q-item-section>{{ row.name }}</q-item-section><q-item-section
              side
              >{{ row.status }}</q-item-section
            >
          </q-item></q-list
        >
      </template>
    </section>
    <q-dialog v-model="dialog" :persistent="saving"
      ><q-card class="editor"
        ><q-form @submit="save">
          <q-card-section>
            <h2>
              {{ form.id ? "Edit" : "Add" }} Azure Service Bus integration
            </h2>
            <q-input v-model="form.name" label="Name" :rules="[required]" />
            <q-select
              v-model="form.messageBrokerId"
              :options="brokers"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              label="Azure Service Bus broker"
              :rules="[required]"
            >
              <template #no-option
                ><q-item
                  ><q-item-section
                    >Create a broker with kind Azure Service Bus in Message
                    Brokers.</q-item-section
                  ></q-item
                ></template
              >
            </q-select>
            <q-input
              v-model="form.namespaceResourceId"
              label="Namespace resource ID"
              hint="Copy the full resource ID from the Azure namespace Properties page."
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
  CreateServiceBusIntegration,
  UpdateServiceBusIntegration,
  type ServiceBusDiscoveryResponse,
  type ServiceBusConnection,
  type MessageBroker,
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
  store.hasPermission("servicebusintegrations:" + action);
const items = ref<ServiceBusConnection[]>([]),
  discovery = ref<ServiceBusDiscoveryResponse>();
const busy = ref(false),
  saving = ref(false),
  dialog = ref(false);
const connection = ref(""),
  selectedName = ref(""),
  error = ref(""),
  formError = ref("");
const brokers = ref<MessageBroker[]>([]);
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
  messageBrokerId: "",
  namespaceResourceId: "",
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
  items.value = await client.serviceBusIntegrationAll(signal);
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
function inspect(item: ServiceBusConnection) {
  void run(async () => {
    selectedName.value = item.name || "";
    discovery.value = await client.serviceBusIntegrationDiscovery(
      item.id!,
      signal,
    );
  });
}
function probe(item: ServiceBusConnection) {
  void run(async () => {
    const result = await client.serviceBusIntegrationValidateSaved(
      item.id!,
      signal,
    );
    connection.value = item.name + " · " + result.status;
  });
}
async function edit(item?: ServiceBusConnection) {
  formError.value = "";
  form.value = item
    ? {
        id: item.id!,
        name: item.name!,
        messageBrokerId: item.messageBrokerId!,
        namespaceResourceId: item.namespaceResourceId!,
        authMode: item.authMode!,
      }
    : empty();
  dialog.value = true;
  try {
    brokers.value = (await client.messageBrokerAll(signal)).filter(
      (b) => b.kind?.toLowerCase() === "azure service bus",
    );
  } catch (e) {
    formError.value = message(e);
  }
}
async function save() {
  saving.value = true;
  formError.value = "";
  try {
    const { id, name, messageBrokerId, namespaceResourceId, authMode } =
      form.value;
    if (id)
      await client.serviceBusIntegrationPUT(
        id,
        new UpdateServiceBusIntegration({
          id,
          name,
          messageBrokerId,
          namespaceResourceId,
          authMode,
        }),
        signal,
      );
    else
      await client.serviceBusIntegrationPOST(
        new CreateServiceBusIntegration({
          name,
          messageBrokerId,
          namespaceResourceId,
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
function remove(item: ServiceBusConnection) {
  q.dialog({
    title: "Delete Azure Service Bus integration?",
    message: "Remove " + item.name + " from Fuse?",
    cancel: true,
  }).onOk(() =>
    run(async () => {
      await client.serviceBusIntegrationDELETE(item.id!, signal);
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
.editor {
  width: 34rem;
  max-width: 95vw;
}
.warning {
  background: var(--fuse-status-drift-bg);
  color: var(--fuse-text);
}
.error {
  background: var(--fuse-status-error-bg);
  color: var(--fuse-text);
}
section,
.integration {
  overflow-wrap: anywhere;
}
th,
td {
  text-align: left;
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
