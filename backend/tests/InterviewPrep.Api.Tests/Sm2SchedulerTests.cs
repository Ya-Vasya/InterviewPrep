using InterviewPrep.Api.Features.Study;

namespace InterviewPrep.Api.Tests;

/// <summary>Plain unit tests: the scheduler is pure and runs on a fake clock.</summary>
public class Sm2SchedulerTests
{
    private readonly FakeClock _clock = new();
    private readonly Sm2Scheduler _scheduler;

    public Sm2SchedulerTests() => _scheduler = new Sm2Scheduler(_clock);

    private static DateTime Utc(DateTimeOffset t) => t.UtcDateTime;

    [Fact]
    public void NewCard_StartsWithDefaultEaseAndIsDueImmediately()
    {
        var card = _scheduler.NewCard();

        Assert.Equal(2.5, card.Ease);
        Assert.Equal(0, card.IntervalDays);
        Assert.Equal(0, card.Repetitions);
        Assert.Equal(0, card.Lapses);
        Assert.Equal(Utc(_clock.GetUtcNow()), card.DueAt);
        Assert.Null(card.LastReviewedAt);
    }

    [Fact]
    public void FirstGood_IsOneDay()
    {
        var card = _scheduler.Review(_scheduler.NewCard(), Rating.Good);

        Assert.Equal(1, card.IntervalDays);
        Assert.Equal(1, card.Repetitions);
        Assert.Equal(Utc(_clock.GetUtcNow().AddDays(1)), card.DueAt);
        Assert.Equal(Utc(_clock.GetUtcNow()), card.LastReviewedAt);
    }

    [Fact]
    public void SecondGood_IsSixDays()
    {
        var first = _scheduler.Review(_scheduler.NewCard(), Rating.Good);
        _clock.Advance(TimeSpan.FromDays(1));

        var second = _scheduler.Review(first, Rating.Good);

        Assert.Equal(6, second.IntervalDays);
        Assert.Equal(2, second.Repetitions);
        Assert.Equal(Utc(_clock.GetUtcNow().AddDays(6)), second.DueAt);
    }

    [Fact]
    public void ThirdGood_MultipliesIntervalByEase()
    {
        var card = _scheduler.NewCard();
        card = _scheduler.Review(card, Rating.Good);
        card = _scheduler.Review(card, Rating.Good);

        var third = _scheduler.Review(card, Rating.Good);

        Assert.Equal(15, third.IntervalDays); // 6 * 2.5
        Assert.Equal(2.5, third.Ease);
    }

    [Fact]
    public void Again_ResetsRepetitionsCountsLapseAndIsDueImmediately()
    {
        var card = _scheduler.NewCard();
        card = _scheduler.Review(card, Rating.Good);
        card = _scheduler.Review(card, Rating.Good);
        _clock.Advance(TimeSpan.FromDays(6));

        var lapsed = _scheduler.Review(card, Rating.Again);

        Assert.Equal(0, lapsed.Repetitions);
        Assert.Equal(0, lapsed.IntervalDays);
        Assert.Equal(1, lapsed.Lapses);
        Assert.Equal(Utc(_clock.GetUtcNow()), lapsed.DueAt);
        Assert.True(lapsed.Ease < card.Ease);
    }

    [Fact]
    public void GoodAfterAgain_StartsTheIntervalsOverAtOneDay()
    {
        var card = _scheduler.NewCard();
        card = _scheduler.Review(card, Rating.Good);
        card = _scheduler.Review(card, Rating.Good);
        card = _scheduler.Review(card, Rating.Again);

        var recovered = _scheduler.Review(card, Rating.Good);

        Assert.Equal(1, recovered.IntervalDays);
        Assert.Equal(1, recovered.Repetitions);
        Assert.Equal(1, recovered.Lapses);
    }

    [Theory]
    [InlineData(Rating.Again)]
    [InlineData(Rating.Hard)]
    public void Ease_NeverDropsBelowFloor(Rating rating)
    {
        var card = _scheduler.NewCard();

        for (var i = 0; i < 30; i++)
            card = _scheduler.Review(card, rating);

        Assert.Equal(Sm2Scheduler.MinEase, card.Ease);
    }

    [Fact]
    public void Easy_RaisesEaseAndHard_LowersIt()
    {
        var start = _scheduler.NewCard();

        Assert.True(_scheduler.Review(start, Rating.Easy).Ease > start.Ease);
        Assert.True(_scheduler.Review(start, Rating.Hard).Ease < start.Ease);
        Assert.Equal(start.Ease, _scheduler.Review(start, Rating.Good).Ease);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void SuccessfulRatings_GrowTheIntervalAndAreOrdered(int goodReviewsBefore)
    {
        var card = _scheduler.NewCard();
        for (var i = 0; i < goodReviewsBefore; i++)
            card = _scheduler.Review(card, Rating.Good);

        var hard = _scheduler.Review(card, Rating.Hard).IntervalDays;
        var good = _scheduler.Review(card, Rating.Good).IntervalDays;
        var easy = _scheduler.Review(card, Rating.Easy).IntervalDays;

        Assert.True(hard >= 1);
        Assert.True(hard <= good, $"hard {hard} > good {good}");
        Assert.True(good <= easy, $"good {good} > easy {easy}");
        Assert.True(good > card.IntervalDays);
    }
}
