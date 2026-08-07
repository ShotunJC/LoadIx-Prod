import { expect, test } from '@playwright/test'

test('administrator dashboard pages do not return server errors', async ({
  page,
}) => {
  const serverErrors: string[] = []
  page.on('response', (response) => {
    if (response.status() >= 500) {
      serverErrors.push(
        `${String(response.status())} ${response.request().method()} ${response.url()}`,
      )
    }
  })

  await page.goto('/login')
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(
    page.getByRole('heading', { name: 'Operations dashboard' }),
  ).toBeVisible()

  const navigationLinks = page
    .getByRole('navigation', { name: 'Primary navigation' })
    .getByRole('link')
  const dashboardPaths: string[] = []
  for (let index = 0; index < (await navigationLinks.count()); index += 1) {
    const href = await navigationLinks.nth(index).getAttribute('href')
    if (href) dashboardPaths.push(new URL(href, page.url()).pathname)
  }

  for (const path of dashboardPaths) {
    await page
      .getByRole('navigation', { name: 'Primary navigation' })
      .locator(`a[href="${path}"]`)
      .click()
    await expect(page).toHaveURL(new RegExp(`${path.replace('/', '\\/')}$`))
    await page.waitForTimeout(250)
  }

  expect(serverErrors, serverErrors.join('\n')).toEqual([])
})
