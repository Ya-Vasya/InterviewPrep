import { NavLink, Outlet, useMatches } from 'react-router-dom'
import { useAiStatus } from '../features/ai/useAiStatus'

export function Layout() {
  // The Generate link only shows when AI is configured; while that is unknown it stays hidden.
  const ai = useAiStatus()
  // A route can ask for the wide layout (`handle: { wide: true }`), for pages with a side panel.
  const wide = useMatches().some((m) => (m.handle as { wide?: boolean } | undefined)?.wide)
  const widthClass = wide ? ' wide' : ''

  return (
    <>
      <header className="app-header">
        <div className={`app-header-inner${widthClass}`}>
          <span className="brand">.NET Interview Prep</span>
          <nav aria-label="Main">
            <NavLink to="/study">Study</NavLink>
            <NavLink to="/questions">Questions</NavLink>
            {ai.data?.enabled && <NavLink to="/generate">Generate</NavLink>}
          </nav>
        </div>
      </header>
      <main className={`container${widthClass}`}>
        <Outlet />
      </main>
    </>
  )
}
