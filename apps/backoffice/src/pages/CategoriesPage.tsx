import { useState } from 'react'

interface Category {
  id: string
  name: string
  displayOrder: number
  color: string
  itemCount: number
  isActive: boolean
}

const sampleCategories: Category[] = [
  { id: '1', name: 'Starters', displayOrder: 1, color: '#4CAF50', itemCount: 8, isActive: true },
  { id: '2', name: 'Mains', displayOrder: 2, color: '#2196F3', itemCount: 15, isActive: true },
  { id: '3', name: 'Desserts', displayOrder: 3, color: '#E91E63', itemCount: 6, isActive: true },
  { id: '4', name: 'Drinks', displayOrder: 4, color: '#FF9800', itemCount: 12, isActive: true },
  { id: '5', name: 'Sides', displayOrder: 5, color: '#9C27B0', itemCount: 7, isActive: true },
  { id: '6', name: 'Specials', displayOrder: 6, color: '#607D8B', itemCount: 3, isActive: false },
]

export default function CategoriesPage() {
  const [categories, setCategories] = useState(sampleCategories)
  const [editingId, setEditingId] = useState<string | null>(null)

  function handleMoveUp(id: string) {
    const index = categories.findIndex((c) => c.id === id)
    if (index <= 0) return
    const newCategories = [...categories]
    const temp = newCategories[index - 1].displayOrder
    newCategories[index - 1].displayOrder = newCategories[index].displayOrder
    newCategories[index].displayOrder = temp
    newCategories.sort((a, b) => a.displayOrder - b.displayOrder)
    setCategories(newCategories)
  }

  function handleMoveDown(id: string) {
    const index = categories.findIndex((c) => c.id === id)
    if (index >= categories.length - 1) return
    const newCategories = [...categories]
    const temp = newCategories[index + 1].displayOrder
    newCategories[index + 1].displayOrder = newCategories[index].displayOrder
    newCategories[index].displayOrder = temp
    newCategories.sort((a, b) => a.displayOrder - b.displayOrder)
    setCategories(newCategories)
  }

  return (
    <>
      <hgroup>
        <h1>Categories</h1>
        <p>Organize menu items into categories</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
        <button>New Category</button>
      </div>

      <table>
        <thead>
          <tr>
            <th style={{ width: '50px' }}>Order</th>
            <th>Name</th>
            <th>Color</th>
            <th>Items</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {categories.map((category, index) => (
            <tr key={category.id}>
              <td>
                <div style={{ display: 'flex', gap: '0.25rem' }}>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.125rem 0.25rem', fontSize: '0.75rem' }}
                    onClick={() => handleMoveUp(category.id)}
                    disabled={index === 0}
                  >
                    ^
                  </button>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.125rem 0.25rem', fontSize: '0.75rem' }}
                    onClick={() => handleMoveDown(category.id)}
                    disabled={index === categories.length - 1}
                  >
                    v
                  </button>
                </div>
              </td>
              <td>
                {editingId === category.id ? (
                  <input
                    type="text"
                    defaultValue={category.name}
                    style={{ margin: 0, padding: '0.25rem' }}
                    autoFocus
                    onBlur={() => setEditingId(null)}
                  />
                ) : (
                  <strong>{category.name}</strong>
                )}
              </td>
              <td>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <div
                    style={{
                      width: '24px',
                      height: '24px',
                      borderRadius: '4px',
                      backgroundColor: category.color,
                    }}
                  />
                  <code style={{ fontSize: '0.75rem' }}>{category.color}</code>
                </div>
              </td>
              <td>{category.itemCount}</td>
              <td>
                <span className={`badge ${category.isActive ? 'badge-success' : 'badge-danger'}`}>
                  {category.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                    onClick={() => setEditingId(category.id)}
                  >
                    Edit
                  </button>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                  >
                    {category.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {categories.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No categories found
        </p>
      )}
    </>
  )
}
