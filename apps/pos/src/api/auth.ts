import { apiClient } from './client'
import type { PinLoginResponse } from '../types'

// Request types matching backend contracts
export interface PinLoginRequest {
  pin: string
  organizationId: string
  siteId: string
  deviceId: string
}

export interface LogoutRequest {
  organizationId: string
  deviceId: string
  sessionId: string
}

export interface RefreshTokenRequest {
  organizationId: string
  sessionId: string
  refreshToken: string
}

export interface RefreshTokenResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
}

// Device flow types
export interface DeviceCodeRequest {
  clientId: string
  scope?: string
  deviceFingerprint?: string
}

export interface DeviceCodeResponse {
  deviceCode: string
  userCode: string
  verificationUri: string
  expiresIn: number
  interval: number
}

export interface DeviceTokenRequest {
  userCode: string
  deviceCode: string
}

export interface DeviceTokenResponse {
  accessToken?: string
  refreshToken?: string
  expiresIn?: number
  error?: string
  errorDescription?: string
}

export interface AuthorizeDeviceRequest {
  userCode: string
  authorizedBy: string
  organizationId: string
  siteId: string
  deviceName: string
  appType: string
}

// Auth endpoints
export async function loginWithPin(request: PinLoginRequest): Promise<PinLoginResponse> {
  return apiClient.post<PinLoginResponse>('/api/auth/pin', request)
}

export async function logout(request: LogoutRequest): Promise<{ message: string }> {
  return apiClient.post('/api/auth/logout', request)
}

export async function refreshToken(request: RefreshTokenRequest): Promise<RefreshTokenResponse> {
  return apiClient.post<RefreshTokenResponse>('/api/auth/refresh', request)
}

// Device flow endpoints
export async function requestDeviceCode(request: DeviceCodeRequest): Promise<DeviceCodeResponse> {
  return apiClient.post<DeviceCodeResponse>('/api/device/code', request)
}

export async function pollDeviceToken(request: DeviceTokenRequest): Promise<DeviceTokenResponse> {
  return apiClient.post<DeviceTokenResponse>('/api/device/token', request)
}

export async function authorizeDevice(request: AuthorizeDeviceRequest): Promise<{ message: string }> {
  return apiClient.post('/api/device/authorize', request)
}
