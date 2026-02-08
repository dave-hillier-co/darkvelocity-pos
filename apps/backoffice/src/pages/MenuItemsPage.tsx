import type { MenuItem } from '../types'

const sampleItems: MenuItem[] = [
  { id: '1', name: 'Burger', price: 12.50, categoryId: '1', accountingGroupId: '1', isActive: true },
  { id: '2', name: 'Fish & Chips', price: 14.00, categoryId: '1', accountingGroupId: '1', isActive: true },
  { id: '3', name: 'Caesar Salad', price: 9.50, categoryId: '1', accountingGroupId: '1', isActive: true },
  { id: '4', name: 'Steak', price: 24.00, categoryId: '1', accountingGroupId: '1', isActive: true },
  { id: '5', name: 'Cola', price: 3.00, categoryId: '2', accountingGroupId: '2', isActive: true },
  { id: '6', name: 'Beer', price: 5.50, categoryId: '2', accountingGroupId: '2', isActive: true },
]

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function MenuItemsPage() {
  return (
    <>
      <hgroup>
        <h1>Menu Items</h1>
        <p>Manage your menu items and pricing</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search items..."
          style={{ maxWidth: '300px' }}
        />
        <button>Add Item</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Price</th>
            <th>Category</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {sampleItems.map((item) => (
            <tr key={item.id}>
              <td>{item.name}</td>
              <td>{formatCurrency(item.price)}</td>
              <td>{item.categoryId === '1' ? 'Food' : 'Drinks'}</td>
              <td>
                <span className={`badge ${item.isActive ? 'badge-success' : 'badge-danger'}`}>
                  {item.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>
                <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                  Edit
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  )
}
