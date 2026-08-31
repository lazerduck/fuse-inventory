<template>
  <div
    :class="[
      'scrum-poker-page',
      {
        'scrum-poker-page--dark': isDark,
      },
    ]"
  >
    <header class="scrum-header">
      <div class="brand-lockup">
        <h1>Scrum poker</h1>
        <span class="title-cards" aria-hidden="true">
          <span class="title-card title-card--back"></span>
          <span class="title-card title-card--front">
            <span class="title-card__rank"></span>
            <span class="title-card__suit">?</span>
          </span>
        </span>
      </div>
      <div v-if="session" class="room-pill">
        <span class="room-pill__label">ROOM</span>
        <strong>{{ session.roomCode }}</strong>
        <q-btn
          flat
          round
          dense
          icon="content_copy"
          aria-label="Copy room code"
          @click="copyRoomCode"
        >
          <q-tooltip>Copy room code</q-tooltip>
        </q-btn>
      </div>
    </header>

    <PlayerEntranceSplash
      :avatar="selectedAvatarColor"
      :player-name="participantName"
      :show="showEntranceSplash"
      @complete="showEntranceSplash = false"
    />

    <q-banner v-if="!featureEnabled" rounded class="banner-muted q-mb-lg">
      Scrum Poker is currently disabled in Fuse Settings.
    </q-banner>

    <q-card v-else-if="!session" flat bordered class="join-card">
      <q-card-section class="join-card__intro">
        <div class="welcome-icon"><q-icon name="groups" size="30px" /></div>
        <div>
          <div class="section-kicker">Collaborative estimation</div>
          <div class="join-title">Create or join a room</div>
          <div class="join-copy">
            {{
              hasRoomCode
                ? "Enter your details to join with the room code below."
                : "Choose a display name, then share the room with your team."
            }}
          </div>
        </div>
      </q-card-section>
      <q-card-section class="join-form">
        <q-input
          v-model="displayName"
          outlined
          label="Your display name"
          maxlength="50"
          counter
          @keyup.enter="submitRoomEntry"
        />
        <div class="avatar-picker q-mt-md">
          <div class="avatar-picker__label avatar-picker__label--images">
            Avatar
          </div>
          <div class="avatar-picker__options avatar-picker__options--images">
            <button
              v-for="avatar in scrumPokerAvatarImages"
              :key="avatar.value"
              type="button"
              class="avatar-picker__option avatar-picker__option--image"
              :class="{
                'avatar-picker__option--selected':
                  selectedAvatarColor === avatar.value,
              }"
              :style="scrumPokerAvatarImageStyle(avatar, true)"
              :aria-label="`Choose avatar ${avatar.index + 1}`"
              :aria-pressed="selectedAvatarColor === avatar.value"
              @click="selectedAvatarColor = avatar.value"
            >
              <q-icon
                v-if="selectedAvatarColor === avatar.value"
                name="check"
                size="16px"
                class="avatar-picker__image-check"
              />
            </button>
          </div>
        </div>
        <q-input
          v-model="joinCode"
          outlined
          label="Existing room code"
          class="q-mt-md"
          maxlength="20"
          @keyup.enter="submitRoomEntry"
        >
          <template v-if="hasRoomCode" #append>
            <q-btn
              flat
              round
              dense
              icon="close"
              aria-label="Clear room code"
              @click="clearRoomCode"
            >
              <q-tooltip>Clear room code</q-tooltip>
            </q-btn>
          </template>
        </q-input>
        <q-banner
          v-if="errorMessage"
          rounded
          dense
          class="banner-error q-mt-md"
          >{{ errorMessage }}</q-banner
        >
      </q-card-section>
      <q-card-actions class="join-actions">
        <q-btn
          class="join-submit-btn"
          size="lg"
          unelevated
          color="primary"
          :label="hasRoomCode ? 'Join room' : 'Create room'"
          :disable="!canSubmit"
          :loading="loading"
          @click="submitRoomEntry"
        />
        <div v-if="hasRoomCode" class="create-room-link-slot">
          <q-btn
            class="create-room-btn"
            flat
            dense
            color="primary"
            label="Create a new room instead"
            @click="createRoom"
          />
        </div>
      </q-card-actions>
    </q-card>

    <template v-else>
      <q-banner
        v-if="errorMessage"
        rounded
        dense
        class="banner-error q-mb-lg"
        >{{ errorMessage }}</q-banner
      >

      <div class="scrum-layout">
        <main class="voting-panel">
          <q-card flat bordered class="cards-card">
            <q-card-section class="voting-card-section">
              <div class="round-heading">
                <h2>Vote when ready</h2>
                <q-chip
                  class="phase-chip"
                  color="primary"
                  text-color="white"
                  :icon="
                    room?.phase === ScrumPokerPhase.Revealed
                      ? 'check_circle'
                      : 'schedule'
                  "
                  :label="
                    room?.phase === ScrumPokerPhase.Revealed
                      ? 'Revealed'
                      : 'Voting'
                  "
                />
              </div>
              <div class="card-grid">
                <q-btn
                  v-for="card in cards"
                  :key="card.value"
                  class="poker-card"
                  :class="{
                    'poker-card--selected': selectedCard === card.value,
                  }"
                  :disable="
                    (room?.phase === ScrumPokerPhase.Revealed &&
                      lockVotesAfterReveal) ||
                    actionLoading
                  "
                  @click="
                    selectCard(selectedCard === card.value ? null : card.value)
                  "
                >
                  <q-icon
                    v-if="card.value === ScrumPokerCard.Coffee"
                    name="coffee"
                    size="1.35em"
                  />
                  <span v-else>{{ card.label }}</span>
                </q-btn>
              </div>
            </q-card-section>
            <q-card-actions class="voting-actions">
              <div class="round-indicator">Round {{ room?.round ?? 1 }}</div>
              <div class="vote-help">
                {{ readyCount }} of {{ room?.participants?.length ?? 0 }} voted
              </div>
            </q-card-actions>
          </q-card>

          <section class="participants-panel">
            <div class="panel-heading">
              <div>
                <h3>
                  Participants
                  <span class="participant-total"
                    >{{ room?.participants?.length ?? 0 }} in room</span
                  >
                </h3>
              </div>
              <div v-if="isCurrentHost" class="action-row">
                <q-btn
                  flat
                  class="control-btn"
                  label="Reset"
                  icon="restart_alt"
                  :loading="actionLoading"
                  :disable="readyCount === 0"
                  @click="resetRound"
                />
                <q-btn
                  unelevated
                  color="primary"
                  :label="
                    room?.phase === ScrumPokerPhase.Revealed ? 'Hide' : 'Reveal'
                  "
                  :icon="
                    room?.phase === ScrumPokerPhase.Revealed
                      ? 'visibility_off'
                      : 'visibility'
                  "
                  :loading="actionLoading"
                  :disable="readyCount === 0"
                  @click="
                    room?.phase === ScrumPokerPhase.Revealed
                      ? hideCards()
                      : revealCards()
                  "
                />
              </div>
            </div>
            <q-list class="participant-list">
              <q-item
                v-for="participant in orderedParticipants"
                :key="participant.id"
              >
                <q-item-section avatar
                  ><q-avatar
                    class="participant-avatar"
                    :style="participantAvatarStyle(participant)"
                  >
                    <template v-if="!participantHasImage(participant)">
                      {{ participant.displayName?.charAt(0).toUpperCase() }}
                    </template>
                  </q-avatar></q-item-section
                >
                <q-item-section>
                  <q-item-label class="participant-name-label"
                    >{{ participant.displayName
                    }}<q-badge
                      v-if="
                        sameParticipantId(participant.id, currentParticipantId)
                      "
                      outline
                      color="primary"
                      label="You"
                      class="q-ml-sm"
                  /></q-item-label>
                  <q-item-label caption class="participant-role-label">
                    <span
                      :class="{
                        'participant-role-label--owner': sameParticipantId(
                          participant.id,
                          room?.ownerParticipantId,
                        ),
                      }"
                      >{{
                        sameParticipantId(
                          participant.id,
                          room?.ownerParticipantId,
                        )
                          ? "Owner"
                          : "Participant"
                      }}</span
                    >
                    <template
                      v-if="
                        sameParticipantId(
                          participant.id,
                          room?.currentHostParticipantId,
                        )
                      "
                    >
                      <span aria-hidden="true">-</span>
                      <span class="participant-role-label--host">Host</span>
                    </template>
                  </q-item-label>
                </q-item-section>
                <q-item-section side class="participant-management-slot">
                  <q-btn
                    v-if="
                      isCurrentHost && participant.id !== currentParticipantId
                    "
                    flat
                    round
                    dense
                    icon="swap_horiz"
                    class="participant-transfer-btn"
                    :aria-label="
                      isRoomOwner
                        ? 'Make participant owner'
                        : 'Make participant host'
                    "
                    @click="
                      transferHost(participant.id, participant.displayName)
                    "
                  >
                    <q-tooltip>
                      {{ isRoomOwner ? "Transfer ownership" : "Transfer host" }}
                    </q-tooltip>
                  </q-btn>
                  <q-btn
                    v-if="
                      isRoomOwner && participant.id !== currentParticipantId
                    "
                    flat
                    round
                    dense
                    icon="person_remove"
                    class="participant-remove-btn"
                    aria-label="Remove participant"
                    @click="
                      removeParticipant(participant.id, participant.displayName)
                    "
                  >
                    <q-tooltip>Remove participant</q-tooltip>
                  </q-btn>
                </q-item-section>
                <q-item-section side class="participant-status-slot">
                  <span
                    class="status-dot"
                    :class="{ 'status-dot--ready': participant.hasVoted }"
                  ></span>
                  <span class="participant-status-text">{{
                    participant.hasVoted ? "Voted" : "Thinking"
                  }}</span>
                </q-item-section>
                <q-item-section side class="participant-score-slot">
                  <Transition name="participant-score">
                    <span
                      v-if="participant.hasVoted"
                      class="participant-score-entry"
                    >
                      <span
                        class="participant-score-flip"
                        :class="{
                          'participant-score-flip--revealed':
                            room?.phase === ScrumPokerPhase.Revealed,
                        }"
                      >
                        <span
                          class="participant-score-face participant-score-face--question"
                        >
                          ?
                        </span>
                        <span
                          class="participant-card participant-score-face participant-score-face--value"
                        >
                          <q-icon
                            v-if="participant.card === ScrumPokerCard.Coffee"
                            name="coffee"
                            size="1.2em"
                          />
                          <span v-else>{{
                            participant.card !== undefined &&
                            participant.card !== null
                              ? cardLabel(participant.card)
                              : "?"
                          }}</span>
                        </span>
                      </span>
                    </span>
                  </Transition>
                  <div class="score-card-placeholder" aria-hidden="true"></div>
                </q-item-section>
              </q-item>
            </q-list>
            <div class="participant-summary-clip">
              <div class="participant-summary">
                <div>
                  <div class="summary-label">Spread</div>
                  <div class="summary-value">{{ spreadDisplay }}</div>
                </div>
                <div>
                  <div class="summary-label">Overall</div>
                  <div class="overall-score-slot">
                    <div
                      class="participant-score-flip overall-score-flip"
                      :class="{
                        'participant-score-flip--revealed':
                          room?.phase === ScrumPokerPhase.Revealed,
                      }"
                    >
                      <span
                        class="participant-score-face participant-score-face--question"
                      >
                        ?
                      </span>
                      <span
                        class="participant-card participant-score-face participant-score-face--value"
                        :style="averageScoreStyle"
                      >
                        {{ averageDisplay }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </section>
        </main>

        <aside class="details-column">
          <q-card flat bordered class="side-card">
            <q-card-section>
              <div class="side-card-header">
                <div class="side-title">Room</div>
                <q-btn
                  v-if="isCurrentHost"
                  flat
                  round
                  dense
                  icon="edit"
                  size="sm"
                  class="room-edit-btn"
                  aria-label="Room settings"
                  @click="editRoomName"
                />
              </div>
              <div class="side-room-name">
                {{ roomName || `Sprint planning round ${room?.round ?? 1}` }}
              </div>
              <div class="side-room-code">Code: {{ session?.roomCode }}</div>
              <q-btn
                outline
                class="full-width q-mt-md q-py-sm"
                icon="link"
                label="Copy invite link"
                @click="copyInviteLink"
              />
            </q-card-section>
          </q-card>

          <q-card v-if="isCurrentHost" flat bordered class="side-card">
            <q-card-section>
              <div class="side-title">Settings</div>
              <div class="settings-row">
                <div class="settings-label">
                  Auto-reveal when everyone's voted
                </div>
                <q-toggle
                  :model-value="autoReveal"
                  dense
                  :disable="autoRevealSaving"
                  @update:model-value="onAutoRevealChange"
                />
              </div>
              <div class="settings-row">
                <div class="settings-label">Lock votes after reveal</div>
                <q-toggle
                  :model-value="lockVotesAfterReveal"
                  dense
                  :disable="lockVotesAfterRevealSaving"
                  @update:model-value="onLockVotesAfterRevealChange"
                />
              </div>
            </q-card-section>
          </q-card>

          <q-card flat bordered class="side-card">
            <q-card-section>
              <div class="side-title">Deck legend</div>
              <div class="legend-row">
                <strong>?</strong><span>Not enough info to estimate</span>
              </div>
              <div class="legend-row">
                <q-icon name="coffee" size="1.2em" /><span
                  >Suggest a break</span
                >
              </div>
            </q-card-section>
          </q-card>

          <q-btn
            flat
            class="leave-room-btn q-py-sm"
            icon="logout"
            label="Leave room"
            @click="leaveRoom"
          />
        </aside>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  watch,
} from "vue";
import { useRoute, useRouter } from "vue-router";
import { Dialog, Notify, useQuasar } from "quasar";
import {
  ScrumPokerCard,
  ScrumPokerPhase,
  ScrumPokerRemoveParticipantRequest,
  ScrumPokerRoomResponse,
  ScrumPokerSessionResponse,
  ScrumPokerTransferHostRequest,
  ScrumPokerTransferOwnershipRequest,
  ApiException,
} from "api/client";
import { useFuseStore } from "../stores/FuseStore";
import { useFuseClient } from "../composables/useFuseClient";
import PlayerEntranceSplash from "../components/PlayerEntranceSplash.vue";
import {
  scrumPokerAvatarImageStyle,
  scrumPokerAvatarImages,
} from "../utils/scrumPokerAvatar";

