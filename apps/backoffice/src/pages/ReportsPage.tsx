export default function ReportsPage() {
  return (
    <>
      <hgroup>
        <h1>Reports</h1>
        <p>Analyze sales, costs, and margins</p>
      </hgroup>

      <div className="cards-grid">
        <article>
          <header>Daily Sales & COGS</header>
          <p>Revenue and cost of goods sold by day</p>
          <footer>
            <button className="secondary">View Report</button>
          </footer>
        </article>

        <article>
          <header>Item Margins</header>
          <p>Profitability analysis per menu item</p>
          <footer>
            <button className="secondary">View Report</button>
          </footer>
        </article>

        <article>
          <header>Category Analysis</header>
          <p>Performance by product category</p>
          <footer>
            <button className="secondary">View Report</button>
          </footer>
        </article>

        <article>
          <header>Supplier Analysis</header>
          <p>Spend, delivery performance, and trends</p>
          <footer>
            <button className="secondary">View Report</button>
          </footer>
        </article>

        <article>
          <header>Stock Movement</header>
          <p>Inventory usage and waste tracking</p>
          <footer>
            <button className="secondary">View Report</button>
          </footer>
        </article>

        <article>
          <header>Cash Drawer Report</header>
          <p>Cash handling and reconciliation</p>
          <footer>
            <button className="secondary">View Report</button>
          </footer>
        </article>
      </div>
    </>
  )
}
