using InterviewPrep.Api.Common;

namespace InterviewPrep.Api.Tests;

public sealed class TwoNewCardsPerDayFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?> { ["Study:NewCardsPerDay"] = "2" };
}

public sealed class TwoReviewsPerDayFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?> { ["Study:ReviewsPerDay"] = "2" };
}

/// <summary>Daily new-card limit, and how "day" is decided from the user's time zone.</summary>
public class NewCardLimitTests(TwoNewCardsPerDayFactory factory)
    : StudyTestBase(factory), IClassFixture<TwoNewCardsPerDayFactory>
{
    [Fact]
    public async Task Queue_OffersOnlyAsManyNewCardsAsTheDailyLimit()
    {
        Assert.Equal(2, (await GetQueue("?limit=100")).Count);
        Assert.Equal(2, (await GetSummary()).Topics.Single(t => t.Topic == Topic.Sql).NewCount);
    }

    [Fact]
    public async Task IntroducingCards_UsesUpTheLimit_UntilTheNextDay()
    {
        foreach (var item in await GetQueue("?limit=100"))
            await Rate(item.QuestionId, "Good");

        Assert.Empty(await GetQueue("?limit=100"));
        var exhausted = await GetSummary();
        Assert.Equal(0, exhausted.NewRemainingToday);
        Assert.All(exhausted.Topics, t => Assert.Equal(0, t.NewCount));

        Clock.Advance(TimeSpan.FromDays(1));

        var nextDay = await GetQueue("?limit=100");
        Assert.Equal(2, nextDay.Count(i => i.IsNew));
        Assert.Equal(2, nextDay.Count(i => !i.IsNew)); // yesterday's two cards are due again
    }

    [Fact]
    public async Task AbandonedQueue_DoesNotUseUpTheLimit()
    {
        await GetQueue("?limit=100");
        await GetQueue("?limit=100");

        Assert.Equal(2, (await GetSummary()).NewRemainingToday);
        Assert.Equal(2, (await GetQueue("?limit=100")).Count);
    }

    [Fact]
    public async Task ReratingACard_DoesNotUseUpNewCardSlots()
    {
        var item = (await GetQueue("?limit=1")).Single();
        await Rate(item.QuestionId, "Again");
        await Rate(item.QuestionId, "Again");
        await Rate(item.QuestionId, "Again");

        Assert.Equal(1, (await GetSummary()).NewRemainingToday);
    }

    [Fact]
    public async Task DayBoundary_FollowsTheTimeZoneSentByTheBrowser()
    {
        // 09:00 UTC is 22:00 in Auckland (UTC+13, daylight time) on the same date.
        foreach (var item in await GetQueue("?limit=100"))
            await Rate(item.QuestionId, "Good");

        // 12:00 UTC is still the same day in UTC, but already past midnight in Auckland.
        Clock.Advance(TimeSpan.FromHours(3));

        Assert.Equal(0, (await GetSummary("?tz=UTC")).NewRemainingToday);
        Assert.Equal(2, (await GetSummary("?tz=Pacific/Auckland")).NewRemainingToday);
        Assert.Empty((await GetQueue("?tz=UTC&limit=100")));
        Assert.Equal(2, (await GetQueue("?tz=Pacific/Auckland&limit=100")).Count(i => i.IsNew));
    }
}

/// <summary>Daily review limit.</summary>
public class ReviewLimitTests(TwoReviewsPerDayFactory factory)
    : StudyTestBase(factory), IClassFixture<TwoReviewsPerDayFactory>
{
    [Fact]
    public async Task Queue_OffersOnlyAsManyDueCardsAsTheDailyReviewLimit_AndRatingsUseItUp()
    {
        // Day 1: introduce four cards (new-card reviews do not count against the review limit).
        var topicQuestions = (await GetQuestions(Topic.Sql)).Take(4).ToList();
        foreach (var q in topicQuestions)
            await Rate(q.Id, "Good");

        Clock.Advance(TimeSpan.FromDays(1));

        var summary = await GetSummary();
        Assert.Equal(2, summary.ReviewsRemainingToday);
        Assert.Equal(2, summary.Topics.Single(t => t.Topic == Topic.Sql).DueCount); // 4 are due, capped at 2

        var due = (await GetQueue("?topic=Sql&limit=100")).Where(i => !i.IsNew).ToList();
        Assert.Equal(2, due.Count);

        foreach (var item in due)
            await Rate(item.QuestionId, "Good");

        Assert.Equal(0, (await GetSummary()).ReviewsRemainingToday);
        Assert.DoesNotContain(await GetQueue("?topic=Sql&limit=100"), i => !i.IsNew);
    }

    [Fact]
    public async Task UndoingAReview_GivesTheReviewSlotBack()
    {
        var q = (await GetQuestions(Topic.Sql))[0];
        await Rate(q.Id, "Good");
        Clock.Advance(TimeSpan.FromDays(1));
        var reviewId = Guid.NewGuid();
        await Rate(q.Id, "Good", reviewId);
        Assert.Equal(1, (await GetSummary()).ReviewsRemainingToday);

        await Client.DeleteAsync($"/api/study/reviews/{reviewId}");

        Assert.Equal(2, (await GetSummary()).ReviewsRemainingToday);
    }
}
