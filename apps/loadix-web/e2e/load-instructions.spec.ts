import { expect, test } from '@playwright/test'

test('creates and plans a load instruction', async ({ page }) => {
  const suffix = Date.now().toString()
  const contractNumber = `AAA-LI-CTR-${suffix}`
  const instructionNumber = `PW-LI-${suffix}`

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
  await page.getByLabel('Contract name').fill('Instruction journey contract')
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Route reference').fill(`LI-RT-${suffix}`)
  await page.getByLabel('Source location').selectOption({ index: 1 })
  await page.getByLabel('Destination location').selectOption({ index: 2 })
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Permitted commodity').selectOption({ index: 1 })
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Fixed amount', { exact: true }).fill('1000')
  await page.getByRole('button', { name: 'Create draft contract' }).click()
  await page.getByRole('button', { name: 'Activate contract' }).click()
  await expect(page.getByText('Active', { exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Load instructions' }).click()
  await page.getByRole('button', { name: 'New instruction' }).click()
  await page.getByLabel('Instruction number').fill(instructionNumber)
  const contractValue = await page
    .getByLabel('Transport contract')
    .locator('option', { hasText: contractNumber })
    .getAttribute('value')
  if (!contractValue) throw new Error('Created contract option was not found.')
  await page.getByLabel('Transport contract').selectOption(contractValue)
  await page.getByLabel('Route').selectOption({ index: 1 })
  await page.getByLabel('Commodity').selectOption({ index: 1 })
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Requested quantity').fill('60')
  await page.getByLabel('Requested trip count').fill('2')
  await page.getByRole('button', { name: 'Continue' }).click()
  await page
    .getByLabel('Customer purchase-order reference')
    .fill(`PO-${suffix}`)
  await page.getByLabel('Customer instruction reference').fill(`CI-${suffix}`)
  await page
    .getByLabel('Special instructions')
    .fill('Synthetic Playwright instruction')
  await page.getByRole('button', { name: 'Create draft instruction' }).click()

  await expect(
    page.getByRole('heading', { name: instructionNumber }),
  ).toBeVisible()
  await expect(page.getByText('Draft', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Plan instruction' }).click()
  await expect(page.getByText('Planned', { exact: true }).first()).toBeVisible()
})