const route = useRoute();
const router = useRouter();
const fuseStore = useFuseStore();
const client = useFuseClient();
const $q = useQuasar();
const displayName = ref("");
const selectedAvatarColor = ref<string | null>(null);
const joinCode = ref("");
const session = ref<ScrumPokerSessionResponse | null>(null);
const room = ref<ScrumPokerRoomResponse | null>(null);
const participantName = ref("");
const showEntranceSplash = ref(false);
const loading = ref(false);
const actionLoading = ref(false);
const autoReveal = ref(false);
const autoRevealSaving = ref(false);
const lockVotesAfterReveal = ref(false);
const lockVotesAfterRevealSaving = ref(false);
const roomName = ref("");
const errorMessage = ref("");
const selectedCard = ref<ScrumPokerCard | null>(null);
let pollTimer: ReturnType<typeof setInterval> | undefined;
let ownerRecoveryInFlight = false;

const featureEnabled = computed(
  () => fuseStore.appSettings?.scrumPokerEnabled === true,
);
const isDark = computed(() => $q.dark.isActive);
const canSubmit = computed(
  () =>
    displayName.value.trim().length > 0 &&
    displayName.value.trim().length <= 50 &&
    selectedAvatarColor.value !== null,
);
const hasRoomCode = computed(() => joinCode.value.trim().length > 0);
const currentParticipantId = computed(() => session.value?.participantId);
const isRoomOwner = computed(
  () =>
    Boolean(session.value?.ownerToken) &&
    sameParticipantId(
      currentParticipantId.value,
      room.value?.ownerParticipantId,
    ),
);
const isCurrentHost = computed(() =>
  sameParticipantId(
    currentParticipantId.value,
    room.value?.currentHostParticipantId,
  ),
);
const orderedParticipants = computed(() => {
  const participants = room.value?.participants ?? [];
  const owner = participants.find((participant) =>
    sameParticipantId(participant.id, room.value?.ownerParticipantId),
  );
  if (!owner) return participants;
  return [
    owner,
    ...participants.filter((participant) => participant !== owner),
  ];
});
const roomAutoReveal = computed(() => room.value?.autoReveal === true);
const roomLockVotesAfterReveal = computed(
  () => room.value?.lockVotesAfterReveal === true,
);
const readyCount = computed(
  () => (room.value?.participants ?? []).filter((p) => p.hasVoted).length,
);
const numericRevealedCards = computed(() =>
  (room.value?.participants ?? [])
    .map((p) => p.card)
    .filter(
      (card): card is ScrumPokerCard => card !== null && card !== undefined,
    )
    .map((card) => cardValue(card))
    .filter((value): value is number => value !== null),
);
const averageDisplay = computed(() =>
  room.value?.phase === ScrumPokerPhase.Revealed &&
  room.value.average !== undefined &&
  room.value.average !== null
    ? nearestDeckValue(room.value.average).toString()
    : "?",
);
const averageScoreStyle = computed(() => {
  if (
    room.value?.phase !== ScrumPokerPhase.Revealed ||
    room.value.average === undefined ||
    room.value.average === null
  )
    return {};

  const score = nearestDeckValue(room.value.average);
  const colors =
    averageHeatColors.find((color) => score <= color.maximum) ??
    averageHeatColors[averageHeatColors.length - 1]!;
  return {
    background: themedSurfaceColor(colors.background, 30),
    color: isDark.value ? colors.darkForeground : colors.foreground,
  };
});
const spreadDisplay = computed(() => {
  if (
    room.value?.phase !== ScrumPokerPhase.Revealed ||
    numericRevealedCards.value.length === 0
  )
    return "—";
  const min = Math.min(...numericRevealedCards.value);
  const max = Math.max(...numericRevealedCards.value);
  return min === max ? "0" : `${min}-${max}`;
});
const cards = [
  { value: ScrumPokerCard.Zero, label: "0" },
  { value: ScrumPokerCard.Half, label: "½" },
  { value: ScrumPokerCard.One, label: "1" },
  { value: ScrumPokerCard.Two, label: "2" },
  { value: ScrumPokerCard.Three, label: "3" },
  { value: ScrumPokerCard.Five, label: "5" },
  { value: ScrumPokerCard.Eight, label: "8" },
  { value: ScrumPokerCard.Thirteen, label: "13" },
  { value: ScrumPokerCard.Twenty, label: "20" },
  { value: ScrumPokerCard.Forty, label: "40" },
  { value: ScrumPokerCard.Hundred, label: "100" },
  { value: ScrumPokerCard.Question, label: "?" },
  { value: ScrumPokerCard.Coffee, label: "Coffee" },
];
const numericDeckValues = [0, 0.5, 1, 2, 3, 5, 8, 13, 20, 40, 100];
const averageHeatColors = [
  {
    maximum: 3,
    background: "#d8f0df",
    foreground: "#2e7047",
    darkForeground: "#b9f2c8",
  },
  {
    maximum: 5,
    background: "#e4f0c7",
    foreground: "#52701f",
    darkForeground: "#e5f5a8",
  },
  {
    maximum: 8,
    background: "#f6edc8",
    foreground: "#78621c",
    darkForeground: "#fff0a8",
  },
  {
    maximum: 13,
    background: "#f8dfc2",
    foreground: "#8a5422",
    darkForeground: "#ffd29c",
  },
  {
    maximum: 20,
    background: "#f4c6a8",
    foreground: "#98451f",
    darkForeground: "#ffb58e",
  },
  {
    maximum: 40,
    background: "#ee9b8f",
    foreground: "#8d2924",
    darkForeground: "#ffaaa0",
  },
  {
    maximum: 100,
    background: "#9f302f",
    foreground: "#fff5f2",
    darkForeground: "#fff5f2",
  },
];
function cardLabel(card: ScrumPokerCard) {
  return cards.find((option) => option.value === card)?.label ?? card;
}
function nearestDeckValue(average: number) {
  return numericDeckValues.reduce((closest, value) => {
    const distance = Math.abs(value - average);
    const closestDistance = Math.abs(closest - average);
    return distance <= closestDistance ? value : closest;
  });
}
function participantAvatarStyle(participant: {
  id?: unknown;
  displayName?: string | null;
  avatarColor?: string | null;
}) {
  const selectedImage = scrumPokerAvatarImages.find(
    (avatar) => avatar.value === participant.avatarColor,
  );
  if (selectedImage) return scrumPokerAvatarImageStyle(selectedImage, false);

  if (sameParticipantId(participant.id, currentParticipantId.value)) {
    const currentImage = scrumPokerAvatarImages.find(
      (avatar) => avatar.value === selectedAvatarColor.value,
    );
    if (currentImage) return scrumPokerAvatarImageStyle(currentImage, false);
  }

  return {};
}
function participantHasImage(participant: {
  id?: unknown;
  avatarColor?: string | null;
}) {
  return (
    scrumPokerAvatarImages.some(
      (avatar) => avatar.value === participant.avatarColor,
    ) ||
    (sameParticipantId(participant.id, currentParticipantId.value) &&
      scrumPokerAvatarImages.some(
        (avatar) => avatar.value === selectedAvatarColor.value,
      ))
  );
}
function sameParticipantId(left?: unknown, right?: unknown) {
  return (
    typeof left === "string" &&
    typeof right === "string" &&
    left.localeCompare(right, undefined, { sensitivity: "accent" }) === 0
  );
}
function themedSurfaceColor(color: string, darkOpacity: number) {
  if (!isDark.value) return color;

  const red = Number.parseInt(color.slice(1, 3), 16);
  const green = Number.parseInt(color.slice(3, 5), 16);
  const blue = Number.parseInt(color.slice(5, 7), 16);
  return `rgba(${red}, ${green}, ${blue}, ${darkOpacity / 100})`;
}
function cardValue(card: ScrumPokerCard): number | null {
  if (card === ScrumPokerCard.Zero) return 0;
  if (card === ScrumPokerCard.Half) return 0.5;
  if (card === ScrumPokerCard.One) return 1;
  if (card === ScrumPokerCard.Two) return 2;
  if (card === ScrumPokerCard.Three) return 3;
  if (card === ScrumPokerCard.Five) return 5;
  if (card === ScrumPokerCard.Eight) return 8;
  if (card === ScrumPokerCard.Thirteen) return 13;
  if (card === ScrumPokerCard.Twenty) return 20;
  if (card === ScrumPokerCard.Forty) return 40;
  if (card === ScrumPokerCard.Hundred) return 100;
  return null;
}
function storageKey(code: string) {
  return `fuse:scrum-poker:${code}`;
}
const ownerTokensStorageKey = "fuse:scrum-poker-owner-tokens";
const legacyOwnerTokenStoragePrefix = "fuse:scrum-poker-owner:";
const ownerTokenLifetimeMs = 60 * 24 * 60 * 60 * 1000;
const leaveRequestSent = ref(false);
function clearLegacyParticipantIdentityStorage() {
  const prefix = "fuse:scrum-poker-identity:";
  for (const storage of [localStorage, sessionStorage]) {
    for (let index = storage.length - 1; index >= 0; index -= 1) {
      const key = storage.key(index);
      if (key?.startsWith(prefix)) storage.removeItem(key);
    }
  }
}
function apiUrl(path: string) {
  return `${import.meta.env.VITE_API_BASE_URL ?? ""}${path}`;
}
function normalizedRoomCode(code: string) {
  return code.trim().toUpperCase();
}
function readOwnerTokens() {
  const now = Date.now();
  const stored = localStorage.getItem(ownerTokensStorageKey);
  let tokens: Record<string, { token: string; expiresAt: number }> = {};
  if (stored) {
    try {
      tokens = JSON.parse(stored);
    } catch {
      localStorage.removeItem(ownerTokensStorageKey);
    }
  }

  let changed = false;
  for (const [roomCode, entry] of Object.entries(tokens)) {
    if (
      !entry?.token ||
      typeof entry.expiresAt !== "number" ||
      entry.expiresAt <= now
    ) {
      delete tokens[roomCode];
      changed = true;
    }
  }

  if (changed)
    localStorage.setItem(ownerTokensStorageKey, JSON.stringify(tokens));
  return tokens;
}
function storeOwnerToken(code: string, token: string) {
  const tokens = readOwnerTokens();
  tokens[normalizedRoomCode(code)] = {
    token,
    expiresAt: Date.now() + ownerTokenLifetimeMs,
  };
  localStorage.setItem(ownerTokensStorageKey, JSON.stringify(tokens));
}
function removeOwnerToken(code: string) {
  const tokens = readOwnerTokens();
  delete tokens[normalizedRoomCode(code)];
  localStorage.setItem(ownerTokensStorageKey, JSON.stringify(tokens));
}
function removeOwnerTokenIfMatches(code: string, token: string) {
  if (storedOwnerToken(code) === token) removeOwnerToken(code);
}
function storedOwnerToken(code: string) {
  return readOwnerTokens()[normalizedRoomCode(code)]?.token;
}
function migrateLegacyOwnerTokens() {
  const tokens = readOwnerTokens();
  let changed = false;
  for (let index = localStorage.length - 1; index >= 0; index -= 1) {
    const key = localStorage.key(index);
    if (!key?.startsWith(legacyOwnerTokenStoragePrefix)) continue;
    const roomCode = key.slice(legacyOwnerTokenStoragePrefix.length);
    const token = localStorage.getItem(key);
    if (roomCode && token && !tokens[normalizedRoomCode(roomCode)]) {
      tokens[normalizedRoomCode(roomCode)] = {
        token,
        expiresAt: Date.now() + ownerTokenLifetimeMs,
      };
      changed = true;
    }
    localStorage.removeItem(key);
  }
  if (changed)
    localStorage.setItem(ownerTokensStorageKey, JSON.stringify(tokens));
}

