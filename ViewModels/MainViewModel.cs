using AuraCoursePlanner.Data;
using AuraCoursePlanner.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AuraCoursePlanner.ViewModels;

public enum AppView { Dashboard, AddCourse, Analytics }

public partial class MainViewModel : ObservableObject
{
    private readonly Func<AuraDbContext> _dbFactory;

    [ObservableProperty] private AppView currentView = AppView.Dashboard;
    [ObservableProperty] private ObservableCollection<CourseViewModel> courses = new();
    [ObservableProperty] private CourseViewModel? selectedCourse;
    [ObservableProperty] private CourseViewModel? selectedAnalyticsCourse;

    // Add-course form
    [ObservableProperty] private string newCourseTitle = string.Empty;
    [ObservableProperty] private int newCourseHours;
    [ObservableProperty] private int newCourseMinutes;

    // Date inputs (Manual properties with Clamping)
    private int _selectedDay = 0;
    public int SelectedDay
    {
        get => _selectedDay;
        set { if (SetProperty(ref _selectedDay, Math.Clamp(value, 1, 31))) UpdateCalculatedDate(); }
    }

    private int _selectedMonth = 0;
    public int SelectedMonth
    {
        get => _selectedMonth;
        set { if (SetProperty(ref _selectedMonth, Math.Clamp(value, 0, 12))) UpdateCalculatedDate(); }
    }

    private int _selectedYear = 0;
    public int SelectedYear
    {
        get => _selectedYear;
        set { if (SetProperty(ref _selectedYear, Math.Clamp(value, 0, 10))) UpdateCalculatedDate(); }
    }

    [ObservableProperty]
    private string calculatedDateString = DateTime.Today.ToString("dd/MM/yyyy");

    private void UpdateCalculatedDate()
    {
        // محاسبه تاریخ بر اساس مقادیر فعلی
        DateTime result = DateTime.Today
            .AddDays(SelectedDay)
            .AddMonths(SelectedMonth)
            .AddYears(SelectedYear);

        CalculatedDateString = result.ToString("dd/MM/yyyy");
    }

    // Dashboard summary metrics
    public int TotalActiveCourses => Courses.Count(c => !c.IsCompleted);
    public double TotalHoursWatched => Courses.Sum(c => c.TotalWatched.TotalHours);
    public double OverallCompletionRate =>
        Courses.Count == 0 ? 0 : Courses.Average(c => c.ProgressPercentage);

    public MainViewModel(Func<AuraDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    private async Task LoadCoursesAsync()
    {
        await using var db = _dbFactory();
        var courseEntities = await db.Courses
            .Include(c => c.StudySessions)
            .AsNoTracking()
            .ToListAsync();

        Courses = new ObservableCollection<CourseViewModel>(
            courseEntities.Select(c => new CourseViewModel(c, _dbFactory)));

        await BackfillMissedDaysAsync();
        RaiseSummaryChanged();
    }

    private async Task BackfillMissedDaysAsync()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        await using var db = _dbFactory();
        var anyInserted = false;

        foreach (var course in Courses)
        {
            if (course.IsCompleted) continue;

            var courseSpanDays = Math.Max((course.GoalEndDate.Date - course.CreatedAt.Date).Days, 1);
            var flatDailyTarget = TimeSpan.FromMinutes(course.TotalDuration.TotalMinutes / courseSpanDays);
            if (flatDailyTarget <= TimeSpan.Zero) continue;

            var loggedDates = course.Sessions.Select(s => s.Date.Date).ToHashSet();
            var cursor = course.CreatedAt.Date.AddDays(1);

            while (cursor <= yesterday)
            {
                if (!loggedDates.Contains(cursor))
                {
                    var autoSession = new StudySession
                    {
                        CourseId = course.Id,
                        Date = cursor,
                        DurationWatched = flatDailyTarget,
                        Notes = "Auto-filled — goal not checked in time",
                        IsAutoFilled = true
                    };
                    db.StudySessions.Add(autoSession);
                    course.Sessions.Add(autoSession);
                    anyInserted = true;
                }
                cursor = cursor.AddDays(1);
            }

            if (anyInserted) course.RefreshMetrics();
        }

        if (anyInserted) await db.SaveChangesAsync();
    }

    [RelayCommand]
    private void NavigateTo(AppView view) => CurrentView = view;

    [RelayCommand]
    private void SelectCourse(CourseViewModel course)
    {
        SelectedCourse = course;
        CurrentView = AppView.Dashboard;
    }

    [RelayCommand]
    private void CloseCourseDetail() => SelectedCourse = null;

    [RelayCommand]
    private async Task AddCourseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCourseTitle)) return;

        // ذخیره با مقادیر کلمپ شده
        DateTime finalDate = DateTime.Today
            .AddDays(SelectedDay)
            .AddMonths(SelectedMonth)
            .AddYears(SelectedYear);

        var course = new Course
        {
            Title = NewCourseTitle.Trim(),
            TotalDuration = new TimeSpan(NewCourseHours, NewCourseMinutes, 0),
            GoalEndDate = finalDate
        };

        await using var db = _dbFactory();
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        Courses.Add(new CourseViewModel(course, _dbFactory));
        RaiseSummaryChanged();

        // Reset inputs
        NewCourseTitle = string.Empty;
        NewCourseHours = 0;
        NewCourseMinutes = 0;
        SelectedDay = 0;
        SelectedMonth = 0;
        SelectedYear = 0;
        UpdateCalculatedDate(); // Reset the date display

        CurrentView = AppView.Dashboard;
    }

    [RelayCommand]
    private async Task DeleteCourseAsync(CourseViewModel? course)
    {
        if (course is null) return;

        await using var db = _dbFactory();
        var entity = await db.Courses.FirstOrDefaultAsync(c => c.Id == course.Id);
        if (entity is null) return;

        db.Courses.Remove(entity);
        await db.SaveChangesAsync();

        Courses.Remove(course);
        if (SelectedCourse == course) SelectedCourse = null;
        if (SelectedAnalyticsCourse == course) SelectedAnalyticsCourse = null;
        RaiseSummaryChanged();
    }

    private void RaiseSummaryChanged()
    {
        OnPropertyChanged(nameof(TotalActiveCourses));
        OnPropertyChanged(nameof(TotalHoursWatched));
        OnPropertyChanged(nameof(OverallCompletionRate));
    }
}