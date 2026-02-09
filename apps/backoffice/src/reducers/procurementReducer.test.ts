import { describe, it, expect } from 'vitest'
import {
  procurementReducer,
  initialProcurementState,
  type ProcurementState,
  type PurchaseDocument,
  type Delivery,
} from './procurementReducer'
import type { Supplier } from '../types'

function makeSupplier(overrides: Partial<Supplier> = {}): Supplier {
  return {
    id: 'sup-1',
    code: 'SUP001',
    name: 'Test Supplier',
    contactEmail: 'test@supplier.com',
    paymentTermsDays: 30,
    leadTimeDays: 5,
    isActive: true,
    ...overrides,
  }
}

function makeDocument(overrides: Partial<PurchaseDocument> = {}): PurchaseDocument {
  return {
    id: 'doc-1',
    documentType: 'Invoice',
    source: 'Upload',
    status: 'pending_review',
    fileName: 'invoice-001.pdf',
    supplierName: 'Test Supplier',
    documentDate: '2026-01-15',
    totalAmount: 500,
    lineCount: 3,
    createdAt: '2026-01-15T10:00:00Z',
    _links: { self: { href: '/api/orgs/org1/sites/site1/purchases/doc-1' } },
    ...overrides,
  }
}

function makeDelivery(overrides: Partial<Delivery> = {}): Delivery {
  return {
    id: 'del-1',
    deliveryNumber: 'DEL-001',
    supplierId: 'sup-1',
    supplierName: 'Test Supplier',
    purchaseOrderId: null,
    status: 'pending',
    totalValue: 250,
    hasDiscrepancies: false,
    lineCount: 2,
    receivedAt: '2026-01-16T09:00:00Z',
    _links: { self: { href: '/deliveries/del-1' } },
    ...overrides,
  }
}