function sessionActionError(error: unknown, fallback: string) {
  if (error instanceof ApiException) {
    try {
      const payload = JSON.parse(error.response) as { error?: string };
      if (payload.error) return payload.error;
    } catch {
      // Use the generated API error when the response is not JSON.
    }
  }

  return error instanceof Error ? error.message : fallback;
}

function editRoomName() {
  if (!isRoomOwner.value) return;

  Dialog.create({
    title: "Edit room name",
    color: "primary",
    prompt: {
      model: roomName.value || `Sprint ${room.value?.round ?? 1} planning`,
      type: "text",
      color: "primary",
      maxlength: 80,
    },
    ok: {
      color: "primary",
      unelevated: true,
      label: "Save",
    },
    cancel: {
      label: "Cancel",
      color: "primary",
      flat: true,
    },
    persistent: true,
  }).onOk((name: string) => {
    const trimmedName = name.trim();
    if (trimmedName) roomName.value = trimmedName;
  });
}

async function createRoom() {
  if (!canSubmit.value) {
    errorMessage.value =
      "Enter a display name and choose an avatar before creating a room.";
    return;
  }
  joinCode.value = "";
  await runSessionAction(() =>
    client.scrumPokerRoomsPOST({
      displayName: displayName.value.trim(),
      avatarColor: selectedAvatarColor.value!,
    } as any),
  );
}

