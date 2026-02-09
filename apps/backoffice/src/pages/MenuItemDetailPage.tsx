import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useMenuCms } from '../contexts/MenuCmsContext'
import type { CreateMenuItemDraftRequest } from '../api/menu'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function getStatusBadge(status: string, hasDraft: boolean) {
  if (status === 'archived') return { className: 'badge badge-danger', label: 'Archived' }
  if (hasDraft) return { className: 'badge badge-warning', label: 'Has Draft' }
  if (status === 'draft') return { className: 'badge badge-warning', label: 'Draft' }
  return { className: 'badge badge-success', label: 'Published' }
}

export default function MenuItemDetailPage() {
  const { documentId } = useParams<{ documentId: string }>()
  const navigate = useNavigate()
  const {
    selectedItem,
    isLoading,
    error,
    selectItem,
    clearSelection,
    createDraft,
    discardDraft,
    publishItem,
    archiveItem,
    restoreItem,
  } = useMenuCms()
  const [showDraftForm, setShowDraftForm] = useState(false)

  useEffect(() => {
    if (documentId) {
      selectItem(documentId)
    }
    return () => clearSelection()
  }, [documentId])

  if (isLoading && !selectedItem) {
    return (
      <>
        <nav aria-label="Breadcrumb">
          <ul>
            <li><a href="/menu/items" onClick={(e) => { e.preventDefault(); navigate('/menu/items') }}>Menu Items</a></li>
            <li>Loading...</li>
          </ul>
        </nav>
        <article aria-busy="true">Loading menu item...</article>
      </>
    )
  }

  if (error && !selectedItem) {
    return (
      <>
        <nav aria-label="Breadcrumb">
          <ul>
            <li><a href="/menu/items" onClick={(e) => { e.preventDefault(); navigate('/menu/items') }}>Menu Items</a></li>
            <li>Error</li>
          </ul>
        </nav>
        <article>
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
          <button className="secondary" onClick={() => navigate('/menu/items')}>Back to Items</button>
        </article>
      </>
    )
  }

  if (!selectedItem) {
    return (
      <>
        <nav aria-label="Breadcrumb">
          <ul>
            <li><a href="/menu/items" onClick={(e) => { e.preventDefault(); navigate('/menu/items') }}>Menu Items</a></li>
            <li>Not Found</li>
          </ul>
        </nav>
        <article>
          <p>Menu item not found.</p>
          <button className="secondary" onClick={() => navigate('/menu/items')}>Back to Items</button>
        </article>
      </>
    )
  }

  const { content, status, hasDraft, currentVersion, versions, createdAt, updatedAt } = selectedItem
  const badge = getStatusBadge(status, hasDraft)

  return (
    <>
      <nav aria-label="Breadcrumb">
        <ul>
          <li><a href="/menu/items" onClick={(e) => { e.preventDefault(); navigate('/menu/items') }}>Menu Items</a></li>
          <li>{content.name}</li>
        </ul>
      </nav>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <hgroup>
        <h1>{content.name}</h1>
        <p>
          <span className={badge.className}>{badge.label}</span>
          {' '}
          Version {currentVersion}
        </p>
      </hgroup>

      <section>
        <article aria-busy={isLoading}>
          <header>
            <h3>Item Details</h3>
          </header>
          <dl>
            <dt>Price</dt>
            <dd>{formatCurrency(content.price)}</dd>

            {content.description && (
              <>
                <dt>Description</dt>
                <dd>{content.description}</dd>
              </>
            )}

            {content.sku && (
              <>
                <dt>SKU</dt>
                <dd><code>{content.sku}</code></dd>
              </>
            )}

            <dt>Track Inventory</dt>
            <dd>{content.trackInventory ? 'Yes' : 'No'}</dd>

            <dt>Created</dt>
            <dd>{formatDate(createdAt)}</dd>

            <dt>Last Updated</dt>
            <dd>{formatDate(updatedAt)}</dd>
          </dl>
        </article>
      </section>

      <section>
        <h3>Actions</h3>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          {status !== 'archived' && !hasDraft && (
            <button onClick={() => setShowDraftForm(true)}>
              Create Draft
            </button>
          )}
          {hasDraft && documentId && (
            <>
              <button onClick={() => publishItem(documentId)}>
                Publish
              </button>
              <button className="secondary outline" onClick={() => discardDraft(documentId)}>
                Discard Draft
              </button>
            </>
          )}
          {status !== 'archived' && documentId && (
            <button
              className="secondary outline"
              onClick={() => {
                if (window.confirm('Archive this menu item?')) {
                  archiveItem(documentId)
                }
              }}
            >
              Archive
            </button>
          )}
          {status === 'archived' && documentId && (
            <button onClick={() => restoreItem(documentId)}>
              Restore
            </button>
          )}
        </div>
      </section>

      {showDraftForm && documentId && (
        <DraftForm
          content={content}
          onSubmit={async (data) => {
            await createDraft(documentId, data)
            setShowDraftForm(false)
          }}
          onCancel={() => setShowDraftForm(false)}
        />
      )}

      <details>
        <summary>Version History</summary>
        {versions && versions.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Version</th>
                <th>Published At</th>
                <th>Published By</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              {versions.map((v) => (
                <tr key={v.version}>
                  <td>v{v.version}</td>
                  <td>{formatDate(v.publishedAt)}</td>
                  <td>{v.publishedBy}</td>
                  <td>{v.changeNote ?? '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p style={{ color: 'var(--pico-muted-color)' }}>No version history available.</p>
        )}
      </details>
    </>
  )
}

function DraftForm({
  content,
  onSubmit,
  onCancel,
}: {
  content: { name: string; price: number; description?: string; categoryId?: string }
  onSubmit: (data: CreateMenuItemDraftRequest) => Promise<void>
  onCancel: () => void
}) {
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setIsSubmitting(true)
    const formData = new FormData(e.currentTarget)
    const data: CreateMenuItemDraftRequest = {
      name: formData.get('name') as string,
      price: parseFloat(formData.get('price') as string),
      description: (formData.get('description') as string) || undefined,
      changeNote: (formData.get('changeNote') as string) || undefined,
    }
    await onSubmit(data)
    setIsSubmitting(false)
  }

  return (
    <article>
      <header>
        <h3>Create Draft</h3>
      </header>
      <form onSubmit={handleSubmit}>
        <label>
          Name
          <input type="text" name="name" defaultValue={content.name} required />
        </label>
        <label>
          Price
          <input type="number" name="price" step="0.01" min="0" defaultValue={content.price} required />
        </label>
        <label>
          Description
          <textarea name="description" rows={3} defaultValue={content.description ?? ''} />
        </label>
        <label>
          Change Note
          <input type="text" name="changeNote" placeholder="What changed?" />
        </label>
        <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onCancel}>
            Cancel
          </button>
          <button type="submit" aria-busy={isSubmitting} disabled={isSubmitting}>
            Save Draft
          </button>
        </div>
      </form>
    </article>
  )
}
