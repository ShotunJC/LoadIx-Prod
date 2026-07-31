import { expect, test, type Page } from '@playwright/test'

test.describe.configure({ mode: 'serial' })

async function login(page: Page) {
  await page.goto('/login')
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(
    page.getByRole('heading', { name: 'Operations dashboard' }),
  ).toBeVisible()
}

async function fillAddress(page: Page) {
  await page.getByLabel('Address line 1').fill('42 Synthetic Road')
  await page.getByLabel('City').fill('Johannesburg')
  await page.getByLabel('Province / region').fill('Gauteng')
  await page.getByLabel('Postal code').fill('2000')
}

test('creates a customer and customer location', async ({ page }) => {
  const suffix = Date.now().toString()
  const customerName = `PW Customer ${suffix}`
  const customerRegistration = `PW-C-${suffix}`
  const locationName = `PW Site ${suffix}`

  await login(page)
  await page.getByRole('link', { name: 'Customers' }).click()
  await page.getByRole('button', { name: 'Add customer' }).click()
  await page
    .getByLabel('Legal name', { exact: true })
    .fill(`${customerName} (Pty) Ltd`)
  await page.getByLabel('Trading name', { exact: true }).fill(customerName)
  await page
    .getByLabel('Registration number', { exact: true })
    .fill(customerRegistration)
  await fillAddress(page)
  await page
    .getByLabel('Contact name', { exact: true })
    .fill('Synthetic Operations Contact')
  await page
    .getByLabel('Contact email', { exact: true })
    .fill(`operations-${suffix}@loadix.example.invalid`)
  await page.getByRole('button', { name: 'Create', exact: true }).click()
  await page.getByRole('searchbox').fill(customerRegistration)
  await expect(page.getByText(customerName, { exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Locations' }).click()
  await page.getByRole('button', { name: 'Add location' }).click()
  await page.getByLabel('Location name', { exact: true }).fill(locationName)
  await page.getByLabel('Location code').fill(`PW-L-${suffix}`)
  await page.getByLabel('Location type').selectOption('DeliverySite')
  await page.locator('select[name="customerId"]').selectOption({
    label: customerName,
  })
  await fillAddress(page)
  await page.getByLabel('Operating hours').fill('Mon–Fri 06:00–18:00')
  await page.getByRole('button', { name: 'Create', exact: true }).click()
  await page.getByRole('searchbox').fill(`PW-L-${suffix}`)
  await expect(page.getByText(locationName, { exact: true })).toBeVisible()
})

test('creates a driver, truck and trailer', async ({ page }) => {
  const suffix = Date.now().toString()
  await login(page)

  await page.getByRole('link', { name: 'Drivers' }).click()
  await page.getByRole('button', { name: 'Add driver' }).click()
  await page.getByLabel('Branch', { exact: true }).selectOption({ index: 1 })
  await page.getByLabel('First name').fill('Synthetic')
  await page.getByLabel('Last name').fill(`Driver ${suffix}`)
  await page.getByLabel('Internal identity reference').fill(`PW-ID-${suffix}`)
  await page.getByLabel('Mobile number').fill('+27000000000')
  await page.getByLabel('Licence number').fill(`PW-LIC-${suffix}`)
  await page
    .getByLabel('Emergency contact', { exact: true })
    .fill('Synthetic Emergency')
  await page.getByLabel('Emergency contact phone').fill('+27000000001')
  await page.getByRole('button', { name: 'Create', exact: true }).click()
  await page.getByRole('searchbox').fill(`PW-LIC-${suffix}`)
  await expect(
    page.getByRole('row', { name: new RegExp(`PW-LIC-${suffix}`) }),
  ).toBeVisible()

  await page.getByRole('link', { name: 'Trucks' }).click()
  await page.getByRole('button', { name: 'Add truck' }).click()
  await fillFleetForm(page, `PW-T-${suffix}`, `PW-TRK-${suffix}`)
  await page.getByRole('button', { name: 'Create', exact: true }).click()
  await page.getByRole('searchbox').fill(`PW-TRK-${suffix}`)
  await expect(
    page.getByText(`PW-TRK-${suffix}`, { exact: true }),
  ).toBeVisible()

  await page.getByRole('link', { name: 'Trailers' }).click()
  await page.getByRole('button', { name: 'Add trailer' }).click()
  await fillFleetForm(page, `PW-R-${suffix}`, `PW-TRL-${suffix}`)
  await page.getByRole('button', { name: 'Create', exact: true }).click()
  await page.getByRole('searchbox').fill(`PW-TRL-${suffix}`)
  await expect(
    page.getByText(`PW-TRL-${suffix}`, { exact: true }),
  ).toBeVisible()
})

test('edits and deactivates a record with version protection', async ({
  page,
}) => {
  const suffix = Date.now().toString()
  const code = `PW-COM-${suffix}`
  const initialName = `PW Commodity ${suffix}`
  const updatedName = `${initialName} Updated`
  await login(page)
  await page.getByRole('link', { name: 'Commodities' }).click()
  await page.getByRole('button', { name: 'Add commodity' }).click()
  await page.getByLabel('Commodity name', { exact: true }).fill(initialName)
  await page.getByLabel('Commodity code').fill(code)
  await page.getByLabel('Weight unit').selectOption('Tonnes')
  await page.getByRole('button', { name: 'Create', exact: true }).click()
  await page.getByRole('searchbox').fill(code)
  await expect(page.getByText(initialName, { exact: true })).toBeVisible()

  await page
    .getByRole('row', { name: new RegExp(code) })
    .getByRole('button', { name: 'View / edit' })
    .click()
  await page.getByLabel('Commodity name', { exact: true }).fill(updatedName)
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByText(updatedName, { exact: true })).toBeVisible()

  await page
    .getByRole('row', { name: new RegExp(code) })
    .getByRole('button', { name: 'View / edit' })
    .click()
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('button', { name: 'Deactivate' }).click()
  await page.getByLabel('Status').selectOption('inactive')
  await expect(page.getByText(updatedName, { exact: true })).toBeVisible()
})

async function fillFleetForm(
  page: Page,
  registration: string,
  fleetNumber: string,
) {
  await page.getByLabel('Branch', { exact: true }).selectOption({ index: 1 })
  await page.getByLabel('Fleet number', { exact: true }).fill(fleetNumber)
  await page.getByLabel('Registration', { exact: true }).fill(registration)
  await page.getByLabel('Make').fill('Synthetic')
  await page.getByLabel('Model').fill('Test')
  await page.getByLabel('Year').fill('2026')
  await page.getByLabel('Tare weight').fill('8')
  await page.getByLabel('Carrying capacity').fill('34')
}