async function joinRoom() {
  if (!canSubmit.value || !joinCode.value.trim()) return;
  const code = joinCode.value.trim().toUpperCase();
  await runSessionAction(() =>
    client.scrumPokerRoomsEnter(code, {
      displayName: displayName.value.trim(),
      ownerToken: storedOwnerToken(code),
      avatarColor: selectedAvatarColor.value!,
    } as any),
  );
}

function clearRoomCode() {
  joinCode.value = "";
  errorMessage.value = "";
}

function submitRoomEntry() {
  if (hasRoomCode.value) {
    void joinRoom();
  } else {
    void createRoom();
  }
}

async function runSessionAction(
  action: () => Promise<ScrumPokerSessionResponse>,
) {
  loading.value = true;
  errorMessage.value = "";
  try {
    const result = await action();
    leaveRequestSent.value = false;
    session.value = result;
    room.value = result.room ?? null;
    participantName.value = displayName.value.trim();
    if (
      !scrumPokerAvatarImages.some(
        (avatar) => avatar.value === selectedAvatarColor.value,
      )
    ) {
      selectedAvatarColor.value =
        currentAvatarColor() ?? selectedAvatarColor.value;
    }
    selectedCard.value = currentCard();
    if (result.roomCode && result.ownerToken)
      storeOwnerToken(result.roomCode, result.ownerToken);
    if (result.roomCode && result.participantToken)
      sessionStorage.setItem(
        storageKey(result.roomCode),
        JSON.stringify({
          session: result,
          participantName: participantName.value,
        }),
      );
    await router.replace({
      name: "scrumPokerRoom",
      params: { roomCode: result.roomCode },
    });
    startPolling();
    await nextTick();
    showEntranceSplash.value = true;
  } catch (error) {
    errorMessage.value = sessionActionError(error, "Unable to join the room.");
  } finally {
    loading.value = false;
  }
}

function currentCard() {
  const me = session.value?.room?.participants?.find(
    (p) => p.id === session.value?.participantId,
  );
  return me?.card ?? null;
}

function currentAvatarColor() {
  const me = session.value?.room?.participants?.find(
    (p) => p.id === session.value?.participantId,
  );
  return me?.avatarColor;
}

