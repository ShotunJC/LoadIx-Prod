import { expect, test } from '@playwright/test'

test('tenant administrator can review a filtered operations report', async ({
  page,
}) => {
  await page.goto('/')
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await page.getByRole('link', { name: 'Reports' }).click()
  await expect(
    page.getByRole('heading', { name: 'Operations reporting' }),
  ).toBeVisible()
  await page.getByLabel('Report').selectOption('exceptions')
  await expect(page.getByRole('button', { name: /CSV export/i })).toBeVisible()
  await expect(page.getByText('Exception report')).toBeVisible()
})

test('report selector exposes all supported reports and pagination controls', async ({
  page,
}) => {
  await page.goto('/')
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await page.getByRole('link', { name: 'Reports' }).click()
  const report = page.getByLabel('Report')
  for (const value of [
    'trip-register',
    'tonnage-by-customer',
    'tonnage-by-commodity',
    'tonnage-by-route',
    'variance',
    'driver-performance',
    'vehicle-utilisation',
    'contract-performance',
    'customer-revenue',
    'trip-profitability',
    'exceptions',
    'outstanding-pod',
    'expiries',
    'invoice-status',
  ]) {
    await report.selectOption(value)
  }
  await expect(
    page.getByRole('navigation', { name: 'Report pagination' }),
  ).toBeVisible()
})
