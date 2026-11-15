# Fuse frontend

Use Node.js 26 LTS (`nvm use` from the repository root). Docker development and CI use the same major version.

```bash
npm ci
npm run dev
npm run test:typecheck
npm test
npm run build
```

The frontend uses Vue 3, Quasar, Vite 8 and the TypeScript 7 native checker through `typescript-native-bridge`. Component tests use Vitest 5 and jsdom 30; browser tests are documented in [Tests/Playwright](../../Tests/Playwright/README.md).

## Dependency maintenance

The frontend follows the [bridge's npm installation instructions](https://github.com/johnsoncodehk/typescript-native-bridge#npm): `typescript` is an exact-version npm alias to `typescript-native-bridge`, with `overrides.typescript` set to `$typescript`. This preserves the classic compiler API required by `vue-tsc` while using the TypeScript 7 checker. Do not replace the alias with plain `typescript@7` until Vue's integration supports its API. After changing the pinned bridge version, regenerate the lockfile with npm and validate with `npm ci` and the checks above. `noUncheckedIndexedAccess` is explicit in the app configuration to preserve the strict array/index checks previously inherited from `@vue/tsconfig`.

`vue-tsc` is pinned to 3.3.13 because [3.3.14 incorrectly reports `.value` errors for template attributes containing URLs](https://github.com/vuejs/language-tools/issues/6322). Revisit the pin when an upstream fix is released; the components do not require a source workaround.


## Native compiler requirements

The bridge prints `TNB ACTIVE` when type checking starts. Supported Linux environments require glibc 2.35 or newer; Alpine/musl is unsupported. The frontend Docker build stage uses Node 26 on Debian Bookworm for this reason. The final application runtime image is unchanged. Native Linux AMD64 and ARM64 builds are checked in CI.

CLI checks use the project compiler automatically. To use it in VS Code, select **TypeScript: Select TypeScript Version → Use Workspace Version**. If necessary, set `js/ts.tsdk.path` to `UI/Fuse.Web/node_modules/typescript/lib` when opening the repository root, or `node_modules/typescript/lib` when opening this frontend directory.

To roll back, restore `devDependencies.typescript` to `~6.0.3`, remove `overrides.typescript`, run `npm install` to regenerate the lockfile, and rerun the checks above.
