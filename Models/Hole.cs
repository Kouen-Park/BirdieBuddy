namespace BirdieBuddy.Models;

// The recorded result for one hole within a Round.
public class Hole
{
    public int Id { get; set; }

    public int RoundId { get; set; }
    public Round? Round { get; set; }

    public int HoleNumber { get; set; } // 1-18

    // Snapshot of CourseHole.Par at the moment this hole was recorded.
    // Copied automatically by the service layer - never entered by the user -
    // so that a later edit to the course's par doesn't silently change the
    // statistics of rounds that were already played.
    public int Par { get; set; }

    public int Score { get; set; }    // must be > 0
    public int Putts { get; set; }    // must be >= 0
    public bool GIR { get; set; }
    public bool? FairwayHit { get; set; } // null for par 3 holes - fairway doesn't apply
    public int Penalty { get; set; }  // must be >= 0
}
