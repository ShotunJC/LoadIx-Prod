import { expect, test } from '@playwright/test'

test.use({
  viewport: { width: 390, height: 844 },
  isMobile: true,
  hasTouch: true,
})

test('driver workspace remains explicit when the network goes offline', async ({
  page,
  context,
}) => {
  await page.goto('/login')
  await page.getByLabel('Email').fill('driver@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Driver 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).not.toHaveURL(/\/login/)
  await page.goto('/driver')

  await expect(
    page.getByRole('heading', { name: 'Your work today' }),
  ).toBeVisible()
  await context.setOffline(true)
  await expect(
    page.getByText('Offline: new updates will be queued safely.'),
  ).toBeVisible()
  await context.setOffline(false)
})
