namespace InterviewPrep.Data.Exceptions;

/// <summary>A save was rejected because it would break a unique index. Lets callers react without knowing the database.</summary>
public sealed class UniqueConstraintViolationException(Exception inner)
    : Exception("A unique constraint was violated.", inner);
