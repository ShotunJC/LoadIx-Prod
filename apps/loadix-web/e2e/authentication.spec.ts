import { expect, test } from '@playwright/test'

test('tenant administrator signs in and opens user administration', async ({
  page,
}) => {
  await page.goto('/')

  await expect(
    page.getByRole('heading', { name: 'Sign in to LoadIx' }),
  ).toBeVisible()
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(
    page.getByRole('heading', { name: 'Operations dashboard' }),
  ).toBeVisible()
  await page.getByRole('link', { name: 'Users' }).click()
  await expect(
    page.getByRole('heading', { name: 'Users', exact: true }),
  ).toBeVisible()
  await expect(
    page.getByRole('heading', { name: 'Invite a user' }),
  ).toBeVisible()
})
