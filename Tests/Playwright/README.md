# Playwright Tests for Fuse Inventory

This directory contains end-to-end tests for the Fuse Inventory application using Playwright.

## Setup

Dependencies are already installed. If you need to reinstall:

```bash
npm install
```

## Running Tests

### Standard test run
Playwright will automatically start the app using docker-compose, run the tests, and clean up:

```bash
npm test
```

### Other test modes

- **Headed mode** (see browser): `npm run test:headed`
- **UI mode** (interactive): `npm run test:ui`
- **Debug mode**: `npm run test:debug`
- **View report**: `npm run report`

### Manual Docker Control

If you need to manually control the Docker containers:

- **Start app**: `npm run docker:up`
- **Stop app**: `npm run docker:down`
- **Stop and clean**: `npm run docker:clean`

### CI/CD

For CI environments, use:

```bash
npm run pretest:ci  # Start docker containers
npm test           # Run tests
npm run posttest:ci # Clean up containers
```

## Configuration

The Playwright configuration is in `playwright.config.ts`. It's set up to:
- Automatically start the app via docker-compose before tests
- Run tests against `http://localhost:8080`
- Test on Chromium, Firefox, and WebKit browsers
- Generate HTML reports

## Writing Tests

Add new test files in the `tests/` directory with the `.spec.ts` extension. See `tests/example.spec.ts` for a basic example.
