<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>PostgreSQL Integrations</h1>
        <p class="subtitle">
          Inspect roles, inherited access and permissions in one database.
        </p>
      </div>
      <q-btn
        v-if="allowed('create')"
        label="Add integration"
        color="primary"
        @click="edit()"
      />
    </div>
    <q-banner v-if="error" class="inspection-error q-mb-md">{{
      error
    }}</q-banner>
    <q-linear-progress v-if="busy" indeterminate />
    <q-list bordered separator>
      <q-item v-for="item in items" :key="item.id"
        ><q-item-section
          ><q-item-label>{{ item.name }}</q-item-label
          ><q-item-label caption>{{
            item.endpoint
          }}</q-item-label></q-item-section
        >
        <q-item-section side
          ><div class="q-gutter-sm">
            <IntegrationPasswordButton provider="postgresql" :integration-id="item.id" @saved="load" />
            <q-btn v-if="store.hasPermission('integrationreviews:read')" flat label="Reviews"
              :to="{ name: 'integrationReviews', query: { provider: 'postgresql', integration: item.id } }" />
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
          >No PostgreSQL integrations configured.</q-item-section
        ></q-item
      >
    </q-list>
    <q-banner v-if="connection" class="q-my-md"
      >Connection: {{ connection }}</q-banner
    >
    <section v-if="discovery" class="q-mt-lg">
      <h2>Database: {{ discovery.database }}</h2>
      <p>
        Observed {{ new Date(discovery.observedAt).toLocaleString() }} ·
        Connection: {{ discovery.connectionStatus }}
      </p>
      <q-banner
        v-for="warning in discovery.warnings"
        :key="warning"
        class="inspection-warning q-mb-sm"
        >{{ warning }}</q-banner
      >
      <h3>Cluster roles — {{ discovery.rolesStatus }}</h3>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Role</th>
            <th>Login</th>
            <th>Connect to database</th>
            <th>Attributes</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="role in discovery.roles" :key="role.name">
            <td>{{ role.name }}</td>
            <td>{{ role.canLogin ? "Yes" : "No" }}</td>
            <td>{{ role.canConnect ? "Yes" : "No" }}</td>
            <td>
              {{
                [
                  role.superuser && "Superuser",
                  role.createRole && "Create role",
                  role.createDatabase && "Create database",
                  role.bypassRowSecurity && "Bypass row security",
                ]
                  .filter(Boolean)
                  .join(", ") || "—"
              }}
            </td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>Memberships — {{ discovery.membershipsStatus }}</h3>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Member</th>
            <th>Role</th>
            <th>Direct</th>
            <th>Available immediately</th>
            <th>Can SET ROLE</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in discovery.memberships"
            :key="row.member + ':' + row.role"
          >
            <td>{{ row.member }}</td>
            <td>{{ row.role }}</td>
            <td>{{ row.direct ? "Yes" : "No" }}</td>
            <td>{{ row.inherited ? "Yes" : "No" }}</td>
            <td>{{ row.canSetRole ? "Yes" : "No" }}</td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>Account comparison</h3>
      <p v-if="!discovery.accounts.length">
        No accounts documented for this data store.
      </p>
      <q-list bordered
        ><q-item v-for="row in discovery.accounts" :key="row.accountId"
          ><q-item-section>{{ row.username || "(no username)" }}</q-item-section
          ><q-item-section side>{{ row.status }}</q-item-section></q-item
        ></q-list
      >
      <h3>Database privileges — {{ discovery.permissionsStatus }}</h3>
      <q-select
        v-model="roleFilter"
        :options="discovery.roles.map((r) => r.name)"
        label="Filter privileges by role"
        clearable
        class="q-mb-md"
      />
      <q-table
        flat
        bordered
        wrap-cells
        :rows="
          discovery.permissions.filter(
            (p) => !roleFilter || p.role === roleFilter,
          )
        "
        :columns="permissionColumns"
        :row-key="permissionKey"
        :pagination="{ rowsPerPage: 15 }"
        :rows-per-page-options="[15, 30, 100]"
      />
    </section>
    <q-dialog v-model="dialog"
      ><q-card style="width: 600px; max-width: 95vw"
        ><q-form @submit="save"
          ><q-card-section
            ><h2>{{ form.id ? "Edit" : "Add" }} PostgreSQL integration</h2>
            <q-input v-model="form.name" label="Name" :rules="[required]" />
            <q-select
              v-model="form.dataStoreId"
              :options="datastores"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              label="PostgreSQL data store"
              :rules="[required]"
            />
            <q-input
              v-model="form.endpoint"
              label="Endpoint (postgresql://host:5432/database)"
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
            <q-banner v-if="formError" class="inspection-error">{{
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
import IntegrationPasswordButton from '../components/integrations/IntegrationPasswordButton.vue'
import { ref, watch, onBeforeUnmount } from "vue";
import { useQuasar } from "quasar";
import { useAuthToken } from "../composables/useAuthToken";
import { useFuseStore } from "../stores/FuseStore";
import { useFuseClient } from "../composables/useFuseClient";
interface Integration {
  id: string;
  name: string;
  dataStoreId: string;
  endpoint: string;
  username: string;
  hasPassword?: boolean;
}
interface Discovery {
  database: string;
  observedAt: string;
  connectionStatus: string;
  rolesStatus: string;
  membershipsStatus: string;
  permissionsStatus: string;
  warnings: string[];
  roles: {
    name: string;
    canLogin: boolean;
    canConnect: boolean;
    superuser: boolean;
    createRole: boolean;
    createDatabase: boolean;
    bypassRowSecurity: boolean;
  }[];
  memberships: {
    member: string;
    role: string;
    direct: boolean;
    inherited: boolean;
    canSetRole: boolean;
  }[];
  permissions: {
    role: string;
    kind: string;
    name: string;
    privileges: string[];
  }[];
  accounts: { accountId: string; username?: string; status: string }[];
}
const roleFilter = ref<string | null>(null);
const permissionKey = (row: Discovery["permissions"][number]) =>
  JSON.stringify([row.role, row.kind, row.name]);
const permissionColumns = [
  { name: "role", label: "Role", field: "role", sortable: true },
  { name: "kind", label: "Kind", field: "kind", sortable: true },
  { name: "name", label: "Object", field: "name", sortable: true },
  {
    name: "privileges",
    label: "Available privileges",
    field: "privileges",
    format: (v: string[]) => v.join(", ") || "None",
  },
];
const store = useFuseStore(),
  q = useQuasar(),
  auth = useAuthToken(),
  controller = new AbortController();
const allowed = (action: string) =>
  store.hasPermission("postgresqlintegrations:" + action);
const items = ref<Integration[]>([]),
  datastores = ref<{ id?: string; name?: string }[]>([]),
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
    dataStoreId: "",
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
    `${import.meta.env.VITE_API_BASE_URL ?? ""}/api/PostgreSqlIntegration${path}`,
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
    datastores.value = (await useFuseClient().dataStoreAll()).filter(
      (x) => x.kind?.toLowerCase() === "postgresql",
    );
  } catch {
    formError.value =
      "Unable to load message datastores. Check your inventory permissions.";
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
  roleFilter.value = null;
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
.inspection-warning {
  background-color: var(--fuse-status-drift-bg);
  color: var(--fuse-text);
}
.inspection-error {
  background-color: var(--fuse-status-error-bg);
  color: var(--fuse-text);
}

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
