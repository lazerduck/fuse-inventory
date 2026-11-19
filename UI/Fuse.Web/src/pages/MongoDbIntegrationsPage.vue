<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>MongoDB Integrations</h1>
        <p class="subtitle">
          Inspect database access, users and inherited roles without reading
          documents.
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
            <IntegrationPasswordButton provider="mongodb" :integration-id="item.id" @saved="load" />
            <q-btn v-if="store.hasPermission('integrationreviews:read')" flat label="Reviews"
              :to="{ name: 'integrationReviews', query: { provider: 'mongodb', integration: item.id } }" />
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
          >No MongoDB integrations configured.</q-item-section
        ></q-item
      >
    </q-list>
    <q-banner v-if="connection" class="q-my-md"
      >Authentication: {{ connection }}</q-banner
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
      <h3>Collections — {{ discovery.collectionsStatus }}</h3>
      <q-list v-if="discovery.collections.length" bordered separator>
        <q-item v-for="name in discovery.collections" :key="name">
          <q-item-section>{{ name }}<q-item-label caption>{{ discovery.collectionTypes?.[name] }}</q-item-label></q-item-section>
          <q-item-section side>
            <q-btn v-if="discovery.collectionTypes?.[name] !== 'view'" flat dense
              label="Indexes" :aria-label="'Inspect indexes on ' + name" :disable="busy" @click="loadIndexes(name)" />
          </q-item-section>
        </q-item>
      </q-list>
      <p v-else>No collections observed.</p>
      <section v-if="indexInventory" aria-live="polite">
        <h3>Indexes on {{ indexInventory.collection }}</h3>
        <p>Visibility: {{ indexInventory.status }}</p>
        <q-list v-if="indexInventory.indexes.length" bordered separator>
          <q-item v-for="index in indexInventory.indexes" :key="index.name">
            <q-item-section>
              <q-item-label>{{ index.name }}</q-item-label>
              <q-item-label caption>{{ index.keys.map(k => k.field + ' ' + k.order).join(', ') }}
                · {{ index.unique ? 'Unique' : 'Non-unique' }}
                · {{ index.expireAfterSeconds == null ? 'No TTL' : 'Expires after ' + index.expireAfterSeconds + ' s' }}
              </q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
      </section>
      <h3>User discovery coverage</h3>
      <q-list bordered
        ><q-item
          v-for="scope in discovery.userScopes"
          :key="scope.authenticationDatabase"
          ><q-item-section>{{ scope.authenticationDatabase }}</q-item-section
          ><q-item-section side>{{ scope.status }}</q-item-section></q-item
        ></q-list
      >
      <h3>Users and inherited roles</h3>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>User</th>
            <th>Auth database</th>
            <th>Direct roles</th>
            <th>Inherited roles</th>
            <th>Privilege inspection</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="user in discovery.users"
            :key="JSON.stringify([user.authenticationDatabase, user.username])"
          >
            <td>{{ user.username }}</td>
            <td>{{ user.authenticationDatabase }}</td>
            <td>{{ roleNames(user.roles) }}</td>
            <td>{{ roleNames(user.inheritedRoles) }}</td>
            <td>{{ user.privilegesStatus }}</td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>Account comparison</h3>
      <p>
        For MongoDB accounts, add a Parameters entry named
        <code>authSource</code> containing the account's authentication
        database. Both username and authSource are matched exactly.
      </p>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Username</th>
            <th>Auth database</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in discovery.accounts" :key="row.accountId">
            <td>{{ row.username || "(no username)" }}</td>
            <td>{{ row.authenticationDatabase || "(not specified)" }}</td>
            <td>{{ row.status }}</td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>
        Roles defined in {{ discovery.database }} — {{ discovery.rolesStatus }}
      </h3>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Role</th>
            <th>Built-in</th>
            <th>Direct roles</th>
            <th>Inherited roles</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="role in discovery.roles"
            :key="JSON.stringify([role.database, role.name])"
          >
            <td>{{ role.database }} / {{ role.name }}</td>
            <td>{{ role.isBuiltin ? "Yes" : "No" }}</td>
            <td>{{ roleNames(role.roles) }}</td>
            <td>{{ roleNames(role.inheritedRoles) }}</td>
          </tr>
        </tbody></q-markup-table
      >
      <h3>Effective privileges for the selected database</h3>
      <q-select
        v-model="principalFilter"
        :options="principalOptions"
        label="User or role"
        emit-value
        map-options
        clearable
        class="q-mb-md"
      />
      <q-table
        flat
        bordered
        wrap-cells
        :rows="privilegeRows"
        :columns="privilegeColumns"
        row-key="key"
        :pagination="{ rowsPerPage: 15 }"
        :rows-per-page-options="[15, 30, 100]"
      />
    </section>
    <q-dialog v-model="dialog"
      ><q-card style="width: 600px; max-width: 95vw"
        ><q-form @submit="save"
          ><q-card-section
            ><h2>{{ form.id ? "Edit" : "Add" }} MongoDB integration</h2>
            <q-input v-model="form.name" label="Name" :rules="[required]" />
            <q-select
              v-model="form.dataStoreId"
              :options="datastores"
              option-value="id"
              option-label="name"
              emit-value
              map-options
              label="MongoDB data store"
              :rules="[required]"
            />
            <q-input
              v-model="form.endpoint"
              label="Endpoint (mongodb://host:27017/database)"
              :rules="[required]"
            />
            <q-input
              v-model="form.username"
              label="Inspection username"
              :rules="[required]"
            />
            <q-input
              v-model="form.authenticationDatabase"
              label="Inspection authentication database"
              :rules="[required]"
              hint="Where the inspection user was created, often admin. This can differ from the database in the endpoint."
            />
            <q-checkbox
              v-model="form.useTls"
              label="Use TLS with certificate verification"
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
import { computed, ref, watch, onBeforeUnmount } from "vue";
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
  authenticationDatabase: string;
  useTls: boolean;
  hasPassword?: boolean;
}
interface RoleReference {
  name: string;
  database: string;
}
interface Privilege {
  scope: string;
  database?: string;
  collection?: string;
  actions: string[];
}
interface Discovery {
  database: string;
  observedAt: string;
  connectionStatus: string;
  collectionsStatus: string;
  collections: string[];
  collectionTypes?: Record<string, string>;
  rolesStatus: string;
  warnings: string[];
  roles: {
    name: string;
    database: string;
    isBuiltin: boolean;
    roles: RoleReference[];
    inheritedRoles: RoleReference[];
    privileges: Privilege[];
  }[];
  users: {
    username: string;
    authenticationDatabase: string;
    roles: RoleReference[];
    inheritedRoles: RoleReference[];
    privileges: Privilege[];
    privilegesStatus: string;
  }[];
  userScopes: { authenticationDatabase: string; status: string }[];
  accounts: {
    accountId: string;
    username?: string;
    authenticationDatabase?: string;
    status: string;
  }[];
}
const roleNames = (roles: RoleReference[]) =>
  roles.map((r) => `${r.database} / ${r.name}`).join(", ") || "None";
