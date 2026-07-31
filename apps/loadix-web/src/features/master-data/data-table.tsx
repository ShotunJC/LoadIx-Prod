import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import type { MasterDataItem, MasterDataPage } from './master-data-api'

export interface DataColumn {
  key: string
  label: string
  render: (item: MasterDataItem) => ReactNode
}

interface Props {
  columns: DataColumn[]
  data: MasterDataPage | undefined
  loading: boolean
  onEdit: (item: MasterDataItem) => void
  onPageChange: (page: number) => void
}

export function DataTable({
  columns,
  data,
  loading,
  onEdit,
  onPageChange,
}: Props) {
  if (loading) {
    return (
      <div aria-label="Loading records" className="space-y-3">
        {Array.from({ length: 5 }, (_, index) => (
          <div
            className="h-14 animate-pulse rounded-lg bg-slate-200"
            key={index}
          />
        ))}
      </div>
    )
  }

  if (!data?.items.length) {
    return (
      <div className="rounded-xl border border-dashed bg-white px-6 py-14 text-center">
        <p className="font-bold text-slate-900">No matching records</p>
        <p className="mt-1 text-sm text-slate-500">
          Adjust the filters or create the first record.
        </p>
      </div>
    )
  }

  return (
    <>
      <div className="overflow-x-auto rounded-xl border bg-white">
        <table className="w-full min-w-180 border-collapse text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-slate-600 uppercase">
            <tr>
              {columns.map((column) => (
                <th className="px-4 py-3" key={column.key} scope="col">
                  {column.label}
                </th>
              ))}
              <th className="px-4 py-3 text-right" scope="col">
                Actions
              </th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {data.items.map((item) => (
              <tr className="hover:bg-slate-50" key={item.id}>
                {columns.map((column) => (
                  <td className="px-4 py-3" key={column.key}>
                    {column.render(item)}
                  </td>
                ))}
                <td className="px-4 py-3 text-right">
                  <Button
                    onClick={() => {
                      onEdit(item)
                    }}
                    type="button"
                    variant="outline"
                  >
                    View / edit
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <nav
        aria-label="Pagination"
        className="mt-4 flex items-center justify-between"
      >
        <p className="text-sm text-slate-600">
          Page {data.page} of {Math.max(1, data.totalPages)} · {data.totalCount}{' '}
          records
        </p>
        <div className="flex gap-2">
          <Button
            disabled={data.page <= 1}
            onClick={() => {
              onPageChange(data.page - 1)
            }}
            type="button"
            variant="outline"
          >
            Previous
          </Button>
          <Button
            disabled={data.page >= data.totalPages}
            onClick={() => {
              onPageChange(data.page + 1)
            }}
            type="button"
            variant="outline"
          >
            Next
          </Button>
        </div>
      </nav>
    </>
  )
}
