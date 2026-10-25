import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, type AcceptDraftsResult, type Draft, type GenerateQuestionsResult } from '../../api/client'
import { GenerateQuestionsPage } from './GenerateQuestionsPage'

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: {
    getAiStatus: vi.fn(),
    generateQuestions: vi.fn(),
    acceptDrafts: vi.fn(),
  },
}))

const getAiStatus = vi.mocked(api.getAiStatus)
const generateQuestions = vi.mocked(api.generateQuestions)
const acceptDrafts = vi.mocked(api.acceptDrafts)

const LABEL = 'AI-generated, verify before relying on it'

const draft = (n: number): Draft => ({
  topic: 'CSharp',
  difficulty: 'Middle',
  text: `Draft question ${n}?`,
  answer: `**Short answer** number ${n}`,
  tags: ['async', 'tasks'],
})

const result = (count: number, duplicatesDropped = 0): GenerateQuestionsResult => ({
  drafts: Array.from({ length: count }, (_, i) => draft(i + 1)),
  duplicatesDropped,
})

const accepted = (rejected: AcceptDraftsResult['rejected'] = []): AcceptDraftsResult => ({ accepted: [], rejected })

function renderPage() {
  const user = userEvent.setup()
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false }, queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: '/generate', element: <GenerateQuestionsPage /> },
      { path: '/elsewhere', element: <p>Elsewhere</p> },
      { path: '/questions', element: <p>Questions page</p> },
    ],
    { initialEntries: ['/generate'] },
  )
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { user, router }
}

type User = ReturnType<typeof userEvent.setup>

const generateButton = () => screen.getByRole('button', { name: /^Generate/ })

/** Generates `count` Drafts with the default form and waits until they are listed. */
async function generateDrafts(user: User, count = 3) {
  generateQuestions.mockResolvedValue(result(count))
  await user.click(await screen.findByRole('button', { name: 'Generate' }))
  await screen.findByRole('button', { name: /Edit draft: Draft question 1\?/ })
}

const listItem = (n: number) =>
  screen.getByRole('button', { name: new RegExp(`^(Accepted|Could not be accepted)?\\s*Draft question ${n}\\?`) })
const acceptAll = () => screen.getByRole('button', { name: /^Accept all/ })

let confirm: ReturnType<typeof vi.spyOn>

beforeEach(() => {
  vi.clearAllMocks()
  window.localStorage.clear()
  getAiStatus.mockResolvedValue({ enabled: true, usedToday: 3, dailyCap: 50 })
  acceptDrafts.mockResolvedValue(accepted())
  confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
})

afterEach(() => confirm.mockRestore())

