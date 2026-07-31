import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

describe('driver PWA configuration', () => {
  it('declares an installable standalone driver application and does not cache APIs', () => {
    const root = process.cwd()
    const manifest = JSON.parse(
      readFileSync(resolve(root, 'public/manifest.webmanifest'), 'utf8'),
    ) as { display: string; start_url: string; icons: unknown[] }
    const worker = readFileSync(resolve(root, 'public/sw.js'), 'utf8')

    expect(manifest.display).toBe('standalone')
    expect(manifest.start_url).toBe('/driver')
    expect(manifest.icons).not.toHaveLength(0)
    expect(worker).toContain("url.pathname.startsWith('/api/')")
  })
})
