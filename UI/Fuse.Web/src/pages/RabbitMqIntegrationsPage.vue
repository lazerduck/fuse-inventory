<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>RabbitMQ Integrations</h1>
        <p class="subtitle">
          Inspect a virtual host and compare documented queues and accounts.
        </p>
      </div>
      <q-btn
        v-if="allowed('create')"
        label="Add integration"
        color="primary"
        @click="edit()"
      />
    </div>
    <q-banner v-if="error" class="review-error q-mb-md">{{
      error
    }}</q-banner>
    <q-linear-progress v-if="busy" indeterminate />
    <q-list bordered separator>
      <q-item v-for="item in items" :key="item.id"
        ><q-item-section
          ><q-item-label>{{ item.name }}</q-item-label
          ><q-item-label caption
            >{{ item.endpoint }} · Virtual host:
            {{ item.virtualHost }}</q-item-label
          ></q-item-section
        >
        <q-item-section side
          ><div class="q-gutter-sm">
            <q-btn
              v-if="store.hasPermission('integrationreviews:read')"
              flat
              label="Reviews"
              :to="{ name: 'integrationReviews', query: { provider: 'rabbitmq', integration: item.id } }"
            />
            <q-btn
              flat
              label="Inspect"
              :disable="busy"
              @click="inspect(item)"
            /><q-btn
              v-if="allowed('validate')"
              flat
              label="Test connection"
              :disable="busy"
              @click="probe(item)"
            /><q-btn
              v-if="allowed('update')"
              flat
              icon="edit"
              aria-label="Edit integration"
              @click="edit(item)"
            /><q-btn
              v-if="allowed('delete')"
              flat
              icon="delete"
              aria-label="Delete integration"
              @click="remove(item)"
            /></div
        ></q-item-section>
      </q-item>
      <q-item v-if="!items.length"
        ><q-item-section
          >No RabbitMQ integrations configured.</q-item-section
        ></q-item
      >
    </q-list>
    <q-banner v-if="connection" class="q-my-md"
      >Management API: {{ connection }}</q-banner
    >
    <section v-if="discovery" class="q-mt-lg">
      <h2>Virtual host: {{ discovery.virtualHost }}</h2>
      <p>
        Observed {{ new Date(discovery.observedAt).toLocaleString() }} ·
        Connection: {{ discovery.connectionStatus }}
      </p>
      <q-banner
        v-for="warning in discovery.warnings"
        :key="warning"
        class="review-warning q-mb-sm"
        >{{ warning }}</q-banner
      >
      <h3>Topology — {{ discovery.topologyStatus }}</h3>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Kind</th>
            <th>Name / source</th>
            <th>Type</th>
            <th>Durability</th>
            <th>Dead-letter exchange</th>
            <th>Destination</th>
            <th>Routing key</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, i) in discovery.resources" :key="i">
            <td>{{ row.kind }}</td>
            <td>{{ row.name || "(default exchange)" }}</td>
            <td>{{ row.type }}</td>
            <td>{{ row.durable == null ? '—' : row.durable ? 'Durable' : 'Transient' }}</td>
            <td>{{ row.kind !== 'queues' ? '—' : row.deadLetterExchange === '' ? '(default exchange)' : row.deadLetterExchange ?? (row.routingOptionsKnown ? 'None' : 'Unknown') }}</td>
            <td>{{ row.destination }}</td>
            <td>{{ row.routingKey }}</td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>Documented queues</h3>
      <p v-if="!discovery.queues.length">
        No queues documented for this broker.
      </p>
      <q-list bordered
        ><q-item v-for="row in discovery.queues" :key="row.name"
          ><q-item-section>{{ row.name }}</q-item-section
          ><q-item-section side>{{ row.status }}</q-item-section></q-item
        ></q-list
      >
      <h3>Users — {{ discovery.usersStatus }}</h3>
      <p>{{ discovery.users.join(", ") || "No users observed" }}</p>
      <h3>Account comparison</h3>
      <q-list bordered
        ><q-item v-for="row in discovery.accounts" :key="row.accountId"
          ><q-item-section>{{ row.username }}</q-item-section
          ><q-item-section side>{{ row.status }}</q-item-section></q-item
        ></q-list
      >
      <h3>Virtual host permissions — {{ discovery.permissionsStatus }}</h3>
      <p>
        Resource-name patterns control broker operations. A topic exchange can also restrict the routing key for publishers.
      </p>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>User</th>
            <th>Configure</th>
            <th>Write</th>
            <th>Read</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in discovery.permissions" :key="row.username">
            <td>{{ row.username }}</td>
            <td>{{ row.configure }}</td>
            <td>{{ row.write }}</td>
            <td>{{ row.read }}</td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>Topic permissions — {{ discovery.topicPermissionsStatus }}</h3>
      <q-markup-table v-if="discovery.topicPermissions?.length" flat bordered wrap-cells>
        <thead><tr><th>User</th><th>Exchange</th><th>Write key pattern</th><th>Read key pattern</th></tr></thead>
        <tbody><tr v-for="row in discovery.topicPermissions" :key="JSON.stringify([row.username, row.exchange])">
          <td>{{ row.username }}</td><td>{{ row.exchange }}</td><td>{{ row.write }}</td><td>{{ row.read }}</td>
        </tr></tbody>
      </q-markup-table>
      <p v-else>{{ discovery.topicPermissionsStatus === 'complete' ? 'No additional topic restrictions observed.' : 'Topic permission evidence is unavailable.' }}</p>
    </section>
    <q-dialog v-model="dialog"
      ><q-card style="width: 600px; max-width: 95vw"
        ><q-form @submit="save"
          ><q-card-section
            ><h2>{{ form.id ? "Edit" : "Add" }} RabbitMQ integration</h2>
            <q-input v-model="form.name" label="Name" :rules="[required]" />
            <q-select
              v-model="form.messageBrokerId"
              :options="brokers"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              label="RabbitMQ message broker"
              :rules="[required]"
            />
            <p>
              Documented queues on this broker are compared within the selected
              virtual host only.
            </p>
            <q-input
              v-model="form.virtualHost"
              label="Virtual host"
              :rules="[required]"
            />
            <q-input
              v-model="form.endpoint"
              label="Management origin (http://host:15672)"
              :rules="[required]"
            />
            <q-input
              v-model="form.username"
              label="Inspection username"
              :rules="[required]"
            />
            <q-input
              v-model="password"
              type="password"
              autocomplete="new-password"
              label="Password"
              :hint="form.id ? 'Leave blank to retain the saved password.' : ''"
            />
            <q-checkbox
              v-if="form.id"
              v-model="clearPassword"
              label="Clear saved password"
            />
            <q-banner v-if="formError" class="bg-red-1">{{
              formError
            }}</q-banner> </q-card-section
          ><q-card-actions align="right"
            ><q-btn flat label="Cancel" v-close-popup /><q-btn
              label="Save"
              type="submit"
              color="primary"
              :loading="saving" /></q-card-actions></q-form></q-card
    ></q-dialog>
  </div>
