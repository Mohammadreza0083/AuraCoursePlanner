using System;

namespace AuraCoursePlanner.Models;

public class StudySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan DurationWatched { get; set; }
    public string? Notes { get; set; }

    /// <summary>True when this session was auto-logged because the user didn't check in
    /// that day (see MainViewModel.BackfillMissedDaysAsync), rather than manually entered.</summary>
    public bool IsAutoFilled { get; set; }
}
