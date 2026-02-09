import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMenuCms } from '../contexts/MenuCmsContext'
import type { CreateMenuItemRequest } from '../api/menu'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

function getStatusBadge(item: { hasDraft: boolean; isArchived: boolean }) {
  if (item.isArchived) return { className: 'badge badge-danger', label: 'Archived' }
  if (item.hasDraft) return { className: 'badge badge-warning', label: 'Draft' }
  return { className: 'badge badge-success', label: 'Published' }
}

export default function MenuItemsPage() {
  const navigate = useNavigate()
  const { items, categories, isLoading, error, loadItems, loadCategories, createItem } = useMenuCms()
  const [searchTerm, setSearchTerm] = useState('')
  const [showCreateDialog, setShowCreateDialog] = useState(false)

  useEffect(() => {
    loadItems()
    loadCategories()
  }, [])

  const filteredItems = items.filter((item) =>
    item.name.toLowerCase().includes(searchTerm.toLowerCase())
  )

  const categoryMap = new Map(categories.map((c) => [c.documentId, c.name]))

  return (
    <>
      <hgroup>
        <h1>Menu Items</h1>
        <p>Manage your menu items and pricing</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search items..."
          style={{ maxWidth: '300px' }}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          aria-label="Search menu items"
        />
        <button onClick={() => setShowCreateDialog(true)}>Add Item</button>
      </div>

      {showCreateDialog && (
        <CreateItemDialog
          categories={categories}
          onSubmit={async (data) => {
            await createItem(data)
            setShowCreateDialog(false)
          }}
          onCancel={() => setShowCreateDialog(false)}
        />
      )}

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Price</th>
            <th>Category</th>
            <th>Version</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredItems.map((item) => {
            const badge = getStatusBadge(item)
            return (
              <tr key={item.documentId}>
                <td>
                  <strong>{item.name}</strong>
                </td>
                <td>{formatCurrency(item.price)}</td>
                <td>{item.categoryId ? categoryMap.get(item.categoryId) ?? '-' : '-'}</td>
                <td>{item.publishedVersion ?? '-'}</td>
                <td>
                  <span className={badge.className}>{badge.label}</span>
                </td>
                <td>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                    onClick={() => navigate(`/menu/items/${item.documentId}`)}
                  >
                    Edit
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {!isLoading && filteredItems.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No menu items found
        </p>
      )}
    </>
  )
}

function CreateItemDialog({
  categories,
  onSubmit,
  onCancel,
}: {
  categories: { documentId: string; name: string }[]
  onSubmit: (data: CreateMenuItemRequest) => Promise<void>
  onCancel: () => void
}) {
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setIsSubmitting(true)
    const formData = new FormData(e.currentTarget)
    const data: CreateMenuItemRequest = {
      name: formData.get('name') as string,
      price: parseFloat(formData.get('price') as string),
      description: (formData.get('description') as string) || undefined,
      categoryId: (formData.get('categoryId') as string) || undefined,
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
          <h3>Add Menu Item</h3>
        </header>
        <form onSubmit={handleSubmit}>
          <label>
            Name
            <input type="text" name="name" required autoFocus />
          </label>
          <label>
            Price
            <input type="number" name="price" step="0.01" min="0" required />
          </label>
          <label>
            Description
            <textarea name="description" rows={3} />
          </label>
          <label>
            Category
            <select name="categoryId">
              <option value="">No category</option>
              {categories.map((cat) => (
                <option key={cat.documentId} value={cat.documentId}>
                  {cat.name}
                </option>
              ))}
            </select>
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
              Create Item
            </button>
          </footer>
        </form>
      </article>
    </dialog>
  )
}
