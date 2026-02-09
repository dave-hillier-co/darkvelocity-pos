import { apiClient } from './client'
import type { HalCollection, HalResource } from '../types'

export interface Employee extends HalResource {
  id: string
  userId: string
  defaultSiteId: string
  employeeNumber: string
  firstName: string
  lastName: string
  email: string
  employmentType: string
  hireDate?: string
  hourlyRate?: number
  salaryAmount?: number
  payFrequency?: string
  isClockedIn: boolean
  roles: EmployeeRole[]
}

export interface EmployeeRole {
  roleId: string
  roleName: string
  department: string
  isPrimary: boolean
  hourlyRateOverride?: number
}

export interface ClockResult extends HalResource {
  timeEntryId: string
  clockedInAt?: string
  clockedOutAt?: string
}

export async function createEmployee(data: {
  userId: string
  defaultSiteId: string
  employeeNumber: string
  firstName: string
  lastName: string
  email: string
  employmentType?: string
  hireDate?: string
}): Promise<{ id: string; employeeNumber: string; createdAt: string } & HalResource> {
  const endpoint = apiClient.buildOrgPath('/employees')
  return apiClient.post(endpoint, data)
}

export async function getEmployee(employeeId: string): Promise<Employee> {
  const endpoint = apiClient.buildOrgPath(`/employees/${employeeId}`)
  return apiClient.get(endpoint)
}

export async function updateEmployee(employeeId: string, data: {
  firstName?: string
  lastName?: string
  email?: string
  hourlyRate?: number
  salaryAmount?: number
  payFrequency?: string
}): Promise<Employee> {
  const endpoint = apiClient.buildOrgPath(`/employees/${employeeId}`)
  return apiClient.patch(endpoint, data)
}

export async function clockIn(employeeId: string, data: {
  siteId: string
  shiftId?: string
}): Promise<ClockResult> {
  const endpoint = apiClient.buildOrgPath(`/employees/${employeeId}/clock-in`)
  return apiClient.post(endpoint, data)
}

export async function clockOut(employeeId: string, data?: {
  notes?: string
}): Promise<ClockResult> {
  const endpoint = apiClient.buildOrgPath(`/employees/${employeeId}/clock-out`)
  return apiClient.post(endpoint, data ?? {})
}

export async function assignRole(employeeId: string, data: {
  roleId: string
  roleName: string
  department: string
  isPrimary?: boolean
  hourlyRateOverride?: number
}): Promise<{ roleId: string; roleName: string; assigned: boolean } & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/employees/${employeeId}/roles`)
  return apiClient.post(endpoint, data)
}

export async function removeRole(employeeId: string, roleId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/employees/${employeeId}/roles/${roleId}`)
  return apiClient.delete(endpoint)
}