async function refreshRoom() {
  if (!session.value?.roomCode || !session.value.participantToken) return;
  try {
    const previousHostId = room.value?.currentHostParticipantId;
    const result = await client.scrumPokerState(
      session.value.roomCode,
      session.value.participantToken,
    );
    room.value = result;
    const currentSession = session.value;
    if (
      sameParticipantId(
        currentSession.participantId,
        result.ownerParticipantId,
      ) &&
      !currentSession.ownerToken &&
      !ownerRecoveryInFlight
    ) {
      ownerRecoveryInFlight = true;
      try {
        const ownerSession = await client.scrumPokerRoomsEnter(
          currentSession.roomCode!,
          {
            displayName: participantName.value || displayName.value.trim(),
            participantToken: currentSession.participantToken,
          } as any,
        );
        session.value = ownerSession;
        room.value = ownerSession.room ?? result;
        if (ownerSession.roomCode && ownerSession.ownerToken)
          storeOwnerToken(ownerSession.roomCode, ownerSession.ownerToken);
        if (ownerSession.roomCode && ownerSession.participantToken)
          sessionStorage.setItem(
            storageKey(ownerSession.roomCode),
            JSON.stringify({
              session: ownerSession,
              participantName: participantName.value,
            }),
          );
      } finally {
        ownerRecoveryInFlight = false;
      }
    } else if (
      currentSession.ownerToken &&
      !sameParticipantId(
        currentSession.participantId,
        result.ownerParticipantId,
      )
    ) {
      const previousOwnerToken = currentSession.ownerToken;
      currentSession.ownerToken = undefined;
      removeOwnerTokenIfMatches(currentSession.roomCode!, previousOwnerToken);
      sessionStorage.setItem(
        storageKey(currentSession.roomCode!),
        JSON.stringify({
          session: currentSession,
          participantName: participantName.value,
        }),
      );
    }
    const nextHost = result.participants?.find(
      (participant) => participant.id === result.currentHostParticipantId,
    );
    if (previousHostId && nextHost?.id && previousHostId !== nextHost.id) {
      Notify.create({
        message:
          nextHost.id === currentParticipantId.value
            ? "You are now the host."
            : `${nextHost.displayName ?? "A participant"} is now the host.`,
        color: "positive",
      });
    }
    const me = result.participants?.find(
      (p) => p.id === currentParticipantId.value,
    );
    selectedCard.value = me?.card ?? null;
  } catch (error) {
    if (isInvalidSessionError(error)) {
      try {
        const result = await client.scrumPokerRoomsEnter(
          session.value.roomCode!,
          {
            displayName: participantName.value || displayName.value.trim(),
            participantToken: session.value.participantToken,
          } as any,
        );
        session.value = result;
        room.value = result.room ?? null;
        startPolling();
        return;
      } catch {
        // Clear the local session when the token can no longer rejoin the room.
      }
      const roomCode = session.value.roomCode!;
      stopPolling();
      sessionStorage.removeItem(storageKey(roomCode));
      session.value = null;
      room.value = null;
      selectedCard.value = null;
      joinCode.value = roomCode;
      errorMessage.value = "";
      return;
    }

    errorMessage.value =
      error instanceof Error
        ? error.message
        : "The room is no longer available.";
    stopPolling();
  }
}

function isInvalidSessionError(error: unknown) {
  if (!error || typeof error !== "object") return false;
  const status = (error as { status?: number }).status;
  return status === 401 || status === 404;
}

function startPolling() {
  stopPolling();
  void refreshRoom();
  pollTimer = setInterval(() => void refreshRoom(), 1000);
}
function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = undefined;
  }
}

async function selectCard(card: ScrumPokerCard | null) {
  if (!session.value) return;
  actionLoading.value = true;
  errorMessage.value = "";
  try {
    room.value = await client.scrumPokerCardPUT(session.value.roomCode!, {
      participantToken: session.value.participantToken,
      card,
    } as any);
    selectedCard.value = card;
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : "Unable to select that card.";
  } finally {
    actionLoading.value = false;
  }
}
async function revealCards() {
  if (!isCurrentHost.value || readyCount.value === 0) return;
  await roomAction(() =>
    client.scrumPokerReveal(session.value!.roomCode!, {
      participantToken: session.value!.participantToken,
    } as any),
  );
}
async function hideCards() {
  if (!isCurrentHost.value || readyCount.value === 0) return;
  await roomAction(() =>
    client.scrumPokerHide(session.value!.roomCode!, {
      participantToken: session.value!.participantToken,
    } as any),
  );
}
async function resetRound() {
  if (!isCurrentHost.value || readyCount.value === 0) return;
  await roomAction(() =>
    client.scrumPokerReset(session.value!.roomCode!, {
      participantToken: session.value!.participantToken,
    } as any),
  );
  selectedCard.value = null;
}
async function roomAction(action: () => Promise<ScrumPokerRoomResponse>) {
  actionLoading.value = true;
  errorMessage.value = "";
  try {
    room.value = await action();
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : "Unable to update the room.";
  } finally {
    actionLoading.value = false;
  }
}
async function copyRoomCode() {
  if (session.value?.roomCode) {
    await navigator.clipboard?.writeText(session.value.roomCode);
    Notify.create({ message: "Room code copied", color: "positive" });
  }
}
async function copyInviteLink() {
  if (!session.value?.roomCode) return;
  const inviteUrl = `${window.location.origin}/scrum-poker/${session.value.roomCode}`;
  await navigator.clipboard?.writeText(inviteUrl);
  Notify.create({ message: "Invite link copied", color: "positive" });
}

function transferHost(participantId?: string, displayName?: string) {
  if (!isCurrentHost.value || !participantId || !session.value?.roomCode)
    return;

  Dialog.create({
    title: isRoomOwner.value ? "Transfer ownership?" : "Transfer host?",
    message: `Make ${displayName || "this participant"} the ${
      isRoomOwner.value ? "owner" : "host"
    }?`,
    cancel: {
      label: "Cancel",
      color: "primary",
      flat: true,
    },
    ok: { color: "primary" },
    persistent: true,
  }).onOk(async () => {
    actionLoading.value = true;
    errorMessage.value = "";
    try {
      room.value = isRoomOwner.value
        ? await client.scrumPokerTransferOwnership(
            session.value!.roomCode!,
            new ScrumPokerTransferOwnershipRequest({
              ownerToken: session.value!.ownerToken,
              participantId,
            }),
          )
        : await client.scrumPokerTransferHost(
            session.value!.roomCode!,
            new ScrumPokerTransferHostRequest({
              participantToken: session.value!.participantToken,
              participantId,
            }),
          );
    } catch (error) {
      errorMessage.value =
        error instanceof Error
          ? error.message
          : isRoomOwner.value
            ? "Unable to transfer ownership."
            : "Unable to transfer host control.";
    } finally {
      actionLoading.value = false;
    }
  });
}

function removeParticipant(participantId?: string, displayName?: string) {
  if (!isRoomOwner.value || !participantId || !session.value?.roomCode) return;

  Dialog.create({
    title: "Remove participant?",
    message: `Remove ${displayName || "this participant"} from the room?`,
    ok: {
      color: "primary",
      unelevated: true,
    },
    cancel: {
      label: "Cancel",
      color: "primary",
      flat: true,
    },
    persistent: true,
  }).onOk(async () => {
    actionLoading.value = true;
    errorMessage.value = "";
    try {
      room.value = await client.scrumPokerRemoveParticipant(
        session.value!.roomCode!,
        new ScrumPokerRemoveParticipantRequest({
          ownerToken: session.value!.ownerToken,
          participantId,
        }),
      );
    } catch (error) {
      errorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to remove participant.";
    } finally {
      actionLoading.value = false;
    }
  });
}

async function updateAutoReveal(enabled: boolean) {
  if (
    !session.value?.roomCode ||
    !session.value.participantToken ||
    autoRevealSaving.value
  )
    return;

  if (enabled === roomAutoReveal.value) return;

  autoRevealSaving.value = true;
  errorMessage.value = "";
  try {
    room.value = await setAutoRevealOnServer(
      session.value.roomCode,
      session.value.participantToken,
      enabled,
    );
  } catch (error) {
    const message =
      error instanceof Error
        ? error.message
        : "Unable to update auto-reveal setting.";
    errorMessage.value = message;
    Notify.create({ type: "negative", message });
  } finally {
    autoRevealSaving.value = false;
  }
}

