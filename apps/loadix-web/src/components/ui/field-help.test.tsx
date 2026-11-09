import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { FieldHelp } from './field-help'

describe('FieldHelp', () => {
  it('opens operational help with a touch or pointer action', async () => {
    render(<FieldHelp label="Tare weight" text="The empty vehicle weight." />)

    const trigger = screen.getByRole('button', { name: 'Help' })
    await userEvent.click(trigger)

    expect(trigger).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('tooltip')).toHaveTextContent(
      'The empty vehicle weight.',
    )
  })

  it('opens from the keyboard', async () => {
    render(<FieldHelp label="Rate type" text="How the trip is charged." />)

    await userEvent.tab()
    expect(screen.getByRole('button', { name: 'Help' })).toHaveFocus()
    await userEvent.keyboard('{Enter}')

    expect(screen.getByRole('tooltip')).toHaveTextContent(
      'How the trip is charged.',
    )
  })

  it('clamps the tooltip inside the right viewport edge', async () => {
    render(<FieldHelp label="Fuel" text="The fuel adjustment." />)
    const trigger = screen.getByRole('button', { name: 'Help' })
    vi.spyOn(trigger, 'getBoundingClientRect').mockReturnValue({
      bottom: 40,
      height: 24,
      left: 990,
      right: 1014,
      top: 16,
      width: 24,
      x: 990,
      y: 16,
      toJSON: () => ({}),
    })
    Object.defineProperty(window, 'innerWidth', {
      configurable: true,
      value: 1024,
    })

    await userEvent.click(trigger)

    expect(screen.getByRole('tooltip')).toHaveStyle({ left: '760px' })
  })
})
