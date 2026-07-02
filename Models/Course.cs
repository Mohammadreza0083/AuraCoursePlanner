using System;
using System.Collections.Generic;

namespace AuraCoursePlanner.Models;

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;

    // Total length of the course video material
    public TimeSpan TotalDuration { get; set; }

    // Deadline to finish the course
    public DateTime GoalEndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation property
    public List<StudySession> StudySessions { get; set; } = new();
}
