import { test, expect } from '@playwright/test';
import { 
  waitForPageLoad,
  dismissInitialDialogs
} from './helpers';

test.describe('Graph', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await dismissInitialDialogs(page);
  });

  test('can view graph page', async ({ page }) => {
    // Navigate directly to the graph page via URL
    await page.goto('/graph');
    await waitForPageLoad(page);
    
    await expect(page.locator('h1:has-text("Graph")')).toBeVisible();
    await expect(page.locator('text=Visualize relationships between entities')).toBeVisible();
  });

  test('graph canvas is rendered', async ({ page }) => {
    await page.goto('/graph');
    await waitForPageLoad(page);
    
    // The graph is rendered in a div with class "graph"
    const graphCanvas = page.locator('.graph');
    await expect(graphCanvas).toBeVisible();
    
    // Wait for cytoscape to initialize (it creates a canvas element)
    // Give it some time as cytoscape initializes asynchronously
    await page.waitForTimeout(1000);
  });

  test('environment filter is displayed', async ({ page }) => {
    await page.goto('/graph');
    await waitForPageLoad(page);
    
    // The filter dropdown should be visible
    await expect(page.locator('text=Filter environments')).toBeVisible();
  });

  test('can navigate to graph from sidebar', async ({ page }) => {
    // The Graph link in the sidebar has an insights icon
    // Find the sidebar item that contains "Graph" text
    await page.click('text=Graph');
    await waitForPageLoad(page);
    
    await expect(page.locator('h1:has-text("Graph")')).toBeVisible();
  });
});
