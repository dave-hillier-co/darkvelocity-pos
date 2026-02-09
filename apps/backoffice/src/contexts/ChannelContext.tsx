import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { channelReducer, initialChannelState, type ChannelState, type ChannelAction } from '../reducers/channelReducer'
import * as channelApi from '../api/channels'
import type { Channel } from '../api/channels'

interface ChannelContextValue extends ChannelState {
  loadChannels: (channels: Channel[]) => void
  selectChannel: (channel: Channel) => void
  deselectChannel: () => void
  connectChannel: (data: Parameters<typeof channelApi.connectChannel>[0]) => Promise<void>
  updateChannel: (channelId: string, data: Parameters<typeof channelApi.updateChannel>[1]) => Promise<void>
  disconnectChannel: (channelId: string) => Promise<void>
  pauseOrders: (channelId: string, reason?: string) => Promise<void>
  resumeOrders: (channelId: string) => Promise<void>
  addLocation: (channelId: string, data: Parameters<typeof channelApi.addLocation>[1]) => Promise<void>
  removeLocation: (channelId: string, locationId: string) => Promise<void>
  triggerMenuSync: (channelId: string, locationId?: string) => Promise<void>
  dispatch: React.Dispatch<ChannelAction>
}

const ChannelContext = createContext<ChannelContextValue | null>(null)

export function ChannelProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(channelReducer, initialChannelState)

  function loadChannels(channels: Channel[]) {
    dispatch({ type: 'CHANNELS_LOADED', payload: { channels } })
  }

  function selectChannel(channel: Channel) {
    dispatch({ type: 'CHANNEL_SELECTED', payload: { channel } })
  }

  function deselectChannel() {
    dispatch({ type: 'CHANNEL_DESELECTED' })
  }

  async function connectChannel(data: Parameters<typeof channelApi.connectChannel>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const channel = await channelApi.connectChannel(data)
      dispatch({ type: 'CHANNEL_CONNECTED', payload: { channel } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function updateChannel(channelId: string, data: Parameters<typeof channelApi.updateChannel>[1]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const channel = await channelApi.updateChannel(channelId, data)
      dispatch({ type: 'CHANNEL_UPDATED', payload: { channel } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function disconnectChannel(channelId: string) {
    try {
      await channelApi.disconnectChannel(channelId)
      dispatch({ type: 'CHANNEL_DISCONNECTED', payload: { channelId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function pauseOrders(channelId: string, reason?: string) {
    try {
      await channelApi.pauseOrders(channelId, reason)
      dispatch({ type: 'CHANNEL_PAUSED', payload: { channelId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function resumeOrders(channelId: string) {
    try {
      await channelApi.resumeOrders(channelId)
      dispatch({ type: 'CHANNEL_RESUMED', payload: { channelId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function addLocation(channelId: string, data: Parameters<typeof channelApi.addLocation>[1]) {
    try {
      await channelApi.addLocation(channelId, data)
      dispatch({
        type: 'LOCATION_ADDED',
        payload: { channelId, locationId: data.locationId, externalStoreId: data.externalStoreId },
      })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function removeLocation(channelId: string, locationId: string) {
    try {
      await channelApi.removeLocation(channelId, locationId)
      dispatch({ type: 'LOCATION_REMOVED', payload: { channelId, locationId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function triggerMenuSync(channelId: string, locationId?: string) {
    try {
      await channelApi.triggerMenuSync(channelId, locationId)
      dispatch({ type: 'MENU_SYNC_TRIGGERED', payload: { channelId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <ChannelContext.Provider
      value={{
        ...state,
        loadChannels,
        selectChannel,
        deselectChannel,
        connectChannel,
        updateChannel,
        disconnectChannel,
        pauseOrders,
        resumeOrders,
        addLocation,
        removeLocation,
        triggerMenuSync,
        dispatch,
      }}
    >
      {children}
    </ChannelContext.Provider>
  )
}

export function useChannels() {
  const context = useContext(ChannelContext)
  if (!context) {
    throw new Error('useChannels must be used within a ChannelProvider')
  }
  return context
}
