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
