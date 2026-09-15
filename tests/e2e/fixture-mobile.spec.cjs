const { test, expect } = require('@playwright/test');

test.describe('mobile live round fixture', () => {
  test('keeps an edit on-device while offline and syncs after reconnecting', async ({ page, context }) => {
    await page.goto('/live-round.html?id=1');
    await expect(page.locator('#hole-heading')).toHaveText('10');

    await context.setOffline(true);
    await page.getByRole('button', { name: 'Increase Score' }).click();
    await expect(page.locator('#live-status')).toContainText(/Offline|saved on this device/i);

    await context.setOffline(false);
    await page.evaluate(() => window.dispatchEvent(new Event('online')));
    await expect(page.locator('#live-status')).toHaveText('Saved to server');
    await expect(page.locator('.round-progress span')).toContainText('0 pending');
  });

  test('announces the device, syncing, and server-saved states', async ({ page }) => {
    await page.goto('/live-round.html?id=1');
    await page.route('**/api/rounds/1/holes/by-number/10', async route => {
      await new Promise(resolve => setTimeout(resolve, 300));
      await route.continue();
    });

    await page.getByRole('button', { name: 'Increase Score' }).click();
    await expect(page.locator('#live-status')).toContainText('saved on this device');
    await expect(page.locator('#live-status')).toHaveText('Syncing with server…');
    await expect(page.locator('#live-status')).toHaveText('Saved to server');
  });

  test('uses an accessible dialog to resolve a concurrent edit', async ({ page }) => {
    await page.goto('/live-round.html?id=3');
    await page.getByRole('button', { name: 'Increase Score' }).click();

    await expect(page.locator('#live-status')).toContainText('Save conflict');
    await page.getByRole('button', { name: 'Review save conflict' }).click();

    const dialog = page.getByRole('dialog', { name: 'Resolve hole 10' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('table', { name: 'Hole 10 save conflict' })).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Use server record' })).toBeFocused();
    await dialog.getByRole('button', { name: 'Use server record' }).click();

    await expect(dialog).toBeHidden();
    await expect(page.locator('#live-status')).toHaveText('Saved to server');
  });

  test('fits the scorecard in a phone viewport and supports keyboard activation', async ({ page }) => {
    await page.goto('/live-round.html?id=1');
    await expect(page.locator('#hole-heading')).toHaveText('10');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

    const increase = page.getByRole('button', { name: 'Increase Score' });
    const puttsIncrease = page.getByRole('button', { name: 'Increase Putts' });
    expect((await increase.boundingBox()).width).toBeGreaterThanOrEqual(56);
    expect((await puttsIncrease.boundingBox()).width).toBeGreaterThanOrEqual(56);
    const originalScore = Number(await page.locator('#score-value').textContent());
    await increase.focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('#score-value')).toHaveText(String(originalScore + 1));
  });

  test('explains how to recover when an offline draft has no cached copy', async ({ page, context }) => {
    await page.goto('/live-round.html?id=404');
    await expect(page.locator('#page-retry-title')).toHaveText('We could not open this round');

    await context.setOffline(true);
    await page.reload();
    await expect(page.locator('#page-retry-title')).toHaveText('This round is not available offline yet');
    await expect(page.getByRole('button', { name: 'Try again after reconnecting' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Back to rounds' })).toBeVisible();
  });
});