const principalFilter = ref<string | null>(null);
const principals = computed(() => [
  ...(discovery.value?.users ?? []).map((u) => ({
    key: JSON.stringify(["user", u.authenticationDatabase, u.username]),
    label: `User: ${u.authenticationDatabase} / ${u.username}`,
    privileges: u.privileges,
  })),
  ...(discovery.value?.roles ?? []).map((r) => ({
    key: JSON.stringify(["role", r.database, r.name]),
    label: `Role: ${r.database} / ${r.name}`,
    privileges: r.privileges,
  })),
]);
const principalOptions = computed(() =>
  principals.value.map((p) => ({ label: p.label, value: p.key })),
);
const privilegeRows = computed(() =>
  principals.value
    .filter((p) => !principalFilter.value || p.key === principalFilter.value)
    .flatMap((p) =>
      p.privileges.map((v, i) => ({
        ...v,
        principal: p.label,
        key: p.key + ":" + i,
      })),
    ),
);
const privilegeColumns = [
  {
    name: "principal",
    label: "User / role",
    field: "principal",
    sortable: true,
  },
  { name: "scope", label: "Scope", field: "scope" },
  {
    name: "database",
    label: "Database",
    field: "database",
    format: (v?: string) => v || "(broader scope)",
  },
  {
    name: "collection",
    label: "Collection",
    field: "collection",
    format: (v?: string) => v == null ? "—" : v || "(all collections)",
  },
  {
    name: "actions",
    label: "Actions",
    field: "actions",
    format: (v: string[]) => v.join(", "),
  },
];
const store = useFuseStore(),
  q = useQuasar(),
  auth = useAuthToken(),
  controller = new AbortController();
const allowed = (action: string) =>
  store.hasPermission("mongodbintegrations:" + action);
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
    authenticationDatabase: "admin",
    useTls: false,
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
    `${import.meta.env.VITE_API_BASE_URL ?? ""}/api/MongoDbIntegration${path}`,
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
      (x) => x.kind?.toLowerCase() === "mongodb",
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
const inspectedId = ref("");
interface IndexInventory {
  collection: string;
  status: string;
  indexes: { name: string; keys: { field: string; order: string }[]; unique: boolean; expireAfterSeconds?: number }[];
}
const indexInventory = ref<IndexInventory | null>(null);
async function loadIndexes(name: string) {
  busy.value = true;
  error.value = "";
  indexInventory.value = null;
  try {
    indexInventory.value = await request<IndexInventory>(
      "/" + inspectedId.value + "/indexes?collection=" + encodeURIComponent(name));
  } catch (e) {
    error.value = String(e);
  } finally {
    busy.value = false;
  }
}
async function inspect(item: Integration) {
  inspectedId.value = item.id;
  indexInventory.value = null;
  busy.value = true;
  error.value = "";
  discovery.value = null;
  principalFilter.value = null;
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
