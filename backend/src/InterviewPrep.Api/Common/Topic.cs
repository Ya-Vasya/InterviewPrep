namespace InterviewPrep.Api.Common;

/// <summary>The fixed set of interview topics. Values are persisted as integers; do not renumber.</summary>
public enum Topic
{
    CSharp = 0,
    AspNetCore = 1,
    Sql = 2,
    SystemDesign = 3
}

public enum Difficulty
{
    Junior = 0,
    Middle = 1,
    Senior = 2
}

public static class TopicExtensions
{
    public static string DisplayName(this Topic topic) => topic switch
    {
        Topic.CSharp => "C#",
        Topic.AspNetCore => "ASP.NET Core",
        Topic.Sql => "SQL",
        Topic.SystemDesign => "System Design",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