</template>
<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from "vue";
import { useQuasar } from "quasar";
import { useAuthToken } from "../composables/useAuthToken";
import { useFuseStore } from "../stores/FuseStore";
import { useFuseClient } from "../composables/useFuseClient";
interface Integration {
  id: string;
  name: string;
  messageBrokerId: string;
  virtualHost: string;
  endpoint: string;
  username: string;
  hasPassword?: boolean;
}
interface Discovery {
  virtualHost: string;
  observedAt: string;
  connectionStatus: string;
  topologyStatus: string;
  usersStatus: string;
  permissionsStatus: string;
  warnings: string[];
  resources: {
    name: string;
    kind: string;
    type?: string;
    destination?: string;
    routingKey?: string;
    durable?: boolean;
    deadLetterExchange?: string;
    routingOptionsKnown?: boolean;
  }[];
  queues: { name: string; status: string }[];
  users: string[];
  topicPermissionsStatus: string;
  topicPermissions: { username: string; exchange: string; write: string; read: string }[];
  permissions: {
    username: string;
    configure: string;
    write: string;
    read: string;
  }[];
  accounts: { accountId: string; username?: string; status: string }[];
}
const store = useFuseStore(),
  q = useQuasar(),
  auth = useAuthToken(),
  controller = new AbortController();
const allowed = (action: string) =>
  store.hasPermission("rabbitmqintegrations:" + action);
