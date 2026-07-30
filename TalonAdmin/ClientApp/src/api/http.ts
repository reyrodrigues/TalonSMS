/**
 * Low-level HTTP for the Talon API.
 *
 * Two things every authenticated call needs, both inherited from the server contract:
 *  - `Authorization: Token <jwt>` — the scheme this API has always used. The server also accepts
 *    `Bearer`, but `Token` is what it was built around, so it stays.
 *  - `X-Tenant-Country` / `X-Tenant-Organization` — nearly every query is filtered by these.
 */

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly url: string,
    readonly body: string
  ) {
    super(`${status} ${url}: ${body.slice(0, 300)}`)
    this.name = 'ApiError'
  }
}

export interface TenantHeaders {
  countryId: number
  organizationId: number
}

type TokenProvider = () => string | null
type TenantProvider = () => TenantHeaders | null
type UnauthorizedHandler = () => void

let getToken: TokenProvider = () => null
let getTenant: TenantProvider = () => null
let onUnauthorized: UnauthorizedHandler = () => {}

/** Wired up once at start-up so this module does not have to import the store (and vice versa). */
export function configureHttp(options: {
  token: TokenProvider
  tenant: TenantProvider
  unauthorized: UnauthorizedHandler
}) {
  getToken = options.token
  getTenant = options.tenant
  onUnauthorized = options.unauthorized
}

function buildHeaders(extra?: HeadersInit): Headers {
  const headers = new Headers(extra)

  const token = getToken()
  if (token) headers.set('Authorization', `Token ${token}`)

  const tenant = getTenant()
  if (tenant) {
    headers.set('X-Tenant-Country', String(tenant.countryId))
    headers.set('X-Tenant-Organization', String(tenant.organizationId))
  }

  return headers
}

async function send(url: string, init: RequestInit): Promise<Response> {
  const response = await fetch(url, { ...init, headers: buildHeaders(init.headers) })

  if (response.status === 401) {
    onUnauthorized()
  }

  if (!response.ok) {
    throw new ApiError(response.status, url, await response.text())
  }

  return response
}

export async function getJson<T>(url: string): Promise<T> {
  const response = await send(url, { method: 'GET' })
  return (await response.json()) as T
}

export async function postJson<T>(url: string, body: unknown): Promise<T> {
  const response = await send(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  const text = await response.text()
  return (text ? JSON.parse(text) : null) as T
}

export async function postForm(url: string, form: Record<string, string>): Promise<Response> {
  return send(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams(form).toString()
  })
}

export async function postBlob(url: string, body: unknown): Promise<Blob> {
  const response = await send(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  return response.blob()
}

export function downloadBlob(blob: Blob, fileName: string) {
  const href = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = href
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(href)
}