describe('GenerateQuestionsPage', () => {
  describe('availability', () => {
    it('explains that AI is not configured instead of showing the form', async () => {
      getAiStatus.mockResolvedValue({ enabled: false, usedToday: 0, dailyCap: 50 })
      renderPage()

      expect(await screen.findByText(/AI is not configured/)).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Generate' })).not.toBeInTheDocument()
    })

    it('shows the form with the usage counter beside it', async () => {
      renderPage()

      expect(await screen.findByRole('button', { name: 'Generate' })).toBeInTheDocument()
      expect(screen.getByText('AI calls today: 3 / 50')).toBeInTheDocument()
      expect(screen.getByText('No drafts yet. Fill in the form and generate some questions.')).toBeInTheDocument()
    })
  })

  describe('generating', () => {
    it('sends what the form says, in one request', async () => {
      generateQuestions.mockResolvedValue(result(1))
      const { user } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      await user.selectOptions(screen.getByLabelText('Topic'), 'SQL')
      await user.selectOptions(screen.getByLabelText('Difficulty'), 'Senior')
      await user.clear(screen.getByLabelText('How many'))
      await user.type(screen.getByLabelText('How many'), '3')
      await user.type(screen.getByLabelText('Tag (optional)'), ' Indexes ')
      await user.type(screen.getByLabelText('Focus (optional)'), 'covering indexes')
      await user.click(screen.getByRole('button', { name: 'Generate' }))

      expect(generateQuestions).toHaveBeenCalledTimes(1)
      expect(generateQuestions).toHaveBeenCalledWith({
        topic: 'Sql', difficulty: 'Senior', count: 3, tag: 'Indexes', focus: 'covering indexes',
      })
    })

    it('asks for five questions by default, and for five when the count is left empty', async () => {
      generateQuestions.mockResolvedValue(result(1))
      const { user } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      await user.click(screen.getByRole('button', { name: 'Generate' }))
      expect(generateQuestions).toHaveBeenLastCalledWith(expect.objectContaining({ count: 5 }))

      await user.clear(screen.getByLabelText('How many'))
      await user.click(screen.getByRole('button', { name: 'Generate' }))
      expect(generateQuestions).toHaveBeenCalledTimes(2)
      expect(generateQuestions).toHaveBeenLastCalledWith(expect.objectContaining({ count: 5 }))
    })

    it('does not send a count above ten: the field refuses it', async () => {
      const { user } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      await user.clear(screen.getByLabelText('How many'))
      await user.type(screen.getByLabelText('How many'), '11')
      await user.click(screen.getByRole('button', { name: 'Generate' }))

      expect(generateQuestions).not.toHaveBeenCalled()
    })

    it('shows a spinner placeholder per question while the call runs, then the Drafts', async () => {
      let finish!: (value: GenerateQuestionsResult) => void
      generateQuestions.mockReturnValue(new Promise((resolve) => (finish = resolve)))
      const { user } = renderPage()
      await user.click(await screen.findByRole('button', { name: 'Generate' }))

      expect(screen.getByRole('status')).toHaveTextContent('Generating 5 questions…')
      expect(document.querySelectorAll('.draft-placeholder')).toHaveLength(5)
      expect(screen.getByRole('button', { name: /Generating…/ })).toBeDisabled()
      expect(screen.getByLabelText('Topic')).toBeDisabled()

      await act(async () => finish(result(2)))

      expect(await screen.findByRole('button', { name: /Edit draft: Draft question 2\?/ })).toBeInTheDocument()
      expect(document.querySelectorAll('.draft-placeholder')).toHaveLength(0)
      expect(generateButton()).toBeEnabled()
    })

    it('lists the Drafts, selects the first and labels it as AI-generated', async () => {
      const { user } = renderPage()
      await generateDrafts(user)

      const detail = screen.getByRole('article', { name: 'Selected draft' })
      expect(within(detail).getByText('Draft question 1?')).toBeInTheDocument()
      expect(within(detail).getByText('number 1')).toBeInTheDocument()
      expect(within(detail).getByText(LABEL)).toBeInTheDocument()
      expect(within(detail).getByText('async')).toBeInTheDocument()
      expect(screen.getByText('3 pending')).toBeInTheDocument()
      expect(acceptAll()).toHaveTextContent('Accept all (3)')
    })

    it('says how many Drafts were skipped as duplicates', async () => {
      generateQuestions.mockResolvedValue(result(1, 2))
      const { user } = renderPage()
      await user.click(await screen.findByRole('button', { name: 'Generate' }))

      expect(await screen.findByText('2 skipped as duplicates of existing questions.')).toBeInTheDocument()
    })

    it('copes with every Draft being a duplicate', async () => {
      generateQuestions.mockResolvedValue(result(0, 3))
      const { user } = renderPage()
      await user.click(await screen.findByRole('button', { name: 'Generate' }))

      expect(await screen.findByText('3 skipped as duplicates of existing questions.')).toBeInTheDocument()
      expect(screen.getByText('Select a draft from the list to review it.')).toBeInTheDocument()
      expect(acceptAll()).toBeDisabled()
    })

    it('shows a failed run as an error with Retry, and no Drafts', async () => {
      generateQuestions.mockRejectedValueOnce(new ApiError(429, 'The daily limit of 50 AI calls is used up.'))
      const { user } = renderPage()
      await user.click(await screen.findByRole('button', { name: 'Generate' }))

      expect(await screen.findByRole('alert')).toHaveTextContent('The daily limit of 50 AI calls is used up.')
      expect(document.querySelectorAll('.draft-placeholder')).toHaveLength(0)

      generateQuestions.mockResolvedValue(result(2))
      await user.click(screen.getByRole('button', { name: 'Retry' }))

      expect(await screen.findByRole('button', { name: /Edit draft: Draft question 2\?/ })).toBeInTheDocument()
      expect(generateQuestions).toHaveBeenCalledTimes(2)
      expect(generateQuestions.mock.calls[1][0]).toEqual(generateQuestions.mock.calls[0][0])
    })

    it('has no Cancel button while a run is in progress', async () => {
      generateQuestions.mockReturnValue(new Promise(() => {}))
      const { user } = renderPage()
      await user.click(await screen.findByRole('button', { name: 'Generate' }))

      expect(screen.queryByRole('button', { name: /cancel|stop/i })).not.toBeInTheDocument()
    })
  })

  describe('reviewing Drafts', () => {
    it('shows the Draft you click', async () => {
      const { user } = renderPage()
      await generateDrafts(user)

      await user.click(listItem(2))

      const detail = screen.getByRole('article', { name: 'Selected draft' })
      expect(within(detail).getByText('Draft question 2?')).toBeInTheDocument()
      expect(listItem(2)).toHaveAttribute('aria-current', 'true')
    })

    it('edits a Draft with the pencil, and the list follows', async () => {
      const { user } = renderPage()
      await generateDrafts(user)

      await user.click(screen.getByRole('button', { name: /Edit draft: Draft question 2\?/ }))
      const question = screen.getByLabelText('Question')
      await user.clear(question)
      await user.type(question, 'Reworded?')
      await user.clear(screen.getByLabelText(/^Tags/))
      await user.type(screen.getByLabelText(/^Tags/), 'a, b,')
      await user.selectOptions(within(screen.getByRole('article', { name: 'Selected draft' })).getByLabelText('Difficulty'), 'Senior')

      expect(screen.getByRole('button', { name: /^Reworded\?/ })).toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: 'Done editing' }))
      const detail = screen.getByRole('article', { name: 'Selected draft' })
      expect(within(detail).getByText('Reworded?')).toBeInTheDocument()
      expect(within(detail).getByText('Senior')).toBeInTheDocument()
      expect(within(detail).getByText('a')).toBeInTheDocument()
      expect(within(detail).getByText('b')).toBeInTheDocument()
    })

    it('discards a Draft with the red cross and moves on to the next one', async () => {
      const { user } = renderPage()
      await generateDrafts(user)

      await user.click(screen.getByRole('button', { name: /Discard draft: Draft question 1\?/ }))

      expect(screen.queryByRole('button', { name: /^Draft question 1\?/ })).not.toBeInTheDocument()
      const detail = screen.getByRole('article', { name: 'Selected draft' })
      expect(within(detail).getByText('Draft question 2?')).toBeInTheDocument()
      expect(acceptAll()).toHaveTextContent('Accept all (2)')
      expect(acceptDrafts).not.toHaveBeenCalled()
    })

    it('discards from the footer of the selected Draft too', async () => {
      const { user } = renderPage()
      await generateDrafts(user)

      await user.click(within(screen.getByRole('article', { name: 'Selected draft' })).getByRole('button', { name: 'Discard' }))

      expect(screen.queryByRole('button', { name: /^Draft question 1\?/ })).not.toBeInTheDocument()
    })
  })

  describe('accepting', () => {
    it('saves one Draft with its edits and marks it with a green check', async () => {
      const { user } = renderPage()
      await generateDrafts(user)
      await user.click(screen.getByRole('button', { name: /Edit draft: Draft question 1\?/ }))
      await user.type(screen.getByLabelText('Question'), ' Really')

      await user.click(screen.getByRole('button', { name: 'Accept' }))

      await waitFor(() => expect(acceptDrafts).toHaveBeenCalledTimes(1))
      expect(acceptDrafts).toHaveBeenCalledWith([{ ...draft(1), text: 'Draft question 1? Really' }])
      expect(await screen.findByRole('img', { name: 'Accepted' })).toBeInTheDocument()
      expect(screen.getByText(/Saved to your questions/)).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'View questions' })).toHaveAttribute('href', '/questions')
      expect(screen.queryByRole('button', { name: 'Accept' })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /Edit draft: Draft question 1/ })).not.toBeInTheDocument()
      expect(acceptAll()).toHaveTextContent('Accept all (2)')
    })

    it('accepts all pending Drafts in one request, skipping the ones already accepted', async () => {
      const { user } = renderPage()
      await generateDrafts(user)
      await user.click(screen.getByRole('button', { name: 'Accept' }))
      await screen.findByRole('img', { name: 'Accepted' })

      await user.click(acceptAll())

      await waitFor(() => expect(acceptDrafts).toHaveBeenCalledTimes(2))
      expect(acceptDrafts).toHaveBeenLastCalledWith([draft(2), draft(3)])
      await waitFor(() => expect(screen.getAllByRole('img', { name: 'Accepted' })).toHaveLength(3))
      expect(acceptAll()).toBeDisabled()
    })

    it('keeps a rejected Draft pending, with the reason, and marks the rest accepted', async () => {
      acceptDrafts.mockResolvedValue(
        accepted([{ index: 1, errors: { text: ['A question with this text already exists.'] } }]),
      )
      const { user } = renderPage()
      await generateDrafts(user)

      await user.click(acceptAll())

      await waitFor(() => expect(screen.getAllByRole('img', { name: 'Accepted' })).toHaveLength(2))
      expect(screen.getByRole('img', { name: 'Could not be accepted' })).toBeInTheDocument()
      expect(acceptAll()).toHaveTextContent('Accept all (1)')

      await user.click(listItem(2))
      expect(screen.getByRole('alert')).toHaveTextContent('A question with this text already exists.')
      expect(screen.getByRole('button', { name: 'Accept' })).toBeEnabled()
    })

    it('shows a failed save as an error and leaves the Drafts pending', async () => {
      acceptDrafts.mockRejectedValue(new ApiError(500, 'Boom'))
      const { user } = renderPage()
      await generateDrafts(user)

      await user.click(screen.getByRole('button', { name: 'Accept' }))

      expect(await screen.findByRole('alert')).toHaveTextContent('Boom')
      expect(acceptAll()).toHaveTextContent('Accept all (3)')
    })
  })

  describe('losing Drafts', () => {
    it('asks before a new run replaces pending Drafts, and keeps them when you say no', async () => {
      const { user } = renderPage()
      await generateDrafts(user)
      confirm.mockReturnValue(false)

      await user.click(generateButton())

      expect(confirm).toHaveBeenCalledWith(expect.stringContaining('Replace 3 pending drafts'))
      expect(generateQuestions).toHaveBeenCalledTimes(1)
      expect(listItem(3)).toBeInTheDocument()
    })

    it('replaces the Drafts when you agree', async () => {
      const { user } = renderPage()
      await generateDrafts(user)
      generateQuestions.mockResolvedValue({ drafts: [{ ...draft(1), text: 'Fresh one?' }], duplicatesDropped: 0 })

      await user.click(generateButton())

      expect(await screen.findByRole('button', { name: /Edit draft: Fresh one\?/ })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /Draft question 3/ })).not.toBeInTheDocument()
      expect(generateQuestions).toHaveBeenCalledTimes(2)
    })

    it('does not ask once every Draft is accepted or discarded', async () => {
      const { user } = renderPage()
      await generateDrafts(user, 2)
      await user.click(screen.getByRole('button', { name: /Discard draft: Draft question 2\?/ }))
      await user.click(screen.getByRole('button', { name: 'Accept' }))
      await screen.findByRole('img', { name: 'Accepted' })

      await user.click(generateButton())

      expect(confirm).not.toHaveBeenCalled()
      expect(generateQuestions).toHaveBeenCalledTimes(2)
    })

    it('warns before the page is closed or refreshed only while Drafts are pending', async () => {
      const { user } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })
      const beforeUnload = () => {
        const event = new Event('beforeunload', { cancelable: true })
        window.dispatchEvent(event)
        return event.defaultPrevented
      }
      expect(beforeUnload()).toBe(false)

      await generateDrafts(user, 1)
      expect(beforeUnload()).toBe(true)

      await user.click(screen.getByRole('button', { name: /Discard draft: Draft question 1\?/ }))
      expect(beforeUnload()).toBe(false)
    })

    it('asks before leaving the page inside the app, and stays when you say no', async () => {
      const { user, router } = renderPage()
      await generateDrafts(user)
      confirm.mockReturnValue(false)

      await act(async () => {
        await router.navigate('/elsewhere')
      })

      expect(confirm).toHaveBeenCalledWith(expect.stringContaining('not accepted yet'))
      expect(router.state.location.pathname).toBe('/generate')
      expect(listItem(1)).toBeInTheDocument()
    })

    it('leaves when you agree', async () => {
      const { user, router } = renderPage()
      await generateDrafts(user)

      await act(async () => {
        await router.navigate('/elsewhere')
      })

      expect(router.state.location.pathname).toBe('/elsewhere')
      expect(await screen.findByText('Elsewhere')).toBeInTheDocument()
    })

    it('leaves without asking when there is nothing to lose', async () => {
      const { router } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      await act(async () => {
        await router.navigate('/elsewhere')
      })

      expect(confirm).not.toHaveBeenCalled()
      expect(router.state.location.pathname).toBe('/elsewhere')
    })
  })

  describe('the resizable panel', () => {
    const separator = () => screen.getByRole('separator', { name: 'Resize drafts panel' })

    it('resizes with the arrow keys, within limits, and resets on double-click', async () => {
      const { user } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })
      expect(separator()).toHaveAttribute('aria-valuenow', '300')

      separator().focus()
      await user.keyboard('{ArrowRight}{ArrowRight}')
      expect(separator()).toHaveAttribute('aria-valuenow', '332')

      await user.keyboard('{ArrowLeft}')
      expect(separator()).toHaveAttribute('aria-valuenow', '316')

      await user.keyboard('{Home}')
      expect(separator()).toHaveAttribute('aria-valuenow', '220')
      await user.keyboard('{ArrowLeft}')
      expect(separator()).toHaveAttribute('aria-valuenow', '220') // never narrower than the minimum

      await user.keyboard('{End}')
      expect(separator()).toHaveAttribute('aria-valuenow', String(Math.floor(window.innerWidth / 2)))
      await user.keyboard('{ArrowRight}')
      expect(separator()).toHaveAttribute('aria-valuenow', String(Math.floor(window.innerWidth / 2))) // nor wider than half the window

      await user.dblClick(separator())
      expect(separator()).toHaveAttribute('aria-valuenow', '300')
    })

    it('sets the panel width from the value and remembers it in this browser', async () => {
      const { user } = renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      separator().focus()
      await user.keyboard('{ArrowRight}')

      expect(document.querySelector<HTMLElement>('.generate-layout')!.style.getPropertyValue('--panel-width')).toBe('316px')
      expect(window.localStorage.getItem('generate.panelWidth')).toBe('316')
    })

    it('starts with the remembered width', async () => {
      window.localStorage.setItem('generate.panelWidth', '400')
      renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      expect(separator()).toHaveAttribute('aria-valuenow', '400')
    })

    it('is dragged with the pointer', async () => {
      renderPage()
      await screen.findByRole('button', { name: 'Generate' })

      const handle = separator()
      act(() => {
        handle.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, clientX: 300 }))
        handle.dispatchEvent(new MouseEvent('pointermove', { bubbles: true, clientX: 360 }))
        handle.dispatchEvent(new MouseEvent('pointerup', { bubbles: true, clientX: 360 }))
      })

      expect(handle).toHaveAttribute('aria-valuenow', '360')
      expect(window.localStorage.getItem('generate.panelWidth')).toBe('360')
    })
  })
})