const items = ref<Integration[]>([]),
  brokers = ref<{ id?: string; name?: string }[]>([]),
  busy = ref(false),
  error = ref(""),
  dialog = ref(false),
  saving = ref(false),
  formError = ref(""),
  password = ref(""),
  clearPassword = ref(false),
  connection = ref(""),
  discovery = ref<Discovery | null>(null);
const empty = (): Integration => ({
    id: "",
    name: "",
    messageBrokerId: "",
    virtualHost: "/",
    endpoint: "",
    username: "",
  }),
  form = ref(empty());
const required = (v: unknown) => !!v || "Required";
async function request<T>(
  path = "",
  method = "GET",
  body?: unknown,
): Promise<T> {
  const headers = new Headers({ "Content-Type": "application/json" }),
    token = auth.getToken();
  if (token) headers.set("Authorization", "Bearer " + token);
  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL ?? ""}/api/RabbitMqIntegration${path}`,
    {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    },
  );
  if (response.status === 401) {
    auth.clearToken();
    window.dispatchEvent(new Event("fuse-auth-invalid"));
  }
  const value = await response.json().catch(() => null);
  if (!response.ok)
    throw new Error(value?.error ?? `Request failed (${response.status})`);
  return value;
}
async function load() {
  if (allowed("read")) items.value = await request<Integration[]>();
}
async function edit(item?: Integration) {
  form.value = item ? { ...item } : empty();
  password.value = "";
  clearPassword.value = false;
  formError.value = "";
  dialog.value = true;
  try {
    brokers.value = (await useFuseClient().messageBrokerAll()).filter(
      (x) => x.kind?.toLowerCase() === "rabbitmq",
    );
  } catch {
    formError.value =
      "Unable to load message brokers. Check your inventory permissions.";
  }
}
async function save() {
  saving.value = true;
  formError.value = "";
  try {
    await request(
      form.value.id ? "/" + form.value.id : "",
      form.value.id ? "PUT" : "POST",
      {
        ...form.value,
        password: clearPassword.value ? "" : password.value || undefined,
      },
    );
    dialog.value = false;
    discovery.value = null;
    connection.value = "";
    await load();
  } catch (e) {
    formError.value = String(e);
  } finally {
    saving.value = false;
  }
}
async function inspect(item: Integration) {
  busy.value = true;
  error.value = "";
  discovery.value = null;
  connection.value = "";
  try {
    discovery.value = await request<Discovery>("/" + item.id + "/discovery");
  } catch (e) {
    error.value = String(e);
  } finally {
    busy.value = false;
  }
}
async function probe(item: Integration) {
  busy.value = true;
  error.value = "";
  connection.value = "";
  try {
    const result = await request<{ status: string; errorMessage?: string }>(
      "/" + item.id + "/validate",
      "POST",
    );
    connection.value = result.errorMessage ?? result.status;
  } catch (e) {
    error.value = String(e);
  } finally {
    busy.value = false;
  }
}
function remove(item: Integration) {
  q.dialog({
    title: "Delete integration?",
    message: "Remove this saved connection from Fuse?",
    cancel: true,
  }).onOk(async () => {
    try {
      await request("/" + item.id, "DELETE");
      discovery.value = null;
      await load();
    } catch (e) {
      error.value = String(e);
    }
  });
}
watch(
  () => allowed("read"),
  () => load().catch((e) => (error.value = String(e))),
  { immediate: true },
);
onBeforeUnmount(() => controller.abort());
</script>
<style scoped src="../styles/pages.css"></style>
<style scoped>
.review-warning { background: var(--fuse-status-drift-bg); color: var(--fuse-text); }
.review-error { background: var(--fuse-status-error-bg); color: var(--fuse-text); }
h2 {
  font-size: 1.5rem;
  line-height: 1.4;
  margin: 24px 0 12px;
}
h3 {
  font-size: 1.2rem;
  line-height: 1.4;
  margin: 24px 0 12px;
}
.q-item__label {
  overflow-wrap: anywhere;
}
@media (max-width: 650px) {
  .q-item {
    flex-wrap: wrap;
    gap: 8px;
  }
  .q-item__section--side {
    padding-left: 0;
    max-width: 100%;
  }
  .q-gutter-sm {
    display: flex;
    flex-wrap: wrap;
  }
}
</style>
