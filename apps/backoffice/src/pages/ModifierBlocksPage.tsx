import { useEffect } from 'react'
import { useMenuCms } from '../contexts/MenuCmsContext'

export default function ModifierBlocksPage() {
  const { modifierBlocks, isLoading, error, loadModifierBlocks } = useMenuCms()

  useEffect(() => {
    loadModifierBlocks()
  }, [])

  return (
    <>
      <hgroup>
        <h1>Modifier Blocks</h1>
        <p>Configure modifier options for menu items</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
        <button>Add Modifier Block</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Selection Rule</th>
            <th>Min</th>
            <th>Max</th>
            <th>Required</th>
            <th>Options</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {modifierBlocks.map((block) => (
            <tr key={block.name}>
              <td>
                <strong>{block.name}</strong>
              </td>
              <td>{block.selectionRule}</td>
              <td>{block.minSelections}</td>
              <td>{block.maxSelections}</td>
              <td>
                <span className={`badge ${block.isRequired ? 'badge-warning' : 'badge-success'}`}>
                  {block.isRequired ? 'Required' : 'Optional'}
                </span>
              </td>
              <td>{block.options.length}</td>
              <td>
                <button
                  className="secondary outline"
                  style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                >
                  Edit
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {!isLoading && modifierBlocks.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No modifier blocks found
        </p>
      )}

      {modifierBlocks.length > 0 && (
        <details>
          <summary>Modifier Options Detail</summary>
          {modifierBlocks.map((block) => (
            <article key={block.name} style={{ marginBottom: '1rem' }}>
              <header>
                <h4>{block.name}</h4>
              </header>
              <table>
                <thead>
                  <tr>
                    <th>Option</th>
                    <th>Price Adjustment</th>
                    <th>Default</th>
                    <th>Order</th>
                    <th>Active</th>
                  </tr>
                </thead>
                <tbody>
                  {block.options
                    .sort((a, b) => a.displayOrder - b.displayOrder)
                    .map((option) => (
                      <tr key={option.optionId}>
                        <td>{option.name}</td>
                        <td>
                          {option.priceAdjustment > 0
                            ? `+${new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(option.priceAdjustment)}`
                            : option.priceAdjustment < 0
                              ? new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(option.priceAdjustment)
                              : '-'}
                        </td>
                        <td>{option.isDefault ? 'Yes' : '-'}</td>
                        <td>{option.displayOrder}</td>
                        <td>
                          <span className={`badge ${option.isActive ? 'badge-success' : 'badge-danger'}`}>
                            {option.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </article>
          ))}
        </details>
      )}
    </>
  )
}
