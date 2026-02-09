import { describe, it, expect } from 'vitest'
import { channelReducer, initialChannelState, type ChannelState } from './channelReducer'
import type { Channel } from '../api/channels'

function makeChannel(overrides: Partial<Channel> = {}): Channel {
  return {
    channelId: 'ch-1',
    platformType: 'UberEats',
    integrationType: 'Direct',
    name: 'Uber Eats',
    status: 'Active',
    totalOrdersToday: 0,
    totalRevenueToday: 0,
    locations: [],
    _links: { self: { href: '/api/orgs/org-1/channels/ch-1' } },
    ...overrides,
  }
}

describe('channelReducer', () => {
  it('handles CHANNELS_LOADED', () => {
    const channels = [makeChannel(), makeChannel({ channelId: 'ch-2', name: 'DoorDash' })]
    const state = channelReducer(
      { ...initialChannelState, isLoading: true },
      { type: 'CHANNELS_LOADED', payload: { channels } }
    )
    expect(state.channels).toHaveLength(2)
    expect(state.isLoading).toBe(false)
  })

  it('handles CHANNEL_SELECTED and CHANNEL_DESELECTED', () => {
    const channel = makeChannel()
    let state = channelReducer(initialChannelState, { type: 'CHANNEL_SELECTED', payload: { channel } })
    expect(state.selectedChannel).toEqual(channel)

    state = channelReducer(state, { type: 'CHANNEL_DESELECTED' })
    expect(state.selectedChannel).toBeNull()
  })

  it('handles CHANNEL_CONNECTED', () => {
    const channel = makeChannel()
    const state = channelReducer(initialChannelState, { type: 'CHANNEL_CONNECTED', payload: { channel } })
    expect(state.channels).toHaveLength(1)
    expect(state.channels[0].name).toBe('Uber Eats')
  })

  it('handles CHANNEL_UPDATED in list and selected', () => {
    const channel = makeChannel()
    const updated = makeChannel({ name: 'Uber Eats UK' })
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, { type: 'CHANNEL_UPDATED', payload: { channel: updated } })
    expect(state.channels[0].name).toBe('Uber Eats UK')
    expect(state.selectedChannel?.name).toBe('Uber Eats UK')
  })

  it('handles CHANNEL_DISCONNECTED', () => {
    const channel = makeChannel()
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, { type: 'CHANNEL_DISCONNECTED', payload: { channelId: 'ch-1' } })
    expect(state.channels).toHaveLength(0)
    expect(state.selectedChannel).toBeNull()
  })

  it('handles CHANNEL_PAUSED', () => {
    const channel = makeChannel()
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, { type: 'CHANNEL_PAUSED', payload: { channelId: 'ch-1' } })
    expect(state.channels[0].status).toBe('Paused')
    expect(state.selectedChannel?.status).toBe('Paused')
  })

  it('handles CHANNEL_RESUMED', () => {
    const channel = makeChannel({ status: 'Paused' })
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, { type: 'CHANNEL_RESUMED', payload: { channelId: 'ch-1' } })
    expect(state.channels[0].status).toBe('Active')
    expect(state.selectedChannel?.status).toBe('Active')
  })

  it('handles LOCATION_ADDED', () => {
    const channel = makeChannel()
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, {
      type: 'LOCATION_ADDED',
      payload: { channelId: 'ch-1', locationId: 'site-1', externalStoreId: 'ext-1' },
    })
    expect(state.channels[0].locations).toHaveLength(1)
    expect(state.channels[0].locations[0].locationId).toBe('site-1')
    expect(state.selectedChannel?.locations).toHaveLength(1)
  })

  it('handles LOCATION_REMOVED', () => {
    const channel = makeChannel({
      locations: [{ locationId: 'site-1', externalStoreId: 'ext-1', isActive: true }],
    })
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, {
      type: 'LOCATION_REMOVED',
      payload: { channelId: 'ch-1', locationId: 'site-1' },
    })
    expect(state.channels[0].locations).toHaveLength(0)
    expect(state.selectedChannel?.locations).toHaveLength(0)
  })

  it('handles MENU_SYNC_TRIGGERED', () => {
    const channel = makeChannel()
    const initial: ChannelState = {
      ...initialChannelState,
      channels: [channel],
      selectedChannel: channel,
    }
    const state = channelReducer(initial, { type: 'MENU_SYNC_TRIGGERED', payload: { channelId: 'ch-1' } })
    expect(state.channels[0].lastSyncAt).toBeDefined()
    expect(state.selectedChannel?.lastSyncAt).toBeDefined()
  })

  it('handles LOADING_FAILED', () => {
    const state = channelReducer(
      { ...initialChannelState, isLoading: true },
      { type: 'LOADING_FAILED', payload: { error: 'Connection refused' } }
    )
    expect(state.isLoading).toBe(false)
    expect(state.error).toBe('Connection refused')
  })
})
