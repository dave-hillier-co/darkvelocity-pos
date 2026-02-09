import type { Supplier } from '../types'

export interface PurchaseDocument {
  id: string
  documentType: string
  source: string
  status: string
  fileName: string
  supplierName: string | null
  documentDate: string | null
  totalAmount: number | null
  lineCount: number
  createdAt: string
  _links: Record<string, { href: string }>
}

export interface Delivery {
  id: string
  deliveryNumber: string
  supplierId: string
  supplierName: string
  purchaseOrderId: string | null
  status: 'pending' | 'accepted' | 'rejected'
  totalValue: number
  hasDiscrepancies: boolean
  lineCount: number
  receivedAt: string
  _links: Record<string, { href: string }>
}

export interface ProcurementState {
  suppliers: Supplier[]
  purchaseDocuments: PurchaseDocument[]
  deliveries: Delivery[]
  selectedDocument: PurchaseDocument | null
  isLoading: boolean
  error: string | null
  statusFilter: string
}

export type ProcurementAction =
  | { type: 'SUPPLIERS_LOADED'; payload: { suppliers: Supplier[] } }
  | { type: 'SUPPLIER_CREATED'; payload: { supplier: Supplier } }
  | { type: 'PURCHASE_DOCUMENTS_LOADED'; payload: { documents: PurchaseDocument[] } }
  | { type: 'DOCUMENT_UPLOADED'; payload: { document: PurchaseDocument } }
  | { type: 'DOCUMENT_CONFIRMED'; payload: { document: PurchaseDocument } }
  | { type: 'DOCUMENT_REJECTED'; payload: { documentId: string } }
  | { type: 'DOCUMENT_SELECTED'; payload: { document: PurchaseDocument | null } }
  | { type: 'DELIVERIES_LOADED'; payload: { deliveries: Delivery[] } }
  | { type: 'DELIVERY_ACCEPTED'; payload: { deliveryId: string } }
  | { type: 'DELIVERY_REJECTED'; payload: { deliveryId: string } }
  | { type: 'STATUS_FILTER_CHANGED'; payload: { filter: string } }
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }

export const initialProcurementState: ProcurementState = {
  suppliers: [],
  purchaseDocuments: [],
  deliveries: [],
  selectedDocument: null,
  isLoading: false,
  error: null,
  statusFilter: 'all',
}

export function procurementReducer(state: ProcurementState, action: ProcurementAction): ProcurementState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'SUPPLIERS_LOADED':
      return { ...state, isLoading: false, suppliers: action.payload.suppliers }

    case 'SUPPLIER_CREATED':
      return {
        ...state,
        isLoading: false,
        suppliers: [...state.suppliers, action.payload.supplier],
      }

    case 'PURCHASE_DOCUMENTS_LOADED':
      return { ...state, isLoading: false, purchaseDocuments: action.payload.documents }

    case 'DOCUMENT_UPLOADED':
      return {
        ...state,
        isLoading: false,
        purchaseDocuments: [...state.purchaseDocuments, action.payload.document],
      }

    case 'DOCUMENT_CONFIRMED':
      return {
        ...state,
        isLoading: false,
        purchaseDocuments: state.purchaseDocuments.map((doc) =>
          doc.id === action.payload.document.id ? action.payload.document : doc
        ),
        selectedDocument:
          state.selectedDocument?.id === action.payload.document.id
            ? action.payload.document
            : state.selectedDocument,
      }

    case 'DOCUMENT_REJECTED':
      return {
        ...state,
        isLoading: false,
        purchaseDocuments: state.purchaseDocuments.filter(
          (doc) => doc.id !== action.payload.documentId
        ),
        selectedDocument:
          state.selectedDocument?.id === action.payload.documentId
            ? null
            : state.selectedDocument,
      }

    case 'DOCUMENT_SELECTED':
      return { ...state, selectedDocument: action.payload.document }

    case 'DELIVERIES_LOADED':
      return { ...state, isLoading: false, deliveries: action.payload.deliveries }

    case 'DELIVERY_ACCEPTED':
      return {
        ...state,
        isLoading: false,
        deliveries: state.deliveries.map((d) =>
          d.id === action.payload.deliveryId ? { ...d, status: 'accepted' as const } : d
        ),
      }

    case 'DELIVERY_REJECTED':
      return {
        ...state,
        isLoading: false,
        deliveries: state.deliveries.map((d) =>
          d.id === action.payload.deliveryId ? { ...d, status: 'rejected' as const } : d
        ),
      }

    case 'STATUS_FILTER_CHANGED':
      return { ...state, statusFilter: action.payload.filter }

    default:
      return state
  }
}
