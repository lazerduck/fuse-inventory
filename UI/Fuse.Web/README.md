# Vue 3 + TypeScript + Vite

This template should help get you started developing with Vue 3 and TypeScript in Vite. The template uses Vue 3 `<script setup>` SFCs, check out the [script setup docs](https://v3.vuejs.org/api/sfc-script-setup.html#sfc-script-setup) to learn more.

Learn more about the recommended Project Setup and IDE Support in the [Vue Docs TypeScript Guide](https://vuejs.org/guide/typescript/overview.html#project-setup).

## Dependency maintenance

`vue-tsc` is pinned to 3.3.13 because [3.3.14 incorrectly reports `.value` errors for template attributes containing URLs](https://github.com/vuejs/language-tools/issues/6322). Revisit the pin when an upstream fix is released; the components do not require a source workaround.
