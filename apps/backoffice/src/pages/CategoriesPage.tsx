import { useEffect, useState } from 'react'
import { useMenuCms } from '../contexts/MenuCmsContext'
import type { CreateCategoryRequest } from '../api/menu'

function getStatusBadge(category: { hasDraft: boolean; isArchived: boolean }) {
  if (category.isArchived) return { className: 'badge badge-danger', label: 'Archived' }
  if (category.hasDraft) return { className: 'badge badge-warning', label: 'Draft' }
  return { className: 'badge badge-success', label: 'Active' }
}

export default function CategoriesPage() {
  const { categories, isLoading, error, loadCategories, createCategory } = useMenuCms()
  const [showCreateDialog, setShowCreateDialog] = useState(false)

  useEffect(() => {
    loadCategories()
  }, [])

  const sortedCategories = [...categories].sort((a, b) => a.displayOrder - b.displayOrder)

  return (
    <>
      <hgroup>
        <h1>Categories</h1>
        <p>Organize menu items into categories</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
        <button onClick={() => setShowCreateDialog(true)}>New Category</button>
      </div>

      {showCreateDialog && (
        <CreateCategoryDialog
          nextOrder={sortedCategories.length + 1}
          onSubmit={async (data) => {
            await createCategory(data)
            setShowCreateDialog(false)
          }}
          onCancel={() => setShowCreateDialog(false)}
        />
      )}

      <table aria-busy={isLoading}>
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
          {sortedCategories.map((category) => {
            const badge = getStatusBadge(category)
            return (
              <tr key={category.documentId}>
                <td>{category.displayOrder}</td>
                <td>
                  <strong>{category.name}</strong>
                </td>
                <td>
                  {category.color ? (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      <div
                        style={{
                          width: '24px',
                          height: '24px',
                          borderRadius: '4px',
                          backgroundColor: category.color,
                        }}
                        aria-hidden="true"
                      />
                      <code style={{ fontSize: '0.75rem' }}>{category.color}</code>
                    </div>
                  ) : (
                    '-'
                  )}
                </td>
                <td>{category.itemCount}</td>
                <td>
                  <span className={badge.className}>{badge.label}</span>
                </td>
                <td>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                  >
                    Edit
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {!isLoading && sortedCategories.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No categories found
        </p>
      )}
    </>
  )
}

function CreateCategoryDialog({
  nextOrder,
  onSubmit,
  onCancel,
}: {
  nextOrder: number
  onSubmit: (data: CreateCategoryRequest) => Promise<void>
  onCancel: () => void
}) {
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setIsSubmitting(true)
    const formData = new FormData(e.currentTarget)
    const data: CreateCategoryRequest = {
      name: formData.get('name') as string,
      displayOrder: parseInt(formData.get('displayOrder') as string, 10),
      description: (formData.get('description') as string) || undefined,
      color: (formData.get('color') as string) || undefined,
      publishImmediately: formData.get('publishImmediately') === 'on',
    }
    await onSubmit(data)
    setIsSubmitting(false)
  }

  return (
    <dialog open>
      <article>
        <header>
          <button aria-label="Close" rel="prev" onClick={onCancel} />
          <h3>New Category</h3>
        </header>
        <form onSubmit={handleSubmit}>
          <label>
            Name
            <input type="text" name="name" required autoFocus />
          </label>
          <label>
            Display Order
            <input type="number" name="displayOrder" min="1" defaultValue={nextOrder} required />
          </label>
          <label>
            Description
            <textarea name="description" rows={2} />
          </label>
          <label>
            Color
            <input type="color" name="color" defaultValue="#4CAF50" />
          </label>
          <label>
            <input type="checkbox" name="publishImmediately" />
            Publish immediately
          </label>
          <footer>
            <button type="button" className="secondary" onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" aria-busy={isSubmitting} disabled={isSubmitting}>
              Create Category
            </button>
          </footer>
        </form>
      </article>
    </dialog>
  )
}
