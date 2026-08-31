namespace BirdieBuddy.Models;

public class CourseTee
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public string Name { get; set; } = string.Empty;

    // Golf NZ course type, for example MN, MY, WN, or WY.
    public string CourseType { get; set; } = string.Empty;

    public string Gender { get; set; } = string.Empty;

    public bool NineHoles { get; set; }

    public decimal? Rating { get; set; }

    public int? Slope { get; set; }

    public string? Colour { get; set; }

    public int? TotalPar { get; set; }

    public int? FrontNinePar { get; set; }
    public int? BackNinePar { get; set; }

    public int? FrontNineMetres { get; set; }
    public int? BackNineMetres { get; set; }

    public List<CourseHole> CourseHoles { get; set; } = new();
}