using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InterviewPrep.Api.Data;

/// <summary>
/// SQLite hands DateTime back as Kind=Unspecified. All timestamps are stored as UTC, so mark them as such
/// when they are read (otherwise they serialize without a "Z"). DateTime, unlike DateTimeOffset, can be
/// compared and ordered in SQL on SQLite.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
