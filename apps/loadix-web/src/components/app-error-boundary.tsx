import { Component, type ErrorInfo, type ReactNode } from 'react'
import { TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface AppErrorBoundaryProps {
  children: ReactNode
}

interface AppErrorBoundaryState {
  hasError: boolean
}

export class AppErrorBoundary extends Component<
  AppErrorBoundaryProps,
  AppErrorBoundaryState
> {
  public state: AppErrorBoundaryState = { hasError: false }

  public static getDerivedStateFromError(): AppErrorBoundaryState {
    return { hasError: true }
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('The LoadIx application failed to render.', error, errorInfo)
  }

  public render() {
    if (this.state.hasError) {
      return (
        <main className="grid min-h-screen place-items-center p-6">
          <section className="w-full max-w-lg rounded-xl border bg-white p-8 shadow-sm">
            <TriangleAlert
              aria-hidden="true"
              className="text-amber-600"
              size={32}
            />
            <h1 className="mt-4 text-2xl font-bold text-slate-950">
              LoadIx could not load
            </h1>
            <p className="mt-2 text-slate-600">
              Refresh the page to try again. If the problem continues, contact
              your LoadIx administrator.
            </p>
            <Button
              className="mt-6"
              onClick={() => {
                window.location.reload()
              }}
            >
              Refresh page
            </Button>
          </section>
        </main>
      )
    }

    return this.props.children
  }
}
