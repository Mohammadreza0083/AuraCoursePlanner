using AuraCoursePlanner.Data;
using AuraCoursePlanner.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AuraCoursePlanner.ViewModels;

public partial class CourseViewModel : ObservableObject
{
    private readonly Func<AuraDbContext> _dbFactory;

    public Guid Id { get; }

    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private TimeSpan totalDuration;
    [ObservableProperty] private DateTime goalEndDate;
    [ObservableProperty] private ObservableCollection<StudySession> sessions = new();
    [ObservableProperty] private CoursePriority priority;
    [ObservableProperty] private int scheduledDaysMask = 127;
    public DateTime CreatedAt { get; }

    public bool IsDayScheduled(DayOfWeek day) => (ScheduledDaysMask & (1 << (int)day)) != 0;

    private void SetDayScheduled(DayOfWeek day, bool value)
    {
        var bit = 1 << (int)day;
        var newMask = value ? (ScheduledDaysMask | bit) : (ScheduledDaysMask & ~bit);
        if (newMask != ScheduledDaysMask) ScheduledDaysMask = newMask;
    }

    // Individual bindable toggles for a 7-day picker in XAML (CheckBox IsChecked="{Binding IsMondayScheduled}" etc.)
    public bool IsSundayScheduled { get => IsDayScheduled(DayOfWeek.Sunday); set => SetDayScheduled(DayOfWeek.Sunday, value); }
    public bool IsMondayScheduled { get => IsDayScheduled(DayOfWeek.Monday); set => SetDayScheduled(DayOfWeek.Monday, value); }
    public bool IsTuesdayScheduled { get => IsDayScheduled(DayOfWeek.Tuesday); set => SetDayScheduled(DayOfWeek.Tuesday, value); }
    public bool IsWednesdayScheduled { get => IsDayScheduled(DayOfWeek.Wednesday); set => SetDayScheduled(DayOfWeek.Wednesday, value); }
    public bool IsThursdayScheduled { get => IsDayScheduled(DayOfWeek.Thursday); set => SetDayScheduled(DayOfWeek.Thursday, value); }
    public bool IsFridayScheduled { get => IsDayScheduled(DayOfWeek.Friday); set => SetDayScheduled(DayOfWeek.Friday, value); }
    public bool IsSaturdayScheduled { get => IsDayScheduled(DayOfWeek.Saturday); set => SetDayScheduled(DayOfWeek.Saturday, value); }

    /// <summary>Whether today is one of this course's scheduled study days.
    /// Used both for the "rest day" pace display and for the tray notification.</summary>
    public bool IsTodayScheduled => IsDayScheduled(DateTime.Today.DayOfWeek);

    partial void OnScheduledDaysMaskChanged(int value)
    {
        OnPropertyChanged(nameof(IsSundayScheduled));
        OnPropertyChanged(nameof(IsMondayScheduled));
        OnPropertyChanged(nameof(IsTuesdayScheduled));
        OnPropertyChanged(nameof(IsWednesdayScheduled));
        OnPropertyChanged(nameof(IsThursdayScheduled));
        OnPropertyChanged(nameof(IsFridayScheduled));
        OnPropertyChanged(nameof(IsSaturdayScheduled));
        OnPropertyChanged(nameof(IsTodayScheduled));
        RaiseAllMetricsChanged();
    }

    /// <summary>0 = highest priority, used purely for sorting.</summary>
    public int PriorityRank => Priority switch
    {
        CoursePriority.High => 0,
        CoursePriority.Medium => 1,
        CoursePriority.Low => 2,
        _ => 1
    };

    public string PriorityDisplay => Priority switch
    {
        CoursePriority.High => "🔥 High",
        CoursePriority.Medium => "⚖️ Medium",
        CoursePriority.Low => "🧊 Low",
        _ => Priority.ToString()
    };

    [ObservableProperty] private DateTime logDate = DateTime.Today;
    [ObservableProperty] private int logHours;
    [ObservableProperty] private int logMinutes;
    [ObservableProperty] private string? logNotes;

    [ObservableProperty] private bool isSelected;

    public CourseViewModel(Course course, Func<AuraDbContext> dbFactory)
    {
        Id = course.Id;
        _dbFactory = dbFactory;
        CreatedAt = course.CreatedAt;
        title = course.Title;
        totalDuration = course.TotalDuration;
        goalEndDate = course.GoalEndDate;
        priority = course.Priority;
        scheduledDaysMask = course.ScheduledDaysMask;
        sessions = new ObservableCollection<StudySession>(
            course.StudySessions.OrderByDescending(s => s.Date));
    }

