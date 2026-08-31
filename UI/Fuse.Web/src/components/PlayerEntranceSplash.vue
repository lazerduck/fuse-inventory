<template>
  <Transition name="player-entrance" appear>
    <div
      v-if="show"
      class="player-entrance"
      role="status"
      aria-live="polite"
      @animationend="finish"
    >
      <div class="player-entrance__stage">
        <div class="player-entrance__burst" aria-hidden="true">
          <span
            class="player-entrance__ring player-entrance__ring--outer"
          ></span>
          <span
            class="player-entrance__ring player-entrance__ring--inner"
          ></span>
        </div>
        <div
          class="player-entrance__avatar"
          :style="avatarStyle"
          aria-hidden="true"
        ></div>
        <div class="player-entrance__name">{{ playerName }}</div>
        <div class="player-entrance__label">JOINING ROOM</div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, watch } from "vue";
import {
  scrumPokerAvatarForSelection,
  scrumPokerAvatarImageStyle,
} from "../utils/scrumPokerAvatar";

const props = defineProps<{
  avatar: string | null | undefined;
  playerName: string;
  show: boolean;
}>();
const emit = defineEmits<{ complete: [] }>();

let completionTimer: ReturnType<typeof setTimeout> | undefined;
let completed = false;
const avatarStyle = computed(() => {
  const avatar = scrumPokerAvatarForSelection(props.avatar);
  return avatar ? scrumPokerAvatarImageStyle(avatar, true) : {};
});

function finish(event?: AnimationEvent) {
  if (completed || (event && event.animationName !== "player-entrance-exit"))
    return;
  completed = true;
  if (completionTimer) clearTimeout(completionTimer);
  emit("complete");
}

function handleMotionPreference() {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    requestAnimationFrame(() => finish());
  }
}

watch(
  () => props.show,
  async (show) => {
    if (!show) return;
    completed = false;
    await nextTick();
    handleMotionPreference();
    completionTimer = setTimeout(() => finish(), 2250);
  },
);

onBeforeUnmount(() => {
  if (completionTimer) clearTimeout(completionTimer);
});
</script>

<style scoped>
.player-entrance {
  position: fixed;
  z-index: 7000;
  inset: 0;
  overflow: hidden;
  background: color-mix(in srgb, var(--fuse-page-bg) 76%, transparent);
  color: var(--fuse-text-primary, inherit);
  isolation: isolate;
  pointer-events: none;
  animation: player-entrance-exit 400ms 1800ms cubic-bezier(0.4, 0, 0.8, 1) both;
}

.player-entrance__stage {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 360px;
  height: 360px;
  transform: translate(-50%, -50%);
}

.player-entrance__avatar {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 180px;
  height: 180px;
  border: 5px solid color-mix(in srgb, var(--q-primary) 72%, white);
  border-radius: 50%;
  box-shadow:
    0 0 0 8px color-mix(in srgb, var(--q-primary) 13%, transparent),
    0 16px 40px color-mix(in srgb, var(--q-primary) 26%, transparent);
  will-change: transform, opacity;
  animation: player-entrance-avatar 980ms cubic-bezier(0.18, 0.9, 0.3, 1) both;
}

.player-entrance__burst {
  position: relative;
  width: 360px;
  height: 360px;
  pointer-events: none;
}

.player-entrance__ring {
  position: absolute;
  inset: 50%;
  border: 1px solid color-mix(in srgb, var(--q-primary) 58%, white);
  border-radius: 50%;
  transform: translate(-50%, -50%);
  animation: player-entrance-ring 850ms 510ms ease-out both;
  will-change: transform, opacity;
}

.player-entrance__ring--inner {
  width: 220px;
  height: 220px;
  border-style: dashed;
  opacity: 0.55;
  animation-delay: 530ms;
}

.player-entrance__ring--outer {
  width: 290px;
  height: 290px;
  opacity: 0.65;
}

.player-entrance__name,
.player-entrance__label {
  position: absolute;
  left: 50%;
  width: min(80vw, 420px);
  text-align: center;
  transform: translateX(-50%);
  animation: player-entrance-copy 440ms 300ms ease-out both;
}

.player-entrance__name {
  top: calc(50% + 135px);
  overflow: hidden;
  font-size: clamp(1.35rem, 3vw, 2rem);
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.player-entrance__label {
  top: calc(50% + 195px);
  color: var(--q-primary);
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.18em;
}

@keyframes player-entrance-avatar {
  0% {
    opacity: 0;
    transform: translate(-50%, -50%) scale(0.6);
  }
  42% {
    opacity: 1;
    transform: translate(-50%, -50%) scale(1.1);
  }
  100% {
    transform: translate(-50%, -50%) scale(1);
  }
}

@keyframes player-entrance-ring {
  0% {
    opacity: 0;
    transform: translate(-50%, -50%) scale(0.85);
  }
  35% {
    opacity: 0.8;
  }
  100% {
    opacity: 0;
    transform: translate(-50%, -50%) scale(1.25);
  }
}

@keyframes player-entrance-copy {
  from {
    opacity: 0;
    transform: translate(-50%, 10px);
  }
  to {
    opacity: 1;
    transform: translate(-50%, 0);
  }
}

@keyframes player-entrance-exit {
  from {
    opacity: 1;
  }
  to {
    opacity: 0;
    transform: scale(1.025);
  }
}

@media (prefers-reduced-motion: reduce) {
  .player-entrance,
  .player-entrance__avatar,
  .player-entrance__ring,
  .player-entrance__name,
  .player-entrance__label {
    animation: none;
  }
}

@media (max-width: 480px) {
  .player-entrance__avatar {
    width: 150px;
    height: 150px;
  }
  .player-entrance__burst {
    transform: scale(0.84);
  }
  .player-entrance__name {
    top: calc(50% + 118px);
  }
  .player-entrance__label {
    top: calc(50% + 170px);
  }
}
</style>
