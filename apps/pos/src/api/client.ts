const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

export interface TenantContext {
  orgId: string
  siteId: string
}

class ApiClient {
  private accessToken: string | null = null
  private tenantContext: TenantContext | null = null

  setToken(token: string | null) {
    this.accessToken = token
  }

  setTenantContext(context: TenantContext | null) {
    this.tenantContext = context
  }

  getTenantContext(): TenantContext | null {
    return this.tenantContext
  }

  buildOrgSitePath(path: string): string {
    if (!this.tenantContext) {
      throw new Error('Tenant context not set')
    }
    return `/api/orgs/${this.tenantContext.orgId}/sites/${this.tenantContext.siteId}${path}`
  }

  buildOrgPath(path: string): string {
    if (!this.tenantContext) {
      throw new Error('Tenant context not set')
    }
    return `/api/orgs/${this.tenantContext.orgId}${path}`
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    }

    if (this.accessToken) {
      headers['Authorization'] = `Bearer ${this.accessToken}`
    }

    // Merge any additional headers from options
    if (options.headers) {
      const additionalHeaders = options.headers as Record<string, string>
      Object.assign(headers, additionalHeaders)
    }

    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      ...options,
      headers,
    })

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Request failed' }))
      throw new Error(error.message || `HTTP ${response.status}`)
    }

    if (response.status === 204) {
      return undefined as T
    }

    return response.json()
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint)
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'POST',
      body: data ? JSON.stringify(data) : undefined,
    })
  }

  async put<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PUT',
      body: data ? JSON.stringify(data) : undefined,
    })
  }

  async patch<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PATCH',
      body: data ? JSON.stringify(data) : undefined,
    })
  }

  async delete(endpoint: string): Promise<void> {
    return this.request(endpoint, { method: 'DELETE' })
  }
}

export const apiClient = new ApiClient()