function onAutoRevealChange(enabled: boolean) {
  void updateAutoReveal(enabled);
}

async function updateLockVotesAfterReveal(enabled: boolean) {
  if (
    !session.value?.roomCode ||
    !session.value.participantToken ||
    lockVotesAfterRevealSaving.value
  )
    return;

  if (enabled === roomLockVotesAfterReveal.value) return;

  lockVotesAfterRevealSaving.value = true;
  errorMessage.value = "";
  try {
    room.value = await setLockVotesAfterRevealOnServer(
      session.value.roomCode,
      session.value.participantToken,
      enabled,
    );
  } catch (error) {
    const message =
      error instanceof Error
        ? error.message
        : "Unable to update vote lock setting.";
    errorMessage.value = message;
    Notify.create({ type: "negative", message });
  } finally {
    lockVotesAfterRevealSaving.value = false;
  }
}

function onLockVotesAfterRevealChange(enabled: boolean) {
  void updateLockVotesAfterReveal(enabled);
}

async function setAutoRevealOnServer(
  roomCode: string,
  participantToken: string,
  enabled: boolean,
): Promise<ScrumPokerRoomResponse> {
  const response = await fetch(
    apiUrl(
      `/api/scrum-poker/rooms/${encodeURIComponent(roomCode)}/settings/auto-reveal`,
    ),
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ participantToken, enabled }),
    },
  );

  if (!response.ok) {
    let message = "Unable to update auto-reveal setting.";
    try {
      const payload = await response.json();
      if (payload?.error) message = payload.error;
    } catch {
      // Keep the generic message when the error payload is not JSON.
    }

    throw new Error(message);
  }

  const payload = await response.json();
  return ScrumPokerRoomResponse.fromJS(payload);
}

async function setLockVotesAfterRevealOnServer(
  roomCode: string,
  participantToken: string,
  enabled: boolean,
): Promise<ScrumPokerRoomResponse> {
  const response = await fetch(
    apiUrl(
      `/api/scrum-poker/rooms/${encodeURIComponent(roomCode)}/settings/lock-votes-after-reveal`,
    ),
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ participantToken, enabled }),
    },
  );

  if (!response.ok) {
    let message = "Unable to update vote lock setting.";
    try {
      const payload = await response.json();
      if (payload?.error) message = payload.error;
    } catch {
      // Keep the generic message when the error payload is not JSON.
    }

    throw new Error(message);
  }

  const payload = await response.json();
  return ScrumPokerRoomResponse.fromJS(payload);
}

async function leaveRoom() {
  const currentSession = session.value;

  // Fetch a fresh owner token if ownership was transferred to us since the last poll.
  if (
    currentSession?.roomCode &&
    currentSession.participantToken &&
    !currentSession.ownerToken
  ) {
    try {
      const fresh = await client.scrumPokerState(
        currentSession.roomCode,
        currentSession.participantToken,
      );
      if (
        sameParticipantId(
          currentSession.participantId,
          fresh.ownerParticipantId,
        )
      ) {
        const ownerSession = await client.scrumPokerRoomsEnter(
          currentSession.roomCode,
          {
            displayName: participantName.value || displayName.value.trim(),
            participantToken: currentSession.participantToken,
          } as any,
        );
        if (ownerSession.ownerToken)
          storeOwnerToken(currentSession.roomCode, ownerSession.ownerToken);
      }
    } catch {
      // Non-critical — proceed with leave.
    }
  }

  if (currentSession?.roomCode) joinCode.value = currentSession.roomCode;
  stopPolling();
  if (currentSession?.roomCode && currentSession.participantToken) {
    leaveRequestSent.value = true;
    try {
      await client.scrumPokerLeave(currentSession.roomCode, {
        participantToken: currentSession.participantToken,
      } as any);
    } catch {
      /* The local session should still be cleared if the room has already expired. */
    }
    sessionStorage.removeItem(storageKey(currentSession.roomCode));
  }
  session.value = null;
  room.value = null;
  selectedCard.value = null;
  participantName.value = "";
  await router.replace({ name: "scrumPoker" });
}

function leaveRoomOnPageHide(event: PageTransitionEvent) {
  if (event.persisted || leaveRequestSent.value) return;

  const currentSession = session.value;
  if (!currentSession?.roomCode || !currentSession.participantToken) return;

  leaveRequestSent.value = true;
  const body = new Blob(
    [JSON.stringify({ participantToken: currentSession.participantToken })],
    { type: "application/json" },
  );
  const endpoint = apiUrl(
    `/api/scrum-poker/rooms/${encodeURIComponent(currentSession.roomCode)}/leave`,
  );
  if (!navigator.sendBeacon(endpoint, body)) {
    void fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        participantToken: currentSession.participantToken,
      }),
      keepalive: true,
    }).catch(() => undefined);
  }
}

async function loadStoredSession() {
  if (!featureEnabled.value) return;
  const code = route.params.roomCode as string | undefined;
  if (!code) return;
  const stored = sessionStorage.getItem(storageKey(code));
  if (!stored) {
    joinCode.value = normalizedRoomCode(code);
    return;
  }
  try {
    const saved = JSON.parse(stored);
    session.value = saved.session ?? saved;
    participantName.value = saved.participantName ?? "";
    displayName.value = participantName.value;
    room.value = session.value?.room ?? null;
    selectedAvatarColor.value =
      currentAvatarColor() ?? selectedAvatarColor.value;
    startPolling();
  } catch {
    sessionStorage.removeItem(storageKey(code));
  }
}

onMounted(async () => {
  window.addEventListener("pagehide", leaveRoomOnPageHide);
  clearLegacyParticipantIdentityStorage();
  migrateLegacyOwnerTokens();
  await fuseStore.fetchStatus();
  await loadStoredSession();
});
watch(
  () => route.params.roomCode,
  () => {
    if (!session.value) {
      void loadStoredSession();
    }
  },
);
watch(joinCode, (code) => {
  if (!code.trim()) errorMessage.value = "";
});
watch(
  roomAutoReveal,
  (enabled) => {
    autoReveal.value = enabled;
  },
  { immediate: true },
);
watch(
  roomLockVotesAfterReveal,
  (enabled) => {
    lockVotesAfterReveal.value = enabled;
  },
  { immediate: true },
);
onBeforeUnmount(() => {
  window.removeEventListener("pagehide", leaveRoomOnPageHide);
  stopPolling();
});
</script>

<style scoped>
@import "../styles/pages.css";
.scrum-poker-page {
  --sp-page-bg: var(--fuse-page-bg);
  --sp-surface: var(--fuse-card-bg);
  --sp-text: inherit;
  --sp-muted: var(--fuse-text-muted);
  --sp-border: var(--fuse-panel-border);
  --sp-soft: var(--fuse-panel-bg);
  --sp-strong: var(--q-primary);
  --sp-strong-soft: color-mix(in srgb, var(--q-primary) 16%, var(--sp-surface));
  --sp-shadow: var(--fuse-shadow-1);

  box-sizing: border-box;
  min-height: 100vh;
  width: 100%;
  padding: 1.7rem clamp(0.8rem, 3vw, 2.2rem);
  background: var(--sp-page-bg);
  color: var(--sp-text);
}

