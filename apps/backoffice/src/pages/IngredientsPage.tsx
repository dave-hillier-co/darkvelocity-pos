import type { Ingredient } from '../types'

const sampleIngredients: Ingredient[] = [
  { id: '1', code: 'BEEF-MINCE', name: 'Beef Mince', unitOfMeasure: 'kg', category: 'proteins', storageType: 'chilled', reorderLevel: 5, currentStock: 8.5 },
  { id: '2', code: 'CHICKEN-BREAST', name: 'Chicken Breast', unitOfMeasure: 'kg', category: 'proteins', storageType: 'chilled', reorderLevel: 4, currentStock: 3.2 },
  { id: '3', code: 'COD-FILLET', name: 'Cod Fillet', unitOfMeasure: 'kg', category: 'proteins', storageType: 'frozen', reorderLevel: 3, currentStock: 6.0 },
  { id: '4', code: 'POTATO', name: 'Potatoes', unitOfMeasure: 'kg', category: 'produce', storageType: 'ambient', reorderLevel: 10, currentStock: 25.0 },
  { id: '5', code: 'LETTUCE', name: 'Romaine Lettuce', unitOfMeasure: 'unit', category: 'produce', storageType: 'chilled', reorderLevel: 5, currentStock: 4 },
  { id: '6', code: 'CHEESE-CHEDDAR', name: 'Cheddar Cheese', unitOfMeasure: 'kg', category: 'dairy', storageType: 'chilled', reorderLevel: 2, currentStock: 1.5 },
]

export default function IngredientsPage() {
  return (
    <>
      <hgroup>
        <h1>Ingredients</h1>
        <p>Track raw materials and stock levels</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search ingredients..."
          style={{ maxWidth: '300px' }}
        />
        <button>Add Ingredient</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Code</th>
            <th>Name</th>
            <th>Category</th>
            <th>Stock</th>
            <th>Unit</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {sampleIngredients.map((ingredient) => {
            const isLow = (ingredient.currentStock ?? 0) <= ingredient.reorderLevel
            return (
              <tr key={ingredient.id}>
                <td><code>{ingredient.code}</code></td>
                <td>{ingredient.name}</td>
                <td>{ingredient.category}</td>
                <td>{ingredient.currentStock?.toFixed(2)}</td>
                <td>{ingredient.unitOfMeasure}</td>
                <td>
                  <span className={`badge ${isLow ? 'badge-warning' : 'badge-success'}`}>
                    {isLow ? 'Low Stock' : 'OK'}
                  </span>
                </td>
                <td>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    Edit
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </>
  )
}
