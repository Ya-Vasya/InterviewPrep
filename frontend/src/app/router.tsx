import { createBrowserRouter, Navigate } from 'react-router-dom'
import { StudySetupPage } from '../features/study/StudySetupPage'
import { Layout } from './Layout'

// The pages that render markdown answers (and so pull in the markdown and syntax-highlighting
// libraries) are loaded on demand, which keeps the initial bundle small.
export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <Navigate to="/study" replace /> },
      {
        path: 'questions',
        lazy: async () => ({ Component: (await import('../features/questions/QuestionsPage')).QuestionsPage }),
      },
      { path: 'study', element: <StudySetupPage /> },
      {
        path: 'study/session',
        lazy: async () => ({ Component: (await import('../features/study/StudySessionPage')).StudySessionPage }),
      },
      { path: '*', element: <Navigate to="/study" replace /> },
    ],
  },
])
