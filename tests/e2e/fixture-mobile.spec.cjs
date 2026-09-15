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
    const originalScore = Number(await page.locator('#score-value').textContent());
    await increase.focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('#score-value')).toHaveText(String(originalScore + 1));
  });
});