describe('procurementReducer', () => {
  it('returns initial state for unknown action', () => {
    const state = procurementReducer(initialProcurementState, { type: 'UNKNOWN' } as never)
    expect(state).toBe(initialProcurementState)
  })

  describe('LOADING_STARTED', () => {
    it('sets isLoading to true and clears error', () => {
      const prev: ProcurementState = { ...initialProcurementState, error: 'old error' }
      const state = procurementReducer(prev, { type: 'LOADING_STARTED' })
      expect(state.isLoading).toBe(true)
      expect(state.error).toBeNull()
    })
  })

  describe('LOADING_FAILED', () => {
    it('sets isLoading to false and sets error', () => {
      const prev: ProcurementState = { ...initialProcurementState, isLoading: true }
      const state = procurementReducer(prev, {
        type: 'LOADING_FAILED',
        payload: { error: 'Network error' },
      })
      expect(state.isLoading).toBe(false)
      expect(state.error).toBe('Network error')
    })
  })

  describe('SUPPLIERS_LOADED', () => {
    it('replaces suppliers and clears loading', () => {
      const suppliers = [makeSupplier(), makeSupplier({ id: 'sup-2', name: 'Other' })]
      const prev: ProcurementState = { ...initialProcurementState, isLoading: true }
      const state = procurementReducer(prev, {
        type: 'SUPPLIERS_LOADED',
        payload: { suppliers },
      })
      expect(state.suppliers).toHaveLength(2)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('SUPPLIER_CREATED', () => {
    it('appends supplier to list', () => {
      const existing = makeSupplier({ id: 'sup-1' })
      const newSupplier = makeSupplier({ id: 'sup-2', name: 'New Supplier' })
      const prev: ProcurementState = {
        ...initialProcurementState,
        suppliers: [existing],
        isLoading: true,
      }
      const state = procurementReducer(prev, {
        type: 'SUPPLIER_CREATED',
        payload: { supplier: newSupplier },
      })
      expect(state.suppliers).toHaveLength(2)
      expect(state.suppliers[1].name).toBe('New Supplier')
      expect(state.isLoading).toBe(false)
    })
  })

  describe('PURCHASE_DOCUMENTS_LOADED', () => {
    it('replaces purchase documents list', () => {
      const documents = [makeDocument(), makeDocument({ id: 'doc-2' })]
      const state = procurementReducer(
        { ...initialProcurementState, isLoading: true },
        { type: 'PURCHASE_DOCUMENTS_LOADED', payload: { documents } }
      )
      expect(state.purchaseDocuments).toHaveLength(2)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('DOCUMENT_UPLOADED', () => {
    it('appends document to list', () => {
      const doc = makeDocument()
      const state = procurementReducer(initialProcurementState, {
        type: 'DOCUMENT_UPLOADED',
        payload: { document: doc },
      })
      expect(state.purchaseDocuments).toHaveLength(1)
      expect(state.purchaseDocuments[0].id).toBe('doc-1')
    })
  })

  describe('DOCUMENT_CONFIRMED', () => {
    it('updates existing document in list', () => {
      const doc = makeDocument()
      const confirmedDoc = { ...doc, status: 'confirmed' }
      const prev: ProcurementState = {
        ...initialProcurementState,
        purchaseDocuments: [doc],
      }
      const state = procurementReducer(prev, {
        type: 'DOCUMENT_CONFIRMED',
        payload: { document: confirmedDoc },
      })
      expect(state.purchaseDocuments[0].status).toBe('confirmed')
    })

    it('updates selectedDocument when it matches', () => {
      const doc = makeDocument()
      const confirmedDoc = { ...doc, status: 'confirmed' }
      const prev: ProcurementState = {
        ...initialProcurementState,
        purchaseDocuments: [doc],
        selectedDocument: doc,
      }
      const state = procurementReducer(prev, {
        type: 'DOCUMENT_CONFIRMED',
        payload: { document: confirmedDoc },
      })
      expect(state.selectedDocument?.status).toBe('confirmed')
    })
  })

  describe('DOCUMENT_REJECTED', () => {
    it('removes document from list', () => {
      const doc = makeDocument()
      const prev: ProcurementState = {
        ...initialProcurementState,
        purchaseDocuments: [doc],
      }
      const state = procurementReducer(prev, {
        type: 'DOCUMENT_REJECTED',
        payload: { documentId: 'doc-1' },
      })
      expect(state.purchaseDocuments).toHaveLength(0)
    })

    it('clears selectedDocument when it matches', () => {
      const doc = makeDocument()
      const prev: ProcurementState = {
        ...initialProcurementState,
        purchaseDocuments: [doc],
        selectedDocument: doc,
      }
      const state = procurementReducer(prev, {
        type: 'DOCUMENT_REJECTED',
        payload: { documentId: 'doc-1' },
      })
      expect(state.selectedDocument).toBeNull()
    })
  })

  describe('DOCUMENT_SELECTED', () => {
    it('sets selectedDocument', () => {
      const doc = makeDocument()
      const state = procurementReducer(initialProcurementState, {
        type: 'DOCUMENT_SELECTED',
        payload: { document: doc },
      })
      expect(state.selectedDocument).toBe(doc)
    })

    it('clears selectedDocument with null', () => {
      const prev: ProcurementState = {
        ...initialProcurementState,
        selectedDocument: makeDocument(),
      }
      const state = procurementReducer(prev, {
        type: 'DOCUMENT_SELECTED',
        payload: { document: null },
      })
      expect(state.selectedDocument).toBeNull()
    })
  })

  describe('DELIVERIES_LOADED', () => {
    it('replaces deliveries list', () => {
      const deliveries = [makeDelivery(), makeDelivery({ id: 'del-2' })]
      const state = procurementReducer(
        { ...initialProcurementState, isLoading: true },
        { type: 'DELIVERIES_LOADED', payload: { deliveries } }
      )
      expect(state.deliveries).toHaveLength(2)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('DELIVERY_ACCEPTED', () => {
    it('updates delivery status to accepted', () => {
      const delivery = makeDelivery()
      const prev: ProcurementState = {
        ...initialProcurementState,
        deliveries: [delivery],
      }
      const state = procurementReducer(prev, {
        type: 'DELIVERY_ACCEPTED',
        payload: { deliveryId: 'del-1' },
      })
      expect(state.deliveries[0].status).toBe('accepted')
    })

    it('does not change other deliveries', () => {
      const deliveries = [makeDelivery({ id: 'del-1' }), makeDelivery({ id: 'del-2' })]
      const prev: ProcurementState = { ...initialProcurementState, deliveries }
      const state = procurementReducer(prev, {
        type: 'DELIVERY_ACCEPTED',
        payload: { deliveryId: 'del-1' },
      })
      expect(state.deliveries[0].status).toBe('accepted')
      expect(state.deliveries[1].status).toBe('pending')
    })
  })

  describe('DELIVERY_REJECTED', () => {
    it('updates delivery status to rejected', () => {
      const delivery = makeDelivery()
      const prev: ProcurementState = {
        ...initialProcurementState,
        deliveries: [delivery],
      }
      const state = procurementReducer(prev, {
        type: 'DELIVERY_REJECTED',
        payload: { deliveryId: 'del-1' },
      })
      expect(state.deliveries[0].status).toBe('rejected')
    })
  })

  describe('STATUS_FILTER_CHANGED', () => {
    it('updates status filter', () => {
      const state = procurementReducer(initialProcurementState, {
        type: 'STATUS_FILTER_CHANGED',
        payload: { filter: 'pending' },
      })
      expect(state.statusFilter).toBe('pending')
    })
  })
})
