import { createContext, useContext, useReducer, type ReactNode } from 'react'
import {
  procurementReducer,
  initialProcurementState,
  type ProcurementState,
  type ProcurementAction,
  type PurchaseDocument,
} from '../reducers/procurementReducer'
import * as procurementApi from '../api/procurement'
import type { Supplier } from '../types'

interface ProcurementContextValue extends ProcurementState {
  loadSuppliers: () => Promise<void>
  createSupplier: (data: {
    code: string
    name: string
    contactEmail?: string
    paymentTermsDays?: number
    leadTimeDays?: number
  }) => Promise<void>
  loadPurchaseDocuments: () => Promise<void>
  uploadDocument: (file: File, type?: string) => Promise<void>
  confirmDocument: (documentId: string, data: {
    confirmedBy: string
    vendorId?: string
    vendorName?: string
    documentDate?: string
    currency?: string
  }) => Promise<void>
  rejectDocument: (documentId: string, rejectedBy: string, reason: string) => Promise<void>
  selectDocument: (document: PurchaseDocument | null) => void
  loadDeliveries: () => Promise<void>
  acceptDelivery: (deliveryId: string) => Promise<void>
  rejectDelivery: (deliveryId: string, reason: string) => Promise<void>
  setStatusFilter: (filter: string) => void
  dispatch: React.Dispatch<ProcurementAction>
}

const ProcurementContext = createContext<ProcurementContextValue | null>(null)

export function ProcurementProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(procurementReducer, initialProcurementState)

  async function loadSuppliers() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await procurementApi.getSuppliers()
      dispatch({
        type: 'SUPPLIERS_LOADED',
        payload: { suppliers: result._embedded.items as Supplier[] },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load suppliers' },
      })
    }
  }

  async function createSupplier(data: {
    code: string
    name: string
    contactEmail?: string
    paymentTermsDays?: number
    leadTimeDays?: number
  }) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const supplier = await procurementApi.createSupplier(data)
      dispatch({ type: 'SUPPLIER_CREATED', payload: { supplier: supplier as Supplier } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to create supplier' },
      })
    }
  }

  async function loadPurchaseDocuments() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await procurementApi.getPurchaseDocuments()
      dispatch({
        type: 'PURCHASE_DOCUMENTS_LOADED',
        payload: { documents: result._embedded.items },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load purchase documents' },
      })
    }
  }

  async function uploadDocument(file: File, type?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const document = await procurementApi.uploadPurchaseDocument(file, type)
      dispatch({ type: 'DOCUMENT_UPLOADED', payload: { document } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to upload document' },
      })
    }
  }

  async function confirmDocument(documentId: string, data: {
    confirmedBy: string
    vendorId?: string
    vendorName?: string
    documentDate?: string
    currency?: string
  }) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const document = await procurementApi.confirmPurchaseDocument(documentId, data)
      dispatch({ type: 'DOCUMENT_CONFIRMED', payload: { document } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to confirm document' },
      })
    }
  }

  async function rejectDocument(documentId: string, rejectedBy: string, reason: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await procurementApi.rejectPurchaseDocument(documentId, rejectedBy, reason)
      dispatch({ type: 'DOCUMENT_REJECTED', payload: { documentId } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to reject document' },
      })
    }
  }

  function selectDocument(document: PurchaseDocument | null) {
    dispatch({ type: 'DOCUMENT_SELECTED', payload: { document } })
  }

  async function loadDeliveries() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await procurementApi.getDeliveries()
      dispatch({
        type: 'DELIVERIES_LOADED',
        payload: { deliveries: result._embedded.items },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load deliveries' },
      })
    }
  }

  async function acceptDelivery(deliveryId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await procurementApi.acceptDelivery(deliveryId)
      dispatch({ type: 'DELIVERY_ACCEPTED', payload: { deliveryId } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to accept delivery' },
      })
    }
  }

  async function rejectDelivery(deliveryId: string, reason: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await procurementApi.rejectDelivery(deliveryId, reason)
      dispatch({ type: 'DELIVERY_REJECTED', payload: { deliveryId } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to reject delivery' },
      })
    }
  }

  function setStatusFilter(filter: string) {
    dispatch({ type: 'STATUS_FILTER_CHANGED', payload: { filter } })
  }

  return (
    <ProcurementContext.Provider
      value={{
        ...state,
        loadSuppliers,
        createSupplier,
        loadPurchaseDocuments,
        uploadDocument,
        confirmDocument,
        rejectDocument,
        selectDocument,
        loadDeliveries,
        acceptDelivery,
        rejectDelivery,
        setStatusFilter,
        dispatch,
      }}
    >
      {children}
    </ProcurementContext.Provider>
  )
}

export function useProcurement() {
  const context = useContext(ProcurementContext)
  if (!context) {
    throw new Error('useProcurement must be used within a ProcurementProvider')
  }
  return context
}