.scrum-poker-page--dark {
  --sp-page-bg: var(--fuse-page-bg);
  --sp-surface: var(--fuse-card-bg);
  --sp-text: inherit;
  --sp-muted: var(--fuse-text-muted);
  --sp-border: var(--fuse-panel-border);
  --sp-soft: var(--fuse-panel-bg);
  --sp-strong: #9dccff;
  --sp-strong-soft: rgba(33, 150, 243, 0.3);
  --sp-shadow: var(--fuse-shadow-1);
}

.scrum-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  max-width: 1260px;
  margin: 0 auto 1.1rem;
}

.brand-lockup {
  display: flex;
  align-items: center;
  gap: 0.8rem;
}

.title-cards {
  position: relative;
  display: block;
  width: 2.15rem;
  height: 2.35rem;
  transform: rotate(8deg);
}

.title-card {
  position: absolute;
  display: flex;
  width: 1.35rem;
  height: 1.9rem;
  border: 2px solid var(--sp-text);
  border-radius: 0.25rem;
  background: var(--sp-surface);
  box-shadow: 0 2px 4px rgba(22, 40, 68, 0.2);
}

.title-card--back {
  top: 0.25rem;
  left: 0;
  border-color: var(--sp-strong);
  background: var(--sp-strong);
  transform: rotate(-16deg);
}

.title-card--front {
  top: 0;
  right: 0;
  align-items: center;
  justify-content: center;
  color: var(--sp-strong);
  transform: rotate(8deg);
}

.title-card__rank {
  position: absolute;
  top: 0.1rem;
  left: 0.18rem;
  font-size: 0.55rem;
  font-weight: 800;
}

.title-card__suit {
  font-size: 1rem;
  line-height: 1;
}

.scrum-header h1 {
  margin: 0;
  font-size: 2.2rem;
  font-weight: 650;
  letter-spacing: -0.01em;
}

.section-kicker {
  color: var(--sp-muted);
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.room-pill {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  padding: 0.25rem 0.35rem 0.25rem 0.8rem;
  border: 1px solid var(--sp-border);
  border-radius: 999px;
  background: var(--sp-surface);
  color: var(--sp-text);
  font-size: 0.9rem;
  box-shadow: var(--sp-shadow);
}

.room-pill__label {
  color: var(--sp-muted);
  font-size: 0.63rem;
  font-weight: 700;
  letter-spacing: 0.1em;
}

.join-card {
  width: min(100%, 710px);
  margin: 2.5rem auto;
  border-color: var(--sp-border);
  border-radius: 18px;
  box-shadow: var(--sp-shadow);
  background: var(--sp-surface);
}

.join-card__intro {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 2rem 2rem 1rem;
}

.welcome-icon {
  width: 54px;
  height: 54px;
  border-radius: 16px;
  display: grid;
  place-items: center;
  color: #fff;
  background: var(--sp-strong);
  box-shadow: 0 8px 18px rgba(33, 93, 175, 0.26);
}

.join-title {
  margin-top: 0.25rem;
  font-size: 1.35rem;
  font-weight: 700;
}

.join-copy {
  margin-top: 0.3rem;
  color: var(--sp-muted);
  font-size: 0.9rem;
}

.join-form {
  padding: 1rem 2rem 1.5rem;
}

.avatar-picker__label {
  margin-bottom: 0.45rem;
  color: var(--sp-muted);
  font-size: 0.8rem;
  font-weight: 600;
}

.avatar-picker__options {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  flex-wrap: wrap;
  margin-bottom: 2rem;
}

.avatar-picker__label--images {
  margin-top: 0.9rem;
}

.avatar-picker__options--images {
  gap: 0.52rem;
}

.avatar-picker__option {
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  padding: 0;
  border: 2px solid transparent;
  border-radius: 50%;
  cursor: pointer;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.avatar-picker__option:hover {
  transform: scale(1.08);
}

.avatar-picker__option--selected {
  border-color: var(--sp-text);
  transform: scale(1.12);
}

.avatar-picker__option--image {
  width: 64px;
  height: 64px;
  overflow: hidden;
  border-width: 3px;
  background-color: var(--sp-soft);
  background-clip: padding-box;
}

.avatar-picker__image-check {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  color: #fff;
  background: rgba(20, 34, 55, 0.466);
}

.join-actions {
  align-items: stretch;
  flex-direction: column;
  gap: 0.5rem;
  min-height: 5.25rem;
  padding: 0 2rem 1.75rem;
}

.create-room-link-slot {
  min-height: 2.25rem;
  display: flex;
  align-items: flex-start;
}

.create-room-btn {
  width: 100%;
  margin-top: 1rem;
}

.join-submit-btn {
  width: 100%;
  margin-top: 0.35rem;
}

.room-notice {
  background: var(--sp-strong-soft);
  color: var(--sp-strong);
}

.banner-muted {
  background: var(--sp-soft);
  color: var(--sp-text);
  border: 1px solid var(--sp-border);
}

.banner-error {
  background: color-mix(in srgb, #e53935 12%, var(--sp-surface));
  color: #d02626;
  border: 1px solid color-mix(in srgb, #e53935 45%, var(--sp-border));
}

.scrum-poker-page--dark .banner-error {
  color: #ff8f8f;
}

.scrum-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 350px;
  gap: 1.1rem;
  max-width: 1260px;
  margin: 0 auto;
  align-items: start;
}

.details-column {
  display: grid;
  gap: 0.875rem;
  align-content: start;
}

.voting-panel {
  display: grid;
  gap: 0.875rem;
  align-content: start;
}

.round-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0 0.1rem 0.65rem;
}

.round-heading h2,
.panel-heading h3 {
  margin: 0;
  font-size: 1.2rem;
  font-weight: 700;
}

.phase-chip {
  text-transform: uppercase;
}

.participant-total {
  color: var(--sp-muted);
  font-size: 0.86rem;
  font-weight: 500;
  margin-left: 0.5rem;
}

.cards-card,
.participants-panel,
.side-card {
  border: 1px solid var(--sp-border);
  border-radius: 12px;
  background: var(--sp-surface);
  box-shadow: var(--sp-shadow);
}

.voting-card-section {
  padding: 0.65rem 0.95rem 1rem;
}

.card-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 0.5rem;
}

.poker-card {
  min-height: 64px;
  border: 1px solid var(--sp-border);
  border-radius: 10px;
  color: var(--sp-text);
  background: var(--sp-surface);
  font-size: 1.125rem;
  font-weight: 600;
  transition:
    transform 0.12s ease,
    box-shadow 0.12s ease,
    border-color 0.12s ease;
}

.poker-card:hover {
  box-shadow: 0 5px 12px rgba(48, 107, 190, 0.15);
  transform: translateY(-1px);
}

.poker-card--selected {
  border-color: var(--sp-strong);
  background: var(--sp-strong);
  color: #fff;
  box-shadow: 0 7px 15px rgba(29, 85, 167, 0.28);
}

.voting-actions {
  justify-content: space-between;
  padding: 0.85rem 1.05rem 0.9rem;
  border-top: 1px solid var(--sp-border);
  color: var(--sp-muted);
  font-size: 0.86rem;
}

.round-indicator {
  color: var(--sp-text);
  font-weight: 600;
}

.participants-panel {
  padding: 0.65rem 0.95rem 0.85rem;
}

.panel-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.7rem;
  padding: 0.2rem 0.1rem 0.65rem;
}

