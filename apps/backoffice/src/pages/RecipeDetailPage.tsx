import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useRecipeCms } from '../contexts/RecipeCmsContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function RecipeDetailPage() {
  const { documentId } = useParams<{ documentId: string }>()
  const navigate = useNavigate()
  const {
    selectedRecipe,
    isLoading,
    error,
    selectRecipe,
    deselectRecipe,
    publishDraft,
    discardDraft,
    archiveRecipe,
    restoreRecipe,
    recalculateCost,
  } = useRecipeCms()

  const [publishNote, setPublishNote] = useState('')
  const [showPublishDialog, setShowPublishDialog] = useState(false)
  const [menuItemIdToLink, setMenuItemIdToLink] = useState('')

  useEffect(() => {
    if (documentId) {
      selectRecipe(documentId)
    }
    return () => {
      deselectRecipe()
    }
  }, [documentId])

  if (error) {
    return (
      <>
        <a href="#" onClick={(e) => { e.preventDefault(); navigate('/menu/recipes') }}>Back to Recipes</a>
        <article aria-label="Error">
          <p>{error}</p>
          <button onClick={() => documentId && selectRecipe(documentId)}>Retry</button>
        </article>
      </>
    )
  }

  if (isLoading && !selectedRecipe) {
    return (
      <>
        <a href="#" onClick={(e) => { e.preventDefault(); navigate('/menu/recipes') }}>Back to Recipes</a>
        <article aria-busy="true">Loading recipe...</article>
      </>
    )
  }

  if (!selectedRecipe) {
    return (
      <>
        <a href="#" onClick={(e) => { e.preventDefault(); navigate('/menu/recipes') }}>Back to Recipes</a>
        <p>Recipe not found.</p>
      </>
    )
  }

  const activeVersion = selectedRecipe.draft ?? selectedRecipe.published
  const isDraft = selectedRecipe.draftVersion != null

  async function handlePublish() {
    if (!documentId) return
    await publishDraft(documentId, publishNote || undefined)
    setShowPublishDialog(false)
    setPublishNote('')
  }

  async function handleDiscardDraft() {
    if (!documentId) return
    await discardDraft(documentId)
  }

  async function handleArchive() {
    if (!documentId) return
    await archiveRecipe(documentId)
  }

  async function handleRestore() {
    if (!documentId) return
    await restoreRecipe(documentId)
  }

  async function handleRecalculateCost() {
    if (!documentId) return
    await recalculateCost(documentId)
  }

  return (
    <>
      <a href="#" onClick={(e) => { e.preventDefault(); navigate('/menu/recipes') }}>Back to Recipes</a>

      <hgroup>
        <h1>{activeVersion?.name ?? 'Untitled Recipe'}</h1>
        <p>
          {selectedRecipe.isArchived ? (
            <span className="badge badge-danger">Archived</span>
          ) : isDraft ? (
            <span className="badge badge-warning">Draft v{selectedRecipe.draftVersion}</span>
          ) : (
            <span className="badge badge-success">Published v{selectedRecipe.publishedVersion}</span>
          )}{' '}
          {activeVersion?.description}
        </p>
      </hgroup>

      {/* Actions */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
        {isDraft && (
          <>
            <button onClick={() => setShowPublishDialog(true)} aria-busy={isLoading}>
              Publish Draft
            </button>
            <button className="secondary outline" onClick={handleDiscardDraft} aria-busy={isLoading}>
              Discard Draft
            </button>
          </>
        )}
        <button className="secondary outline" onClick={handleRecalculateCost} aria-busy={isLoading}>
          Recalculate Cost
        </button>
        {selectedRecipe.isArchived ? (
          <button className="outline" onClick={handleRestore} aria-busy={isLoading}>
            Restore
          </button>
        ) : (
          <button className="secondary outline" onClick={handleArchive} aria-busy={isLoading}>
            Archive
          </button>
        )}
      </div>

      {/* Publish dialog */}
      {showPublishDialog && (
        <dialog open>
          <article>
            <header>
              <button aria-label="Close" rel="prev" onClick={() => setShowPublishDialog(false)} />
              <h3>Publish Recipe</h3>
            </header>
            <label>
              Publish note (optional)
              <input
                type="text"
                value={publishNote}
                onChange={(e) => setPublishNote(e.target.value)}
                placeholder="What changed?"
              />
            </label>
            <footer>
              <button className="secondary" onClick={() => setShowPublishDialog(false)}>Cancel</button>
              <button onClick={handlePublish}>Publish</button>
            </footer>
          </article>
        </dialog>
      )}

      {/* Cost summary */}
      <section>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
          <article style={{ margin: 0, padding: '1rem' }}>
            <small style={{ color: 'var(--pico-muted-color)' }}>Cost/Portion</small>
            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>
              {formatCurrency(activeVersion?.costPerPortion ?? 0)}
            </p>
          </article>
          <article style={{ margin: 0, padding: '1rem' }}>
            <small style={{ color: 'var(--pico-muted-color)' }}>Theoretical Cost</small>
            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>
              {formatCurrency(activeVersion?.theoreticalCost ?? 0)}
            </p>
          </article>
          <article style={{ margin: 0, padding: '1rem' }}>
            <small style={{ color: 'var(--pico-muted-color)' }}>Portion Yield</small>
            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>
              {activeVersion?.portionYield ?? 0} {activeVersion?.yieldUnit ?? ''}
            </p>
          </article>
          <article style={{ margin: 0, padding: '1rem' }}>
            <small style={{ color: 'var(--pico-muted-color)' }}>Ingredients</small>
            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>
              {activeVersion?.ingredients.length ?? 0}
            </p>
          </article>
        </div>
      </section>

      {/* Prep info */}
      {(activeVersion?.prepTimeMinutes || activeVersion?.cookTimeMinutes) && (
        <section>
          <div style={{ display: 'flex', gap: '2rem', marginBottom: '1rem' }}>
            {activeVersion.prepTimeMinutes && (
              <div>
                <small style={{ color: 'var(--pico-muted-color)' }}>Prep Time</small>
                <p><strong>{activeVersion.prepTimeMinutes} min</strong></p>
              </div>
            )}
            {activeVersion.cookTimeMinutes && (
              <div>
                <small style={{ color: 'var(--pico-muted-color)' }}>Cook Time</small>
                <p><strong>{activeVersion.cookTimeMinutes} min</strong></p>
              </div>
            )}
          </div>
        </section>
      )}

      {/* Ingredients table */}
      <h2>Ingredients</h2>
      {activeVersion?.ingredients && activeVersion.ingredients.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>Ingredient</th>
              <th>Quantity</th>
              <th>Unit</th>
              <th>Waste %</th>
              <th>Effective Qty</th>
              <th>Unit Cost</th>
              <th>Line Cost</th>
              <th>Optional</th>
            </tr>
          </thead>
          <tbody>
            {activeVersion.ingredients
              .sort((a, b) => a.displayOrder - b.displayOrder)
              .map((ing) => (
                <tr key={ing.ingredientId}>
                  <td>{ing.ingredientName}</td>
                  <td>{ing.quantity}</td>
                  <td>{ing.unit}</td>
                  <td>{ing.wastePercentage}%</td>
                  <td>{ing.effectiveQuantity}</td>
                  <td>{formatCurrency(ing.unitCost)}</td>
                  <td>{formatCurrency(ing.lineCost)}</td>
                  <td>{ing.isOptional ? 'Yes' : '-'}</td>
                </tr>
              ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={6}><strong>Total</strong></td>
              <td>
                <strong>
                  {formatCurrency(
                    activeVersion.ingredients.reduce((sum, ing) => sum + ing.lineCost, 0)
                  )}
                </strong>
              </td>
              <td />
            </tr>
          </tfoot>
        </table>
      ) : (
        <p style={{ color: 'var(--pico-muted-color)' }}>No ingredients added yet.</p>
      )}

      {/* Prep instructions */}
      {activeVersion?.prepInstructions && (
        <details>
          <summary>Preparation Instructions</summary>
          <p>{activeVersion.prepInstructions}</p>
        </details>
      )}

      {/* Tags */}
      {((activeVersion?.allergenTags?.length ?? 0) > 0 || (activeVersion?.dietaryTags?.length ?? 0) > 0) && (
        <details>
          <summary>Allergen and Dietary Tags</summary>
          {(activeVersion?.allergenTags?.length ?? 0) > 0 && (
            <div style={{ marginBottom: '0.5rem' }}>
              <strong>Allergens: </strong>
              {activeVersion?.allergenTags.map((tag) => (
                <span key={tag} className="badge badge-danger" style={{ marginRight: '0.25rem' }}>{tag}</span>
              ))}
            </div>
          )}
          {(activeVersion?.dietaryTags?.length ?? 0) > 0 && (
            <div>
              <strong>Dietary: </strong>
              {activeVersion?.dietaryTags.map((tag) => (
                <span key={tag} className="badge badge-success" style={{ marginRight: '0.25rem' }}>{tag}</span>
              ))}
            </div>
          )}
        </details>
      )}

      {/* Linked menu items */}
      <details>
        <summary>Linked Menu Items ({selectedRecipe.linkedMenuItemIds.length})</summary>
        {selectedRecipe.linkedMenuItemIds.length > 0 ? (
          <ul>
            {selectedRecipe.linkedMenuItemIds.map((id) => (
              <li key={id}><code>{id}</code></li>
            ))}
          </ul>
        ) : (
          <p style={{ color: 'var(--pico-muted-color)' }}>No linked menu items.</p>
        )}
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'end' }}>
          <label style={{ flex: 1 }}>
            Menu item document ID
            <input
              type="text"
              value={menuItemIdToLink}
              onChange={(e) => setMenuItemIdToLink(e.target.value)}
              placeholder="Enter menu item document ID"
            />
          </label>
          <button
            className="outline"
            disabled={!menuItemIdToLink.trim()}
            style={{ marginBottom: '1rem' }}
          >
            Link
          </button>
        </div>
      </details>

      {/* Version history */}
      <details>
        <summary>Version History ({selectedRecipe.totalVersions} versions)</summary>
        <table>
          <thead>
            <tr>
              <th>Version</th>
              <th>Status</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {selectedRecipe.published && (
              <tr>
                <td>v{selectedRecipe.publishedVersion}</td>
                <td><span className="badge badge-success">Published</span></td>
                <td>{new Date(selectedRecipe.published.createdAt).toLocaleDateString('en-GB')}</td>
              </tr>
            )}
            {selectedRecipe.draft && (
              <tr>
                <td>v{selectedRecipe.draftVersion}</td>
                <td><span className="badge badge-warning">Draft</span></td>
                <td>{new Date(selectedRecipe.draft.createdAt).toLocaleDateString('en-GB')}</td>
              </tr>
            )}
          </tbody>
        </table>
      </details>

      {/* Schedules */}
      {selectedRecipe.schedules.length > 0 && (
        <details>
          <summary>Scheduled Changes ({selectedRecipe.schedules.length})</summary>
          <table>
            <thead>
              <tr>
                <th>Version</th>
                <th>Activate At</th>
                <th>Deactivate At</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {selectedRecipe.schedules.map((schedule) => (
                <tr key={schedule.scheduleId}>
                  <td>v{schedule.version}</td>
                  <td>{new Date(schedule.activateAt).toLocaleDateString('en-GB')}</td>
                  <td>{schedule.deactivateAt ? new Date(schedule.deactivateAt).toLocaleDateString('en-GB') : '-'}</td>
                  <td>
                    <span className={`badge ${schedule.isActive ? 'badge-success' : 'badge-warning'}`}>
                      {schedule.isActive ? 'Active' : 'Pending'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}
    </>
  )
}
