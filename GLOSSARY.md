# Interview Prep

A web app for one developer preparing for .NET interviews: browse interview questions by topic and study them as flashcards on a spaced-repetition schedule.

## Language

**Question**:
The content to study: question text, reference answer, topic and difficulty.
_Avoid_: Card (a card is learning state, not content)

**Difficulty**:
How hard a question is: Junior, Middle or Senior. A property of the question that does not change with the user's performance.
_Avoid_: Complexity, level (when meaning a question's difficulty)

**Card**:
The user's learning state for one question. A question has at most one card.
_Avoid_: Flashcard (as a data term)

**Rating**:
The user's self-assessment of how well they recalled a card's answer: Again, Hard, Good or Easy. It sets when the card is next due.
_Avoid_: Difficulty, complexity, grade, score

**Due**:
A card is due once its next review time has arrived. A card rated Again is due again immediately.
_Avoid_: Expired, overdue (when meaning merely due)

**New card**:
A question the user has not studied yet, so it has no card until it is first introduced in a study session.
_Avoid_: Unseen question

**Review**:
One recorded rating of a card at a point in time. Undoing a rating removes its review.
_Avoid_: Answer attempt (that is an AI-checked free-text answer, a different thing)

**Study session**:
One sitting of working through due cards, then new cards, rating each one.
_Avoid_: Quiz, round

**Answer attempt**:
The user's own free-text answer to a question, typed before seeing the reference answer, together with the AI's verdict on it. Checking an attempt never rates a card by itself.
_Avoid_: Review (a review is a rating, not an answer), submission

**Verdict**:
The AI's judgement of an answer attempt against the reference answer: correct, partial or incorrect, with a score and the key points covered and missed.
_Avoid_: Rating (that is the user's own choice), grade

**Suggested rating**:
The Rating implied by a verdict (incorrect is Again, partial is Hard, correct is Good). It is only a suggestion: the user accepts or overrides it, and a review counts as AI-assisted only when the user's rating matches it.
_Avoid_: Auto-rating

**Explanation**:
The AI's account of what was wrong or missing in a partial or incorrect answer attempt, and why. One-shot, tied to one attempt.
_Avoid_: Chat, feedback (feedback is the short comment inside a verdict)

**Note**:
A personal text the user keeps for a question, such as a saved explanation. A question has at most one note, and it belongs to the question rather than to the card.
_Avoid_: Comment, annotation

**Generation run**:
One use of the Generate form: the user asks for 1 to 10 questions on a topic and difficulty, and the Drafts appear one at a time as they are produced. A new run replaces the previous Drafts.
_Avoid_: Batch, job

**Duplicate**:
A Draft whose wording matches an existing question of the same topic (or another Draft in the same batch), ignoring case, spacing and punctuation. Duplicates are skipped, never shown or saved.
_Avoid_: Repeat, near-duplicate

**Draft**:
An AI-generated question the user has not accepted yet. Nothing is stored until it is accepted, and an accepted draft becomes a question with the AI as its source.
_Avoid_: Suggestion, candidate question