.action-row {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.control-btn {
  border: 1px solid var(--sp-border);
}

.participant-list {
  border-top: 1px solid var(--sp-border);
}

.participant-list .q-item {
  min-height: 56px;
  padding: 0.6rem 0.15rem;
  border-bottom: 1px solid var(--sp-border);
}

.participant-list .q-item:last-child {
  border-bottom: 0;
}

.participant-avatar {
  width: 50px;
  height: 50px;
  background: transparent;
  color: inherit;
  font-weight: 700;
}

.status-dot {
  display: block;
  flex: 0 0 7px;
  width: 7px;
  height: 7px;
  margin: 0;
  border-radius: 50%;
  background: #c2c9d6;
}

.status-dot--ready {
  background: #36b37e;
}

.participant-role-label {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.participant-name-label {
  font-weight: 500;
  font-size: 1rem;
}

.participant-role-label--owner {
  color: var(--fuse-text-muted);
  font-weight: 400;
}

.participant-role-label--host {
  color: var(--fuse-text-muted);
  font-weight: 400;
}

.participant-status-slot {
  display: flex;
  flex: 0 0 92px;
  width: 92px;
  min-width: 92px;
  box-sizing: border-box;
  padding: 0 0 0 0.45rem;
  flex-direction: row;
  justify-content: flex-start;
  align-items: center;
  gap: 0.3rem;
  color: var(--fuse-text-muted);
  font-size: 0.75rem;
  line-height: 1;
  white-space: nowrap;
}

.participant-status-text {
  display: block;
  text-align: center;
}

.participant-card {
  display: grid;
  place-items: center;
  min-width: 52px;
  height: 36px;
  font-size: 1.125rem;
  border-radius: 7px;
  background: var(--sp-strong-soft);
  color: var(--sp-strong);
  font-weight: 700;
}

.participant-score-entry {
  position: relative;
  z-index: 1;
  display: block;
  height: 36px;
  animation: participant-score-enter 0.28s cubic-bezier(0.2, 0.75, 0.25, 1) both;
}

.participant-score-leave-active {
  transform-origin: center bottom;
  animation: participant-score-leave 0.28s cubic-bezier(0.4, 0, 1, 1) both;
}

@keyframes participant-score-leave {
  from {
    opacity: 1;
    transform: translateY(0);
  }
  to {
    opacity: 0;
    transform: translateY(100%);
  }
}

@keyframes participant-score-enter {
  from {
    opacity: 0;
    transform: translateY(100%);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.participant-score-slot {
  perspective: 500px;
  perspective-origin: center center;
}

.participant-score-flip {
  position: relative;
  display: block;
  width: 52px;
  height: 36px;
  transform-style: preserve-3d;
  transform-origin: center center;
  transition: transform 0.7s cubic-bezier(0.2, 0.75, 0.25, 1);
}

.participant-score-flip--revealed {
  transform: rotateY(180deg);
}

.overall-score-flip {
  flex: 0 0 52px;
  width: 52px;
  height: 36px;
  transition: transform 0.7s cubic-bezier(0.2, 0.75, 0.25, 1);
}

.participant-score-face {
  position: absolute;
  inset: 0;
  backface-visibility: hidden;
}

.participant-score-face--question {
  display: grid;
  place-items: center;
  min-width: 52px;
  height: 36px;
  border-radius: 7px;
  background: var(--sp-strong-soft);
  color: var(--sp-strong);
  font-size: 1.125rem;
  font-weight: 700;
}

.participant-score-face--value {
  transform: rotateY(180deg);
}

.participant-management-slot,
.participant-score-slot {
  flex: 0 0 52px;
  width: 52px;
  min-width: 52px;
  padding: 0;
  justify-content: center;
  align-items: center;
}

.participant-management-slot {
  flex-basis: 92px;
  width: 92px;
  min-width: 92px;
  display: flex;
  flex-direction: row;
  flex-wrap: nowrap;
  gap: 0.1rem;
}

.participant-score-slot {
  position: relative;
  flex-direction: column;
}

.score-card-placeholder {
  position: absolute;
  z-index: 0;
  box-sizing: border-box;
  width: 52px;
  height: 36px;
  border: 1px solid #d9dee7;
  border-radius: 7px;
  background: transparent;
}

.participant-status-slot {
  flex: 0 0 92px;
  width: 92px;
  min-width: 92px;
}

.participant-transfer-btn,
.participant-remove-btn {
  color: #a1a1a179;
  margin-right: 1em;
}

.participant-transfer-btn:hover,
.participant-remove-btn:hover {
  color: #909090;
}

.participant-summary {
  border-top: 1px solid var(--sp-border);
  padding: 1rem 0.25rem 0.1rem;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.participant-summary-clip {
  overflow: hidden;
}

.participant-summary-enter-active,
.participant-summary-leave-active {
  transition:
    opacity 0.28s ease,
    transform 0.28s cubic-bezier(0.2, 0.75, 0.25, 1);
}

.participant-summary-enter-from,
.participant-summary-leave-to {
  opacity: 0;
  transform: translateY(-100%);
}

.participant-summary > div {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  perspective: 500px;
  perspective-origin: center center;
}

.summary-label {
  color: var(--sp-muted);
  font-size: 0.875rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  margin-right: 10px;
}

.summary-value {
  font-size: 1.125rem;
  font-weight: 650;
}

.overall-score-slot {
  position: relative;
  flex: 0 0 52px;
  width: 52px;
  height: 36px;
}

.overall-score-slot > .participant-score-enter-active,
.overall-score-slot > .participant-score-leave-active {
  position: absolute;
  inset: 0;
}

.overall-score-slot > .participant-score-enter-active {
  animation: participant-score-enter 0.28s cubic-bezier(0.2, 0.75, 0.25, 1) both;
}

.overall-score-slot > .participant-score-leave-active {
  animation: participant-score-leave 0.28s cubic-bezier(0.4, 0, 1, 1) both;
}

.overall-score-slot > .summary-value {
  display: grid;
  width: 100%;
  height: 100%;
  place-items: center;
}

.participant-summary > div:last-child {
  justify-content: flex-end;
}

.side-card {
  border-radius: 12px;
}

.side-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.room-edit-btn {
  color: var(--sp-muted);
}

.side-title {
  color: var(--sp-muted);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.7rem;
  font-weight: 700;
}

.side-room-name {
  margin-top: 0.4rem;
  font-weight: 700;
  color: var(--sp-text);
}

.side-room-code {
  margin-top: 0.15rem;
  color: var(--sp-muted);
  font-size: 0.83rem;
}

.settings-row {
  margin-top: 0.75rem;
  margin-bottom: 0.3rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.settings-label {
  color: var(--sp-text);
  line-height: 1.25;
}

.legend-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  color: var(--sp-muted);
  margin-top: 0.75rem;
  margin-bottom: 0.3rem;
  font-size: 0.86rem;
}

.legend-row strong {
  color: var(--sp-text);
  width: 16px;
}

.leave-room-btn {
  border: 1px solid var(--sp-border);
  background: var(--sp-surface);
}

.q-chip {
  padding: 0.6em 0.6em 0.7em;
}

@media (max-width: 1150px) {
  .scrum-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}

@media (max-width: 720px) {
  .card-grid {
    grid-template-columns: repeat(5, 1fr);
  }

  .panel-heading {
    flex-direction: column;
    align-items: flex-start;
  }
}

@media (max-width: 520px) {
  .scrum-poker-page {
    padding: 1rem 0.7rem;
  }

  .scrum-header {
    margin-bottom: 0.85rem;
  }

  .room-pill__label {
    display: none;
  }

  .card-grid {
    grid-template-columns: repeat(4, 1fr);
    gap: 0.45rem;
  }

  .poker-card {
    min-height: 54px;
  }

  .join-card__intro,
  .join-form {
    padding-left: 1.15rem;
    padding-right: 1.15rem;
  }

  .join-actions {
    padding-left: 1.15rem;
    padding-right: 1.15rem;
    flex-wrap: wrap;
  }
}
</style>
