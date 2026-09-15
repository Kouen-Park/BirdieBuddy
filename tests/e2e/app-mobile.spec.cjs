const { test, expect } = require('@playwright/test');

test.skip(!process.env.BIRDIEBUDDY_E2E_POSTGRES, 'Requires the PostgreSQL-backed ASP.NET Core app.');

test('lets a guest browse the home page and course catalogue', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole('heading', { name: 'Keep every round in view.' })).toBeVisible();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await expect(page.getByText('Browse without an account')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Create account' })).toBeVisible();
  await page.getByRole('button', { name: 'Close navigation' }).click();
  await expect(page.getByRole('link', { name: 'Explore courses' })).toBeVisible();

  await page.getByRole('link', { name: 'Explore courses' }).click();
  await expect(page).toHaveURL(/\/courses\.html$/);
  await expect(page.getByRole('heading', { name: 'Courses' })).toBeVisible();
  await expect(page.getByLabel('Find a course')).toBeVisible();
});

test('registers, starts, resumes, and completes a full round', async ({ page }) => {
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const email = `e2e-${unique}@example.test`;
  const courseName = `E2E Links ${unique}`;

  await page.goto('/signup.html');
  await page.getByLabel('Display name').fill('Mobile E2E');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('E2e-safe-password-42!');
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page).toHaveURL(/\/index\.html$/);

  const created = await page.evaluate(async ({ name }) => {
    const csrfResponse = await fetch('/api/security/csrf', { credentials: 'same-origin' });
    const { token } = await csrfResponse.json();
    const response = await fetch('/api/courses', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
      body: JSON.stringify({
        name,
        location: 'Playwright, NZ',
        holes: Array.from({ length: 18 }, (_, index) => ({
          holeNumber: index + 1,
          par: index % 3 === 0 ? 3 : 4,
          distance: 120 + index * 25
        }))
      })
    });
    return { status: response.status, body: await response.json() };
  }, { name: courseName });
  expect(created.status).toBe(201);

  await page.goto('/live-round.html');
  await page.locator('#live-course').selectOption(String(created.body.id));
  await expect(page.locator('#live-tee')).toBeEnabled();
  await page.locator('#live-tee').selectOption({ index: 0 });
  await page.getByRole('button', { name: 'Start round' }).click();
  await expect(page).toHaveURL(/\/live-round\.html\?id=\d+&new=1$/);

  const roundUrl = page.url();
  await page.reload();
  await expect(page).toHaveURL(roundUrl);
  await expect(page.locator('#hole-heading')).toHaveText('1');

  for (let hole = 1; hole <= 18; hole += 1) {
    await expect(page.locator('#hole-heading')).toHaveText(String(hole));
    const save = page.getByRole('button', { name: hole === 18 ? 'Save hole' : 'Save & next' });
    await save.focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('#live-status')).toHaveText('Saved to server');
  }

  const finish = page.getByRole('button', { name: 'Finish round' });
  await finish.focus();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/round-details\.html\?id=\d+$/);
  await expect(page.getByText('Completed', { exact: true })).toBeVisible();
});
