using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Services.Scheduling;

/// <summary>
/// SM-2 with four ratings. First successful review = 1 day, second = 6 days, then interval * ease.
/// Again resets repetitions, counts a lapse and makes the card due immediately.
/// </summary>
public sealed class Sm2Scheduler(TimeProvider time) : IScheduler
{
    public const double InitialEase = 2.5;
    public const double MinEase = 1.3;

    private const double HardMultiplier = 1.2;
    private const double EasyBonus = 1.3;

    public CardState NewCard() => new(InitialEase, 0, 0, 0, time.GetUtcNow().UtcDateTime, null);

    public CardState Review(CardState state, Rating rating)
    {
        var now = time.GetUtcNow().UtcDateTime;

        if (rating == Rating.Again)
        {
            return state with
            {
                Ease = Math.Max(MinEase, state.Ease - 0.2),
                IntervalDays = 0,
                Repetitions = 0,
                Lapses = state.Lapses + 1,
                DueAt = now,
                LastReviewedAt = now
            };
        }

        var ease = Math.Max(MinEase, state.Ease + rating switch
        {
            Rating.Hard => -0.15,
            Rating.Easy => 0.15,
            _ => 0.0
        });

        var interval = NextInterval(state, rating, ease);

        return state with
        {
            Ease = ease,
            IntervalDays = interval,
            Repetitions = state.Repetitions + 1,
            DueAt = now.AddDays(interval),
            LastReviewedAt = now
        };
    }

    private static int NextInterval(CardState state, Rating rating, double newEase)
    {
        switch (state.Repetitions)
        {
            case 0:
                return rating == Rating.Easy ? 3 : 1;
            case 1:
                return rating switch { Rating.Hard => 3, Rating.Easy => 8, _ => 6 };
        }

        // Every successful rating grows the interval by at least a day; Hard <= Good <= Easy.
        var previous = state.IntervalDays;
        var good = Math.Max(previous + 1, (int)Math.Round(previous * newEase));

        return rating switch
        {
            Rating.Hard => Math.Min(good, Math.Max(previous + 1, (int)Math.Round(previous * HardMultiplier))),
            Rating.Easy => Math.Max(good, (int)Math.Round(previous * newEase * EasyBonus)),
            _ => good
        };
    }
}
