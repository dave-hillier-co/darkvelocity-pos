import { useEffect, useState } from 'react'
import { useMenuCms } from '../contexts/MenuCmsContext'
import * as menuApi from '../api/menu'

export default function ContentTagsPage() {
  const { contentTags, isLoading, error, loadTags } = useMenuCms()
  const [categoryFilter, setCategoryFilter] = useState<string>('all')
  const [showCreateDialog, setShowCreateDialog] = useState(false)

  useEffect(() => {
    loadTags()
  }, [])

  const tagCategories = [...new Set(contentTags.map((t) => t.category))]

  const filteredTags = categoryFilter === 'all'
    ? contentTags
    : contentTags.filter((t) => t.category === categoryFilter)

  const sortedTags = [...filteredTags].sort((a, b) => a.displayOrder - b.displayOrder)

  return (
    <>
      <hgroup>
        <h1>Content Tags</h1>
        <p>Manage tags for menu items (dietary, allergens, etc.)</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <select
          value={categoryFilter}
          onChange={(e) => setCategoryFilter(e.target.value)}
          style={{ maxWidth: '200px' }}
          aria-label="Filter by category"
        >
          <option value="all">All Categories</option>
          {tagCategories.map((cat) => (
            <option key={cat} value={cat}>{cat}</option>
          ))}
        </select>
        <button onClick={() => setShowCreateDialog(true)}>Add Tag</button>
      </div>

      {showCreateDialog && (
        <CreateTagDialog
          onSubmit={async (data) => {
            await menuApi.createContentTag(data)
            setShowCreateDialog(false)
            loadTags()
          }}
          onCancel={() => setShowCreateDialog(false)}
        />
      )}

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Category</th>
            <th>Color</th>
            <th>Order</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {sortedTags.map((tag) => (
            <tr key={tag.tagId}>
              <td>
                <strong>{tag.name}</strong>
                {tag.externalPlatform && (
                  <>
                    <br />
                    <small style={{ color: 'var(--pico-muted-color)' }}>
                      {tag.externalPlatform}: {tag.externalTagId}
                    </small>
                  </>
                )}
              </td>
              <td>{tag.category}</td>
              <td>
                {tag.badgeColor ? (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <div
                      style={{
                        width: '20px',
                        height: '20px',
                        borderRadius: '4px',
                        backgroundColor: tag.badgeColor,
                      }}
                      aria-hidden="true"
                    />
                    <code style={{ fontSize: '0.75rem' }}>{tag.badgeColor}</code>
                  </div>
                ) : (
                  '-'
                )}
              </td>
              <td>{tag.displayOrder}</td>
              <td>
                <span className={`badge ${tag.isActive ? 'badge-success' : 'badge-danger'}`}>
                  {tag.isActive ? 'Active' : 'Inactive'}
                </span>
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
          ))}
        </tbody>
      </table>

      {!isLoading && sortedTags.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No content tags found
        </p>
      )}
    </>
  )
}

function CreateTagDialog({
  onSubmit,
  onCancel,
}: {
  onSubmit: (data: menuApi.CreateContentTagRequest) => Promise<void>
  onCancel: () => void
}) {
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setIsSubmitting(true)
    const formData = new FormData(e.currentTarget)
    const data: menuApi.CreateContentTagRequest = {
      name: formData.get('name') as string,
      category: formData.get('category') as string,
      badgeColor: (formData.get('badgeColor') as string) || undefined,
      displayOrder: parseInt(formData.get('displayOrder') as string, 10) || 0,
    }
    await onSubmit(data)
    setIsSubmitting(false)
  }

  return (
    <dialog open>
      <article>
        <header>
          <button aria-label="Close" rel="prev" onClick={onCancel} />
          <h3>Add Content Tag</h3>
        </header>
        <form onSubmit={handleSubmit}>
          <label>
            Name
            <input type="text" name="name" required autoFocus />
          </label>
          <label>
            Category
            <input type="text" name="category" placeholder="e.g. dietary, allergen, cuisine" required />
          </label>
          <label>
            Badge Color
            <input type="color" name="badgeColor" defaultValue="#4CAF50" />
          </label>
          <label>
            Display Order
            <input type="number" name="displayOrder" min="0" defaultValue="0" />
          </label>
          <footer>
            <button type="button" className="secondary" onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" aria-busy={isSubmitting} disabled={isSubmitting}>
              Create Tag
            </button>
          </footer>
        </form>
      </article>
    </dialog>
  )
}
