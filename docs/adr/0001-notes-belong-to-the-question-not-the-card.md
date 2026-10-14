# Notes belong to the question, not the card

A saved explanation (R3c) is called a *personal note on the card*, but cards are created lazily on the first rating, so a user can check an answer, ask for an explanation and save it before any card exists. We store notes in their own table keyed by question (at most one per question) and leave the `Card` entity untouched; "on the card" means "shown on the card in the UI". The alternative, creating a card when a note is saved, would blur the rule that only a rating creates a card and uses up the daily new-card limit.

## Considered Options

- **Note column on `Card`.** Simplest schema, but it needs a card to exist. Creating one just to hold a note would make a card with no review, which breaks "a card exists once it has been rated" and the new-card accounting.
- **Note table keyed by question (chosen).** Works before and after the first rating, survives suspending a card, and keeps study state separate from user-written content.

## Consequences

- A question has at most one note; saving a new one replaces the old one only after the user confirms.
- Deleting or resetting a card never deletes the note.
- Moving notes onto `Card` later would be a data migration, which is why this is recorded.
