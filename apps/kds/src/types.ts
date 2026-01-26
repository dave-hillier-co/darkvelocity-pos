export type StationType = 'prep' | 'expo'
export type TicketLayout = 'tiled' | 'classic'
export type OrderType = 'for_here' | 'takeout' | 'delivery'
export type TicketStatus = 'pending' | 'in_progress' | 'completed'
export type LineItemStatus = 'pending' | 'completed'
export type TicketUrgency = 'normal' | 'warning' | 'critical'
export type KdsView = 'open' | 'completed'

export interface StationSettings {
  id: string
  name: string
  type: StationType
  layout: TicketLayout
  yellowThresholdSeconds: number
  redThresholdSeconds: number
}

export interface TicketLineItem {
  id: string
  itemName: string
  quantity: number
  modifiers: string[]
  status: LineItemStatus
  completedAt?: string
}

export interface Ticket {
  id: string
  orderNumber: string
  orderType: OrderType
  customerName?: string
  tableName?: string
  status: TicketStatus
  items: TicketLineItem[]
  createdAt: string
  completedAt?: string
  isPrioritized: boolean
}
