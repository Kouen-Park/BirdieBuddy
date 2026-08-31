namespace BirdieBuddy.DTOs;

public record ExternalCourseSummaryDto(
    string ExternalId,
    string ClubName,
    string CourseName,
    string? Location,
    int MaleTeeCount,
    int FemaleTeeCount
);
