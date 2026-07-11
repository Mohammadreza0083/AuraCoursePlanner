using System;
using System.Collections.Generic;

namespace AuraCoursePlanner.Models;

/// <summary>User-assigned importance of a course, used to sort the dashboard
/// and to decide which course should get attention first.</summary>
public enum CoursePriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;

    // Total length of the course video material
    public TimeSpan TotalDuration { get; set; }

    // Deadline to finish the course
    public DateTime GoalEndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Manual priority set by the user (defaults to Medium for existing rows).
    public CoursePriority Priority { get; set; } = CoursePriority.Medium;

    // Bitmask of DayOfWeek (1 << (int)DayOfWeek) representing which days of the
    // week this course is meant to be studied on. Defaults to every day (127 = all 7 bits set).
    public int ScheduledDaysMask { get; set; } = 127;

    // Navigation property
    public List<StudySession> StudySessions { get; set; } = new();
}