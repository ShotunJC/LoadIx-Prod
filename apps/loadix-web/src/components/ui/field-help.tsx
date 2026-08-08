import {
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
} from 'react'
import { createPortal } from 'react-dom'
import { CircleHelp } from 'lucide-react'

const edgeGap = 8
const tooltipWidth = 256

export function FieldHelp({ label, text }: { label: string; text: string }) {
  const id = useId()
  const triggerRef = useRef<HTMLButtonElement>(null)
  const tooltipRef = useRef<HTMLSpanElement>(null)
  const [open, setOpen] = useState(false)
  const [pinned, setPinned] = useState(false)
  const [position, setPosition] = useState({ left: edgeGap, top: edgeGap })

  const updatePosition = useCallback(() => {
    const trigger = triggerRef.current
    if (!trigger) return
    const bounds = trigger.getBoundingClientRect()
    const width = Math.min(tooltipWidth, window.innerWidth - edgeGap * 2)
    const preferredLeft = bounds.left + bounds.width / 2 - width / 2
    const left = Math.min(
      Math.max(preferredLeft, edgeGap),
      window.innerWidth - width - edgeGap,
    )
    const height = tooltipRef.current?.offsetHeight ?? 0
    const below = bounds.bottom + edgeGap
    const top =
      height > 0 && below + height > window.innerHeight - edgeGap
        ? Math.max(edgeGap, bounds.top - height - edgeGap)
        : below
    setPosition({ left, top })
  }, [])

  useLayoutEffect(() => {
    if (open) updatePosition()
  }, [open, updatePosition])

  useEffect(() => {
    if (!open) return
    const reposition = () => {
      updatePosition()
    }
    const closeOutside = (event: PointerEvent) => {
      if (
        event.target instanceof Node &&
        !triggerRef.current?.contains(event.target) &&
        !tooltipRef.current?.contains(event.target)
      ) {
        setOpen(false)
        setPinned(false)
      }
    }
    window.addEventListener('resize', reposition)
    window.addEventListener('scroll', reposition, true)
    document.addEventListener('pointerdown', closeOutside)
    return () => {
      window.removeEventListener('resize', reposition)
      window.removeEventListener('scroll', reposition, true)
      document.removeEventListener('pointerdown', closeOutside)
    }
  }, [open, updatePosition])

  const close = () => {
    setOpen(false)
    setPinned(false)
  }

  return (
    <>
      <button
        aria-describedby={open ? id : undefined}
        aria-expanded={open}
        aria-label={`Help for ${label}`}
        className="ml-1 inline-flex size-6 items-center justify-center rounded-full text-slate-500 outline-none hover:bg-slate-100 hover:text-slate-800 focus-visible:ring-2 focus-visible:ring-emerald-600"
        onBlur={close}
        onClick={() => {
          if (pinned) close()
          else {
            setOpen(true)
            setPinned(true)
          }
        }}
        onKeyDown={(event) => {
          if (event.key === 'Escape') close()
        }}
        onMouseEnter={() => {
          setOpen(true)
        }}
        onMouseLeave={() => {
          if (!pinned) setOpen(false)
        }}
        onFocus={() => {
          setOpen(true)
        }}
        ref={triggerRef}
        type="button"
      >
        <CircleHelp aria-hidden="true" size={16} />
      </button>
      {open
        ? createPortal(
            <span
              className="fixed z-100 max-w-[calc(100vw-1rem)] rounded-lg bg-slate-950 px-3 py-2 text-left text-xs leading-5 font-medium text-white shadow-xl"
              id={id}
              ref={tooltipRef}
              role="tooltip"
              style={{
                left: position.left,
                top: position.top,
                width: tooltipWidth,
              }}
            >
              {text}
            </span>,
            document.body,
          )
        : null}
    </>
  )
}

export function FieldLabel({ label, help }: { label: string; help: string }) {
  return (
    <span className="mb-1 flex items-center text-sm font-bold text-slate-700">
      {label}
      <FieldHelp label={label} text={help} />
    </span>
  )
}
