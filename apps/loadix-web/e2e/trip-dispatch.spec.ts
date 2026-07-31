import { expect, test } from '@playwright/test'

test('generates, assigns, and dispatches a trip', async ({ page }) => {
  const suffix = Date.now().toString()
  const contractNumber = `AAA-DSP-${suffix}`
  const instructionNumber = `DSP-LI-${suffix}`
  const licenceNumber = `DSP-LIC-${suffix}`
  const vehicleFleetNumber = `DSP-TRK-${suffix}`
  const trailerFleetNumber = `DSP-TRL-${suffix}`

  await page.goto('/login')
  await page.getByLabel('Email').fill('admin@loadix.example.invalid')
  await page.getByLabel('Password').fill('LoadIx Demo Admin 2026!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(
    page.getByRole('heading', { name: 'Operations dashboard' }),
  ).toBeVisible()

  await page.getByRole('link', { name: 'Drivers' }).click()
  await page.getByRole('button', { name: 'Add driver' }).click()
  await page.getByLabel('Branch', { exact: true }).selectOption({ index: 1 })
  await page.getByLabel('First name').fill('Dispatch')
  await page.getByLabel('Last name').fill(`Driver ${suffix}`)
  await page.getByLabel('Internal identity reference').fill(`DSP-ID-${suffix}`)
  await page.getByLabel('Mobile number').fill('+27000000010')
  await page.getByLabel('Licence number').fill(licenceNumber)
  await page
    .getByLabel('Emergency contact', { exact: true })
    .fill('Synthetic Dispatch Contact')
  await page.getByLabel('Emergency contact phone').fill('+27000000011')
  await page.getByRole('button', { name: 'Create', exact: true }).click()

  await page.getByRole('link', { name: 'Trucks' }).click()
  await page.getByRole('button', { name: 'Add truck' }).click()
  await page.getByLabel('Branch', { exact: true }).selectOption({ index: 1 })
  await page
    .getByLabel('Fleet number', { exact: true })
    .fill(vehicleFleetNumber)
  await page.getByLabel('Registration', { exact: true }).fill(`DSP-T-${suffix}`)
  await page.getByLabel('Make').fill('Synthetic')
  await page.getByLabel('Model').fill('Dispatch')
  await page.getByLabel('Year').fill('2026')
  await page.getByLabel('Tare weight').fill('8')
  await page.getByLabel('Carrying capacity').fill('34')
  await page.getByRole('button', { name: 'Create', exact: true }).click()

  await page.getByRole('link', { name: 'Trailers' }).click()
  await page.getByRole('button', { name: 'Add trailer' }).click()
  await page.getByLabel('Branch', { exact: true }).selectOption({ index: 1 })
  await page
    .getByLabel('Fleet number', { exact: true })
    .fill(trailerFleetNumber)
  await page.getByLabel('Registration', { exact: true }).fill(`DSP-R-${suffix}`)
  await page.getByLabel('Make').fill('Synthetic')
  await page.getByLabel('Model').fill('Dispatch')
  await page.getByLabel('Year').fill('2026')
  await page.getByLabel('Tare weight').fill('6')
  await page.getByLabel('Carrying capacity').fill('36')
  await page.getByRole('button', { name: 'Create', exact: true }).click()

  await page.getByRole('link', { name: 'Contracts' }).click()
  await page.getByRole('button', { name: 'New contract' }).click()
  await page.getByLabel('Customer', { exact: true }).selectOption({ index: 1 })
  await page.getByLabel('Contract number').fill(contractNumber)
  await page.getByLabel('Contract name').fill('Dispatch journey contract')
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Route reference').fill(`DSP-RT-${suffix}`)
  await page.getByLabel('Source location').selectOption({ index: 1 })
  await page.getByLabel('Destination location').selectOption({ index: 2 })
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Permitted commodity').selectOption({ index: 1 })
  await page.getByRole('button', { name: 'Continue' }).click()
  await page.getByLabel('Fixed amount', { exact: true }).fill('1500')
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
  await page.getByLabel('Requested quantity').fill('30')
  await page.getByLabel('Requested trip count').fill('1')
  await page.getByRole('button', { name: 'Continue' }).click()
  await page
    .getByLabel('Customer purchase-order reference')
    .fill(`PO-DSP-${suffix}`)
  await page
    .getByLabel('Customer instruction reference')
    .fill(`CI-DSP-${suffix}`)
  await page.getByRole('button', { name: 'Create draft instruction' }).click()
  await page.getByRole('button', { name: 'Plan instruction' }).click()
  await page.getByLabel('Trip count').fill('1')
  await page.getByRole('button', { name: 'Generate trips' }).click()
  await expect(page.getByText(`${instructionNumber}-T001`)).toBeVisible()

  await page.getByRole('link', { name: 'Trips', exact: true }).click()
  await page.getByLabel('Search trips').fill(instructionNumber)
  await page.getByText(`${instructionNumber}-T001`).click()
  await expect(
    page.getByRole('dialog', { name: `${instructionNumber}-T001` }),
  ).toBeVisible()
  await page.getByLabel('Driver').selectOption({
    label: `Dispatch Driver ${suffix} · ${licenceNumber}`,
  })
  const vehicleValue = await page
    .getByLabel('Vehicle')
    .locator('option', { hasText: vehicleFleetNumber })
    .getAttribute('value')
  if (!vehicleValue) throw new Error('Dispatch vehicle option was not found.')
  await page.getByLabel('Vehicle').selectOption(vehicleValue)
  await page
    .getByRole('checkbox', { name: new RegExp(trailerFleetNumber) })
    .check()
  await page.getByRole('button', { name: 'Assign trip' }).click()
  const dialog = page.getByRole('dialog', { name: `${instructionNumber}-T001` })
  await expect(
    dialog.getByText('Assigned', { exact: true }).first(),
  ).toBeVisible()
  await dialog
    .locator('input[type="file"]')
    .first()
    .setInputFiles({
      name: `pod-${suffix}.pdf`,
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.7\nSynthetic proof of delivery\n%%EOF'),
    })
  await dialog.getByRole('button', { name: 'Upload document' }).click()
  await expect(dialog.getByText(`pod-${suffix}.pdf`)).toBeVisible()
  await expect(
    dialog.getByRole('button', { name: `Download pod-${suffix}.pdf` }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Dispatch trip' }).click()
  await expect(
    dialog.getByText('Dispatched', { exact: true }).first(),
  ).toBeVisible()
  await expect(page.getByText('Assigned → Dispatched')).toBeVisible()
})
