import { useNavigate } from 'react-router-dom'

export default function ReportsPage() {
  const navigate = useNavigate()

  return (
    <>
      <hgroup>
        <h1>Reports</h1>
        <p>Analyze sales, costs, and margins</p>
      </hgroup>

      <div className="cards-grid">
        <article>
          <header>Margin Analysis</header>
          <p>Profitability analysis per menu item and daily COGS</p>
          <footer>
            <button className="secondary" onClick={() => navigate('/reports/margins')}>View Report</button>
          </footer>
        </article>

        <article>
          <header>Daily Sales & COGS</header>
          <p>Revenue and cost of goods sold by day</p>
          <footer>
            <button className="secondary" onClick={() => navigate('/reports/margins')}>View Report</button>
          </footer>
        </article>

        <article>
          <header>Category Analysis</header>
          <p>Performance by product category</p>
          <footer>
            <button className="secondary" onClick={() => navigate('/reports/margins')}>View Report</button>
          </footer>
        </article>

        <article>
          <header>Supplier Analysis</header>
          <p>Spend, delivery performance, and trends</p>
          <footer>
            <button className="secondary" onClick={() => navigate('/procurement/suppliers')}>View Report</button>
          </footer>
        </article>

        <article>
          <header>Stock Movement</header>
          <p>Inventory usage and waste tracking</p>
          <footer>
            <button className="secondary" onClick={() => navigate('/inventory/stock')}>View Report</button>
          </footer>
        </article>

        <article>
          <header>Cost Alerts</header>
          <p>Items with margin or cost changes requiring attention</p>
          <footer>
            <button className="secondary" onClick={() => navigate('/reports/margins')}>View Report</button>
          </footer>
        </article>
      </div>
    </>
  )
}
