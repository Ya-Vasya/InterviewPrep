import { createBrowserRouter, Navigate } from 'react-router-dom'
import { QuestionsPage } from '../features/questions/QuestionsPage'
import { StudySessionPage } from '../features/study/StudySessionPage'
import { StudySetupPage } from '../features/study/StudySetupPage'
import { Layout } from './Layout'

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <Navigate to="/study" replace /> },
      { path: 'questions', element: <QuestionsPage /> },
      { path: 'study', element: <StudySetupPage /> },
      { path: 'study/session', element: <StudySessionPage /> },
      { path: '*', element: <Navigate to="/study" replace /> },
    ],
  },
])
