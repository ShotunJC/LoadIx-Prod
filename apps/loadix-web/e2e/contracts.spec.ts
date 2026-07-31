import { expect, test } from '@playwright/test'

test('creates and activates a transport contract', async ({ page }) => {
  const suffix = Date.now().toString()
  const contractNumber = `PW-CTR-${suffix}`

  await page.goto('/login')
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(
    page.getByRole('heading', { name: 'Operations dashboard' }),
  ).toBeVisible()

  await page.getByRole('link', { name: 'Contracts' }).click()
  await page.getByRole('button', { name: 'New contract' }).click()
  await page.getByLabel('Customer', { exact: true }).selectOption({ index: 1 })
  await page.getByLabel('Contract number').fill(contractNumber)
  await page.getByLabel('Contract name').fill('Playwright minerals transport')
  await page.getByRole('button', { name: 'Continue' }).click()

  await page.getByLabel('Route reference').fill(`PW-RT-${suffix}`)
  await page.getByLabel('Source location').selectOption({ index: 1 })
  await page.getByLabel('Destination location').selectOption({ index: 2 })
  await page.getByRole('button', { name: 'Continue' }).click()

  await page.getByLabel('Permitted commodity').selectOption({ index: 1 })
  await page.getByRole('button', { name: 'Continue' }).click()
  await page
    .getByLabel('Rate type')
    .selectOption({ label: 'Base plus per tonne' })
  await page.getByLabel('Base fee').fill('500')
  await page.getByLabel('Amount per tonne').fill('125')
  await page.getByLabel('Minimum charge').fill('1000')
  await page.getByLabel('Fuel surcharge (%)').fill('5')
  await page.getByRole('button', { name: 'Create draft contract' }).click()

  await expect(
    page.getByRole('heading', { name: contractNumber }),
  ).toBeVisible()
  await expect(page.getByText('Draft', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Activate contract' }).click()
  await expect(page.getByText('Active', { exact: true })).toBeVisible()
  await expect(page.getByText('Activated', { exact: true })).toBeVisible()
})
