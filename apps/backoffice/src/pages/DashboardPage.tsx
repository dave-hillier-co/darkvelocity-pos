export default function DashboardPage() {
  return (
    <>
      <hgroup>
        <h1>Dashboard</h1>
        <p>Overview of today's performance</p>
      </hgroup>

      <div className="cards-grid">
        <article>
          <header>Today's Sales</header>
          <p style={{ fontSize: '2rem', fontWeight: 700 }}>$1,234.56</p>
          <footer>
            <small>+12% from yesterday</small>
          </footer>
        </article>

        <article>
          <header>Orders</header>
          <p style={{ fontSize: '2rem', fontWeight: 700 }}>47</p>
          <footer>
            <small>Average $26.27 per order</small>
          </footer>
        </article>

        <article>
          <header>Gross Margin</header>
          <p style={{ fontSize: '2rem', fontWeight: 700 }}>68.5%</p>
          <footer>
            <small>COGS: $389.23</small>
          </footer>
        </article>

        <article>
          <header>Low Stock Alerts</header>
          <p style={{ fontSize: '2rem', fontWeight: 700, color: 'var(--pico-del-color)' }}>3</p>
          <footer>
            <small>Items below reorder level</small>
          </footer>
        </article>
      </div>

      <section style={{ marginTop: '2rem' }}>
        <h2>Quick Actions</h2>
        <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
          <button>Create Purchase Order</button>
          <button className="secondary">Record Delivery</button>
          <button className="secondary">View Reports</button>
        </div>
      </section>
    </>
  )
}
