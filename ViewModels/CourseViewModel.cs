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
    public DateTime CreatedAt { get; }

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
        sessions = new ObservableCollection<StudySession>(
            course.StudySessions.OrderByDescending(s => s.Date));
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

    public bool IsOverdue => (GoalEndDate.Date - DateTime.Today).Days < 0 && RemainingTime > TimeSpan.Zero;

    public bool IsCompleted => TotalWatched >= TotalDuration && TotalDuration > TimeSpan.Zero;

    public TimeSpan DynamicDailyPace
    {
        get
        {
            if (IsCompleted) return TimeSpan.Zero;
            if (RemainingDays <= 0)
            {
                return RemainingTime;
            }
            var minutesPerDay = RemainingTime.TotalMinutes / RemainingDays;
            return TimeSpan.FromMinutes(Math.Max(minutesPerDay, 0));
        }
    }

    public string DynamicDailyPaceDisplay =>
        IsCompleted ? "✅ Completed" : $"⚡ Target: {FormatHm(DynamicDailyPace)}/day";

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

    public void RefreshMetrics() => RaiseAllMetricsChanged();

    private void RaiseAllMetricsChanged()
    {
        OnPropertyChanged(nameof(TotalWatched));
        OnPropertyChanged(nameof(RemainingTime));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ProgressPercentageDisplay));
        OnPropertyChanged(nameof(RemainingDays));
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