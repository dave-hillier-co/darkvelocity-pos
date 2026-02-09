import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useRecipeCms } from '../contexts/RecipeCmsContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function RecipesPage() {
  const navigate = useNavigate()
  const { recipes, categories, isLoading, error, loadRecipes, loadCategories } = useRecipeCms()
  const [searchTerm, setSearchTerm] = useState('')
  const [showArchived, setShowArchived] = useState(false)
  const [categoryFilter, setCategoryFilter] = useState<string>('all')

  useEffect(() => {
    loadRecipes()
    loadCategories()
  }, [])

  const filteredRecipes = recipes.filter((recipe) => {
    const matchesSearch = recipe.name.toLowerCase().includes(searchTerm.toLowerCase())
    const matchesArchived = showArchived || !recipe.isArchived
    const matchesCategory = categoryFilter === 'all' || recipe.categoryId === categoryFilter
    return matchesSearch && matchesArchived && matchesCategory
  })

  if (error) {
    return (
      <>
        <hgroup>
          <h1>Recipes</h1>
          <p>Manage recipe costing and ingredients</p>
        </hgroup>
        <article aria-label="Error">
          <p>{error}</p>
          <button onClick={() => loadRecipes()}>Retry</button>
        </article>
      </>
    )
  }

  return (
    <>
      <hgroup>
        <h1>Recipes</h1>
        <p>Manage recipe costing and ingredients</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <input
            type="search"
            placeholder="Search recipes..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            style={{ maxWidth: '300px' }}
            aria-label="Search recipes"
          />
          <select
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            style={{ maxWidth: '200px' }}
            aria-label="Filter by category"
          >
            <option value="all">All Categories</option>
            {categories.map((cat) => (
              <option key={cat.documentId} value={cat.documentId}>{cat.name}</option>
            ))}
          </select>
          <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <input
              type="checkbox"
              checked={showArchived}
              onChange={(e) => setShowArchived(e.target.checked)}
            />
            Show archived
          </label>
        </div>
        <button onClick={() => navigate('/menu/recipes/new')}>New Recipe</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Cost/Portion</th>
            <th>Version</th>
            <th>Menu Items</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredRecipes.map((recipe) => (
            <tr key={recipe.documentId}>
              <td>{recipe.name}</td>
              <td>{formatCurrency(recipe.costPerPortion)}</td>
              <td>{recipe.publishedVersion ?? '-'}</td>
              <td>{recipe.linkedMenuItemCount}</td>
              <td>
                {recipe.isArchived ? (
                  <span className="badge badge-danger">Archived</span>
                ) : recipe.hasDraft ? (
                  <span className="badge badge-warning">Draft</span>
                ) : (
                  <span className="badge badge-success">Published</span>
                )}
              </td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                    onClick={() => navigate(`/menu/recipes/${recipe.documentId}`)}
                  >
                    Edit
                  </button>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                    onClick={() => navigate(`/menu/recipes/${recipe.documentId}`)}
                  >
                    Cost
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {!isLoading && filteredRecipes.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No recipes found
        </p>
      )}
    </>
  )
}
