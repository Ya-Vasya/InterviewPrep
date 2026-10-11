import { NavLink, Outlet } from 'react-router-dom'

export function Layout() {
  return (
    <>
      <header className="app-header">
        <div className="app-header-inner">
          <span className="brand">.NET Interview Prep</span>
          <nav aria-label="Main">
            <NavLink to="/study">Study</NavLink>
            <NavLink to="/questions">Questions</NavLink>
          </nav>
        </div>
      </header>
      <main className="container">
        <Outlet />
      </main>
    </>
  )
}
