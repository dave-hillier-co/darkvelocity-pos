import { useState } from 'react'

interface Recipe {
  id: string
  code: string
  menuItemName: string
  portionYield: number
  currentCostPerPortion: number
  ingredientCount: number
  isActive: boolean
}

const sampleRecipes: Recipe[] = [
  { id: '1', code: 'RCP-BURGER-001', menuItemName: 'Classic Burger', portionYield: 1, currentCostPerPortion: 3.75, ingredientCount: 5, isActive: true },
  { id: '2', code: 'RCP-FISH-001', menuItemName: 'Fish & Chips', portionYield: 1, currentCostPerPortion: 4.20, ingredientCount: 4, isActive: true },
  { id: '3', code: 'RCP-PASTA-001', menuItemName: 'Spaghetti Bolognese', portionYield: 4, currentCostPerPortion: 2.50, ingredientCount: 8, isActive: true },
  { id: '4', code: 'RCP-STEAK-001', menuItemName: 'Ribeye Steak', portionYield: 1, currentCostPerPortion: 8.50, ingredientCount: 3, isActive: true },
  { id: '5', code: 'RCP-SALAD-001', menuItemName: 'Caesar Salad', portionYield: 1, currentCostPerPortion: 2.80, ingredientCount: 6, isActive: false },
]

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function RecipesPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [showInactive, setShowInactive] = useState(false)

  const filteredRecipes = sampleRecipes.filter((recipe) => {
    const matchesSearch = recipe.menuItemName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      recipe.code.toLowerCase().includes(searchTerm.toLowerCase())
    const matchesStatus = showInactive || recipe.isActive
    return matchesSearch && matchesStatus
  })

  return (
    <div className="main-body">
      <header className="page-header">
        <h1>Recipes</h1>
        <p>Manage recipe costing and ingredients</p>
      </header>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <input
            type="search"
            placeholder="Search recipes..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            style={{ maxWidth: '300px' }}
          />
          <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <input
              type="checkbox"
              checked={showInactive}
              onChange={(e) => setShowInactive(e.target.checked)}
            />
            Show inactive
          </label>
        </div>
        <button>New Recipe</button>
      </div>

      <table className="data-table">
        <thead>
          <tr>
            <th>Code</th>
            <th>Menu Item</th>
            <th>Yield</th>
            <th>Cost/Portion</th>
            <th>Ingredients</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredRecipes.map((recipe) => (
            <tr key={recipe.id}>
              <td><code>{recipe.code}</code></td>
              <td>{recipe.menuItemName}</td>
              <td>{recipe.portionYield}</td>
              <td>{formatCurrency(recipe.currentCostPerPortion)}</td>
              <td>{recipe.ingredientCount}</td>
              <td>
                <span className={`badge ${recipe.isActive ? 'badge-success' : 'badge-danger'}`}>
                  {recipe.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    Edit
                  </button>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    Cost
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {filteredRecipes.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No recipes found
        </p>
      )}
    </div>
  )
}
