# Fuse frontend

Use Node.js 26 LTS (`nvm use` from the repository root). Docker development and CI use the same major version.

```bash
npm ci
npm run dev
npm run test:typecheck
npm test
npm run build
```

The frontend uses Vue 3, Quasar, Vite 8 and TypeScript 6. Component tests use Vitest 5 and jsdom 30; browser tests are documented in [Tests/Playwright](../../Tests/Playwright/README.md).

## Dependency maintenance

Keep TypeScript on the 6.x line until Vue's compiler integration supports native TypeScript 7. `noUncheckedIndexedAccess` is explicit in the app configuration to preserve the strict array/index checks previously inherited from `@vue/tsconfig`.

`vue-tsc` is pinned to 3.3.13 because [3.3.14 incorrectly reports `.value` errors for template attributes containing URLs](https://github.com/vuejs/language-tools/issues/6322). Revisit the pin when an upstream fix is released; the components do not require a source workaround.
