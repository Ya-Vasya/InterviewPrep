using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Services.Scheduling;

/// <summary>Spaced-repetition algorithm. Pure: no database access. Replaceable (e.g. by FSRS) without touching the services.</summary>
public interface IScheduler
{
    /// <summary>State of a card that has never been rated: due immediately.</summary>
    CardState NewCard();

    /// <summary>The state after rating <paramref name="state"/>, relative to the scheduler's current time.</summary>
    CardState Review(CardState state, Rating rating);
}
