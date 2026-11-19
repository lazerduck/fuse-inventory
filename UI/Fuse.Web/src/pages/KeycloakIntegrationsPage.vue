<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Keycloak Integrations</h1>
        <p class="subtitle">
          Inspect clients and service-account roles in a realm.
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
            item.issuer
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
          >No Keycloak integrations configured.</q-item-section
        ></q-item
      >
    </q-list>
    <p v-if="connection" aria-live="polite" class="q-my-md">{{ connection }}</p>
    <section v-if="discovery" class="q-mt-lg">
      <h2>{{ selectedName }}</h2>
      <p>
        {{ discovery.issuer }} · {{ discovery.observedAt?.toLocaleString() }}
      </p>
      <p>
        Authentication: {{ discovery.connectionStatus }} · Client visibility:
        {{ discovery.clientsStatus }}
      </p>
      <q-banner v-if="discovery.clientsStatus !== 'complete'" class="warning"
        >Visibility is incomplete. Unseen clients have unknown status.</q-banner
      >
      <h3>Clients and service accounts</h3>
      <q-list bordered separator>
        <q-expansion-item
          v-for="row in discovery.clients"
          :key="row.id"
          :label="row.clientId"
          :caption="
            [
              enabledLabel(row.enabled),
              row.serviceAccount ||
                (row.serviceAccountsEnabled === false
                  ? 'No service account'
                  : 'Service account details unavailable'),
            ].join(' · ')
          "
        >
          <div class="q-pa-md">
            <p>Client UUID: {{ row.id }}</p>
            <template v-if="row.rolesStatus !== 'not-applicable'">
              <p>Role inspection: {{ row.rolesStatus }}</p>
              <p>Direct roles: {{ roles(row.directRoles, row.rolesStatus) }}</p>
              <p>
                Effective roles (including composites):
                {{ roles(row.effectiveRoles, row.rolesStatus) }}
              </p>
            </template>
          </div>
        </q-expansion-item>
        <q-item v-if="!discovery.clients?.length"
          ><q-item-section>No clients observed.</q-item-section></q-item
        >
      </q-list>
      <h3>Fuse account matches</h3>
      <p>
        On an OAuth client account, set <code>keycloakIssuer</code> to this
        realm issuer and <code>keycloakClientId</code> to the exact client ID.
      </p>
      <q-markup-table flat bordered wrap-cells
        ><thead>
          <tr>
            <th>Client ID</th>
            <th>Fuse account</th>
            <th>Application</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="match in discovery.accounts" :key="match.accountId">
            <td>{{ match.clientId || "Unspecified" }}</td>
            <td>{{ match.accountId }}</td>
            <td>
              <router-link
                v-if="match.applicationId"
                :to="{
                  name: 'applicationEdit',
                  params: { id: match.applicationId },
                }"
                >{{ match.applicationName }}</router-link><span v-else>—</span>
            </td>
            <td>{{ match.status }}</td>
          </tr>
          <tr v-if="!discovery.accounts?.length">
            <td colspan="4">No OAuth accounts reference this realm.</td>
          </tr>
        </tbody>
      </q-markup-table>
    </section>
    <q-dialog v-model="dialog" :persistent="saving"
      ><q-card class="editor"
        ><q-form @submit="save">
          <q-card-section>
            <h2>{{ form.id ? "Edit" : "Add" }} Keycloak integration</h2>
            <q-input v-model="form.name" label="Name" :rules="[required]" />
            <q-input
              v-model="form.issuer"
              label="Realm issuer"
              hint="For example https://sso.example.org/realms/inventory"
              :rules="[required]"
            />
            <q-input
              v-model="form.clientId"
              label="Inspection client ID"
              :rules="[required]"
            />
            <q-input
              v-model="form.secret"
              label="Client secret"
              type="password"
              autocomplete="new-password"
              :rules="needsSecret ? [required] : []"
              :hint="needsSecret ? '' : 'Leave blank to keep the saved secret.'"
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
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useQuasar } from "quasar";
import {
  CreateKeycloakIntegration,
  UpdateKeycloakIntegration,
  type KeycloakDiscoveryResponse,
  type KeycloakIntegrationResponse,
  type KeycloakRole,
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
  store.hasPermission("keycloakintegrations:" + action);
const items = ref<KeycloakIntegrationResponse[]>([]),
  discovery = ref<KeycloakDiscoveryResponse>();
const busy = ref(false),
  saving = ref(false),
  dialog = ref(false);
const connection = ref(""),
  selectedName = ref(""),
  error = ref(""),
  formError = ref("");
const form = ref({ id: "", name: "", issuer: "", clientId: "", secret: "" });
const original = ref<KeycloakIntegrationResponse>();
const needsSecret = computed(
  () =>
    !original.value ||
    original.value.issuer !== form.value.issuer ||
    original.value.clientId !== form.value.clientId,
);
const required = (value: string) => !!value?.trim() || "Required";
const enabledLabel = (value?: boolean) =>
  value === true ? "Enabled" : value === false ? "Disabled" : "Unknown";
const roles = (values?: KeycloakRole[], status?: string) =>
  values
    ?.map(
      (r) => (r.clientId ? "Client " + r.clientId : "Realm") + " / " + r.name,
    )
    .join(", ") || (status === "complete" ? "None" : "None observed");
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
  items.value = await client.keycloakIntegrationAll(signal);
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
function inspect(item: KeycloakIntegrationResponse) {
  void run(async () => {
    selectedName.value = item.name || "";
    discovery.value = await client.keycloakIntegrationDiscovery(
      item.id!,
      signal,
    );
  });
}
function probe(item: KeycloakIntegrationResponse) {
  void run(async () => {
    const result = await client.keycloakIntegrationValidateSaved(
      item.id!,
      signal,
    );
    connection.value = item.name + " · " + result.status;
  });
}
function edit(item?: KeycloakIntegrationResponse) {
  original.value = item;
  formError.value = "";
  form.value = {
    id: item?.id || "",
    name: item?.name || "",
    issuer: item?.issuer || "",
    clientId: item?.clientId || "",
    secret: "",
  };
  dialog.value = true;
}
watch(dialog, (open) => {
  if (!open) form.value.secret = "";
});
async function save() {
  saving.value = true;
  formError.value = "";
  try {
    const { id, name, issuer, clientId, secret } = form.value;
    if (id)
      await client.keycloakIntegrationPUT(
        id,
        new UpdateKeycloakIntegration({
          id,
          name,
          issuer,
          clientId,
          clientSecret: secret || undefined,
        }),
        signal,
      );
    else
      await client.keycloakIntegrationPOST(
        new CreateKeycloakIntegration({
          name,
          issuer,
          clientId,
          clientSecret: secret,
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
function remove(item: KeycloakIntegrationResponse) {
  q.dialog({
    title: "Delete Keycloak integration?",
    message: "Remove " + item.name + " from Fuse?",
    cancel: true,
  }).onOk(() =>
    run(async () => {
      await client.keycloakIntegrationDELETE(item.id!, signal);
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