    partial void OnPriorityChanged(CoursePriority value)
    {
        OnPropertyChanged(nameof(PriorityRank));
        OnPropertyChanged(nameof(PriorityDisplay));
    }

    public TimeSpan TotalWatched => TimeSpan.FromTicks(Sessions.Sum(s => s.DurationWatched.Ticks));

    public TimeSpan RemainingTime
    {
        get
        {
            var remaining = TotalDuration - TotalWatched;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public double ProgressPercentage
    {
        get
        {
            if (TotalDuration <= TimeSpan.Zero) return 0;
            var pct = TotalWatched.TotalMinutes / TotalDuration.TotalMinutes * 100.0;
            return Math.Clamp(pct, 0, 100);
        }
    }

    public string ProgressPercentageDisplay => $"{ProgressPercentage:0.0}%";

    public int RemainingDays
    {
        get
        {
            var days = (GoalEndDate.Date - DateTime.Today).Days;
            return Math.Max(days, 0);
        }
    }

    /// <summary>How many of the remaining calendar days between today and the deadline
    /// (inclusive) actually fall on a day this course is scheduled for. This is what
    /// pacing is now based on — previously every single day counted, which overstated
    /// how much slack you had if you only study a few days a week.</summary>
    public int RemainingScheduledDays
    {
        get
        {
            if (ScheduledDaysMask == 0) return RemainingDays; // nothing selected: fall back to every day

            var count = 0;
            var cursor = DateTime.Today;
            var end = GoalEndDate.Date;
            if (end < cursor) return 0;

            while (cursor <= end)
            {
                if (IsDayScheduled(cursor.DayOfWeek)) count++;
                cursor = cursor.AddDays(1);
            }
            return count;
        }
    }

    public bool IsOverdue => (GoalEndDate.Date - DateTime.Today).Days < 0 && RemainingTime > TimeSpan.Zero;

    public bool IsCompleted => TotalWatched >= TotalDuration && TotalDuration > TimeSpan.Zero;

    public TimeSpan DynamicDailyPace
    {
        get
        {
            if (IsCompleted) return TimeSpan.Zero;
            var scheduledDaysLeft = RemainingScheduledDays;
            if (scheduledDaysLeft <= 0)
            {
                return RemainingTime;
            }
            var minutesPerDay = RemainingTime.TotalMinutes / scheduledDaysLeft;
            return TimeSpan.FromMinutes(Math.Max(minutesPerDay, 0));
        }
    }

    public string DynamicDailyPaceDisplay
    {
        get
        {
            if (IsCompleted) return "✅ Completed";
            if (!IsTodayScheduled) return "😴 Rest day — no session planned today";
            return $"⚡ Target: {FormatHm(DynamicDailyPace)}/day";
        }
    }

    public DateTime? EstimatedCompletionDate
    {
        get
        {
            if (IsCompleted) return null;

            var recentSessions = Sessions
                .Where(s => s.Date >= DateTime.Today.AddDays(-7))
                .ToList();

            if (recentSessions.Count == 0) return null;

            var activeDays = recentSessions.Select(s => s.Date.Date).Distinct().Count();
            if (activeDays == 0) return null;

            var totalRecentMinutes = recentSessions.Sum(s => s.DurationWatched.TotalMinutes);
            var avgMinutesPerActiveDay = totalRecentMinutes / activeDays;

            if (avgMinutesPerActiveDay <= 0) return null;

            var daysNeeded = Math.Ceiling(RemainingTime.TotalMinutes / avgMinutesPerActiveDay);
            return DateTime.Today.AddDays(daysNeeded);
        }
    }

    public bool ProjectedToMissDeadline =>
        EstimatedCompletionDate.HasValue && EstimatedCompletionDate.Value.Date > GoalEndDate.Date;

    private static string FormatHm(TimeSpan t) => $"{(int)t.TotalHours}h {t.Minutes}m";

    public ObservableCollection<double> ActualBurndownValues
    {
        get
        {
            var ordered = Sessions.OrderBy(s => s.Date).ToList();
            var values = new ObservableCollection<double>();
            var remaining = TotalDuration.TotalHours;
            values.Add(Math.Round(remaining, 1));
            foreach (var s in ordered)
            {
                remaining -= s.DurationWatched.TotalHours;
                values.Add(Math.Round(Math.Max(remaining, 0), 1));
            }
            return values;
        }
    }

    public ObservableCollection<double> TargetTrajectoryValues
    {
        get
        {
            var points = Math.Max(ActualBurndownValues.Count, 2);
            var values = new ObservableCollection<double>();
            var start = TotalDuration.TotalHours;
            for (int i = 0; i < points; i++)
            {
                var fraction = (double)i / (points - 1);
                values.Add(Math.Round(Math.Max(start * (1 - fraction), 0), 1));
            }
            return values;
        }
    }

    public ISeries[] BurndownSeries => new ISeries[]
    {
        new LineSeries<double>
        {
            Values = ActualBurndownValues,
            Name = "Hours Remaining",
            Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(
                new SkiaSharp.SKColor(0x63, 0x66, 0xF1), 3),
            Fill = null,
            GeometrySize = 8
        },
        new LineSeries<double>
        {
            Values = TargetTrajectoryValues,
            Name = "Target Path",
            Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(
                new SkiaSharp.SKColor(0x9C, 0xA3, 0xAF), 2),
            Fill = null,
            GeometrySize = 0
        }
    };

    public Axis[] BurndownXAxes => new Axis[]
    {
        new Axis
        {
            Labels = Sessions.OrderBy(s => s.Date).Select(s => s.Date.ToString("MMM d")).Prepend("Start").ToArray(),
            LabelsRotation = 0,
            ForceStepToMin = true,
            MinStep = 1
        }
    };

    public Axis[] BurndownYAxes => new Axis[]
    {
        new Axis
        {
            Labeler = value => $"{value:0.0}h"
        }
    };

    [RelayCommand]
    private async Task SaveProgressAsync()
    {
        var duration = new TimeSpan(LogHours, LogMinutes, 0);
        if (duration <= TimeSpan.Zero) return;

        var newSession = new StudySession
        {
            CourseId = Id,
            Date = LogDate,
            DurationWatched = duration,
            Notes = string.IsNullOrWhiteSpace(LogNotes) ? null : LogNotes
        };

        await using var db = _dbFactory();
        db.StudySessions.Add(newSession);
        await db.SaveChangesAsync();

        Sessions.Insert(0, newSession);
        RaiseAllMetricsChanged();

        LogHours = 0;
        LogMinutes = 0;
        LogNotes = null;
        LogDate = DateTime.Today;
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(StudySession? session)
    {
        if (session is null) return;

        await using var db = _dbFactory();
        db.StudySessions.Attach(session);
        db.StudySessions.Remove(session);
        await db.SaveChangesAsync();

        Sessions.Remove(session);
        RaiseAllMetricsChanged();
    }

    public bool IsTodayCheckedIn => Sessions.Any(s => s.Date.Date == DateTime.Today);

    [RelayCommand]
    private async Task MarkTodayDoneAsync()
    {
        if (IsTodayCheckedIn) return;

        var pace = DynamicDailyPace;
        if (pace <= TimeSpan.Zero) return;

        var session = new StudySession
        {
            CourseId = Id,
            Date = DateTime.Today,
            DurationWatched = pace,
            Notes = "Daily goal check-in",
            IsAutoFilled = false
        };

        await using var db = _dbFactory();
        db.StudySessions.Add(session);
        await db.SaveChangesAsync();

        Sessions.Insert(0, session);
        RaiseAllMetricsChanged();
        OnPropertyChanged(nameof(IsTodayCheckedIn));
    }

    /// <summary>Applies edited core fields (used by the Edit Course flow) and
    /// refreshes every derived metric that depends on them.</summary>
    public void ApplyEdit(string newTitle, TimeSpan newTotalDuration, DateTime newGoalEndDate,
        CoursePriority newPriority, int newScheduledDaysMask)
    {
        Title = newTitle;
        TotalDuration = newTotalDuration;
        GoalEndDate = newGoalEndDate;
        Priority = newPriority;
        ScheduledDaysMask = newScheduledDaysMask;
        RaiseAllMetricsChanged();
    }

    public void RefreshMetrics() => RaiseAllMetricsChanged();

    private void RaiseAllMetricsChanged()
    {
        OnPropertyChanged(nameof(TotalWatched));
        OnPropertyChanged(nameof(RemainingTime));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ProgressPercentageDisplay));
        OnPropertyChanged(nameof(RemainingDays));
        OnPropertyChanged(nameof(RemainingScheduledDays));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(DynamicDailyPace));
        OnPropertyChanged(nameof(DynamicDailyPaceDisplay));
        OnPropertyChanged(nameof(EstimatedCompletionDate));
        OnPropertyChanged(nameof(ProjectedToMissDeadline));
        OnPropertyChanged(nameof(IsTodayCheckedIn));
        OnPropertyChanged(nameof(ActualBurndownValues));
        OnPropertyChanged(nameof(TargetTrajectoryValues));
        OnPropertyChanged(nameof(BurndownSeries));
        OnPropertyChanged(nameof(BurndownXAxes));
        OnPropertyChanged(nameof(BurndownYAxes));
    }
}