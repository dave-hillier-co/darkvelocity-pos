import type { Channel, ChannelStatus } from '../api/channels'

export type ChannelAction =
  | { type: 'CHANNELS_LOADED'; payload: { channels: Channel[] } }
  | { type: 'CHANNEL_SELECTED'; payload: { channel: Channel } }
  | { type: 'CHANNEL_DESELECTED' }
  | { type: 'CHANNEL_CONNECTED'; payload: { channel: Channel } }
  | { type: 'CHANNEL_UPDATED'; payload: { channel: Channel } }
  | { type: 'CHANNEL_DISCONNECTED'; payload: { channelId: string } }
  | { type: 'CHANNEL_PAUSED'; payload: { channelId: string } }
  | { type: 'CHANNEL_RESUMED'; payload: { channelId: string } }
  | { type: 'LOCATION_ADDED'; payload: { channelId: string; locationId: string; externalStoreId: string } }
  | { type: 'LOCATION_REMOVED'; payload: { channelId: string; locationId: string } }
  | { type: 'MENU_SYNC_TRIGGERED'; payload: { channelId: string } }
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }

export interface ChannelState {
  channels: Channel[]
  selectedChannel: Channel | null
  isLoading: boolean
  error: string | null
}

export const initialChannelState: ChannelState = {
  channels: [],
  selectedChannel: null,
  isLoading: false,
  error: null,
}

function updateChannelInList(channels: Channel[], channelId: string, update: Partial<Channel>): Channel[] {
  return channels.map(c =>
    c.channelId === channelId ? { ...c, ...update } : c
  )
}

export function channelReducer(state: ChannelState, action: ChannelAction): ChannelState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'CHANNELS_LOADED':
      return { ...state, channels: action.payload.channels, isLoading: false, error: null }

    case 'CHANNEL_SELECTED':
      return { ...state, selectedChannel: action.payload.channel }

    case 'CHANNEL_DESELECTED':
      return { ...state, selectedChannel: null }

    case 'CHANNEL_CONNECTED':
      return {
        ...state,
        channels: [...state.channels, action.payload.channel],
        isLoading: false,
      }

    case 'CHANNEL_UPDATED': {
      const updated = action.payload.channel
      return {
        ...state,
        channels: updateChannelInList(state.channels, updated.channelId, updated),
        selectedChannel: state.selectedChannel?.channelId === updated.channelId ? updated : state.selectedChannel,
        isLoading: false,
      }
    }

    case 'CHANNEL_DISCONNECTED': {
      const { channelId } = action.payload
      return {
        ...state,
        channels: state.channels.filter(c => c.channelId !== channelId),
        selectedChannel: state.selectedChannel?.channelId === channelId ? null : state.selectedChannel,
      }
    }

    case 'CHANNEL_PAUSED': {
      const { channelId } = action.payload
      const status: ChannelStatus = 'Paused'
      return {
        ...state,
        channels: updateChannelInList(state.channels, channelId, { status }),
        selectedChannel: state.selectedChannel?.channelId === channelId
          ? { ...state.selectedChannel, status }
          : state.selectedChannel,
      }
    }

    case 'CHANNEL_RESUMED': {
      const { channelId } = action.payload
      const status: ChannelStatus = 'Active'
      return {
        ...state,
        channels: updateChannelInList(state.channels, channelId, { status }),
        selectedChannel: state.selectedChannel?.channelId === channelId
          ? { ...state.selectedChannel, status }
          : state.selectedChannel,
      }
    }

    case 'LOCATION_ADDED': {
      const { channelId, locationId, externalStoreId } = action.payload
      return {
        ...state,
        channels: state.channels.map(c =>
          c.channelId === channelId
            ? { ...c, locations: [...c.locations, { locationId, externalStoreId, isActive: true }] }
            : c
        ),
        selectedChannel: state.selectedChannel?.channelId === channelId
          ? { ...state.selectedChannel, locations: [...state.selectedChannel.locations, { locationId, externalStoreId, isActive: true }] }
          : state.selectedChannel,
      }
    }

    case 'LOCATION_REMOVED': {
      const { channelId, locationId } = action.payload
      return {
        ...state,
        channels: state.channels.map(c =>
          c.channelId === channelId
            ? { ...c, locations: c.locations.filter(l => l.locationId !== locationId) }
            : c
        ),
        selectedChannel: state.selectedChannel?.channelId === channelId
          ? { ...state.selectedChannel, locations: state.selectedChannel.locations.filter(l => l.locationId !== locationId) }
          : state.selectedChannel,
      }
    }

    case 'MENU_SYNC_TRIGGERED': {
      const { channelId } = action.payload
      const lastSyncAt = new Date().toISOString()
      return {
        ...state,
        channels: updateChannelInList(state.channels, channelId, { lastSyncAt }),
        selectedChannel: state.selectedChannel?.channelId === channelId
          ? { ...state.selectedChannel, lastSyncAt }
          : state.selectedChannel,
      }
    }

    default:
      return state
  }
}
