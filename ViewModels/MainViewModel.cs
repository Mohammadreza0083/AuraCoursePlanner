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

    // Add/Edit-course form (the same form is reused for both flows)
    [ObservableProperty] private string newCourseTitle = string.Empty;
    [ObservableProperty] private int newCourseHours;
    [ObservableProperty] private int newCourseMinutes;
    [ObservableProperty] private CoursePriority newCoursePriority = CoursePriority.Medium;

    /// <summary>Null while adding a new course; set to the course being edited
    /// while the Add/Edit form is open in edit mode.</summary>
    [ObservableProperty] private Guid? editingCourseId;

    public bool IsEditingCourse => EditingCourseId.HasValue;
    public string AddCourseFormTitle => IsEditingCourse ? "Edit Course" : "Add Course";

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

    partial void OnEditingCourseIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(IsEditingCourse));
        OnPropertyChanged(nameof(AddCourseFormTitle));
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
        ApplyPrioritySort();
        RaiseSummaryChanged();
    }

    /// <summary>Prioritization feature: reorders the dashboard so High-priority,
    /// not-yet-completed courses surface first, then by soonest deadline within
    /// the same priority tier. Completed courses sink to the bottom.</summary>
    private void ApplyPrioritySort()
    {
        var sorted = Courses
            .OrderBy(c => c.IsCompleted)
            .ThenBy(c => c.PriorityRank)
            .ThenBy(c => c.GoalEndDate)
            .ToList();

        Courses = new ObservableCollection<CourseViewModel>(sorted);
    }

    [RelayCommand]
    private void SortByPriority() => ApplyPrioritySort();

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

    /// <summary>Opens the Add/Edit form in "add" mode with blank fields.</summary>
    [RelayCommand]
    private void StartAddCourse()
    {
        EditingCourseId = null;
        NewCourseTitle = string.Empty;
        NewCourseHours = 0;
        NewCourseMinutes = 0;
        NewCoursePriority = CoursePriority.Medium;
        SelectedDay = 0;
        SelectedMonth = 0;
        SelectedYear = 0;
        UpdateCalculatedDate();
        CurrentView = AppView.AddCourse;
    }

    /// <summary>Opens the Add/Edit form pre-filled with an existing course's
    /// values so the user can change them (the Edit Course feature).</summary>
    [RelayCommand]
    private void StartEditCourse(CourseViewModel? course)
    {
        if (course is null) return;

        EditingCourseId = course.Id;
        NewCourseTitle = course.Title;
        NewCourseHours = (int)course.TotalDuration.TotalHours;
        NewCourseMinutes = course.TotalDuration.Minutes;
        NewCoursePriority = course.Priority;

        // Re-derive the day/month/year offset fields from the stored deadline
        // so the same relative-date picker used for Add works for Edit too.
        var today = DateTime.Today;
        var goal = course.GoalEndDate.Date;
        var months = ((goal.Year - today.Year) * 12) + (goal.Month - today.Month);
        var approxMonthDate = today.AddMonths(months);
        var days = (goal - approxMonthDate).Days;
        if (days < 0) { months -= 1; approxMonthDate = today.AddMonths(months); days = (goal - approxMonthDate).Days; }

        SelectedYear = Math.Clamp(months / 12, 0, 10);
        SelectedMonth = Math.Clamp(months % 12, 0, 12);
        SelectedDay = Math.Clamp(days, 1, 31);
        UpdateCalculatedDate();

        CurrentView = AppView.AddCourse;
    }

    [RelayCommand]
    private void CancelAddCourse()
    {
        EditingCourseId = null;
        CurrentView = AppView.Dashboard;
    }

    [RelayCommand]
    private async Task AddCourseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCourseTitle)) return;

        // ذخیره با مقادیر کلمپ شده
        DateTime finalDate = DateTime.Today
            .AddDays(SelectedDay)
            .AddMonths(SelectedMonth)
            .AddYears(SelectedYear);

        var duration = new TimeSpan(NewCourseHours, NewCourseMinutes, 0);
        var title = NewCourseTitle.Trim();

        await using var db = _dbFactory();

        if (EditingCourseId is Guid editId)
        {
            var entity = await db.Courses.FirstOrDefaultAsync(c => c.Id == editId);
            if (entity is not null)
            {
                entity.Title = title;
                entity.TotalDuration = duration;
                entity.GoalEndDate = finalDate;
                entity.Priority = NewCoursePriority;
                await db.SaveChangesAsync();

                var existingVm = Courses.FirstOrDefault(c => c.Id == editId);
                existingVm?.ApplyEdit(title, duration, finalDate, NewCoursePriority);
            }
        }
        else
        {
            var course = new Course
            {
                Title = title,
                TotalDuration = duration,
                GoalEndDate = finalDate,
                Priority = NewCoursePriority
            };

            db.Courses.Add(course);
            await db.SaveChangesAsync();

            Courses.Add(new CourseViewModel(course, _dbFactory));
        }

        ApplyPrioritySort();
        RaiseSummaryChanged();

        // Reset inputs
        EditingCourseId = null;
        NewCourseTitle = string.Empty;
        NewCourseHours = 0;
        NewCourseMinutes = 0;
        NewCoursePriority = CoursePriority.Medium;
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
        if (EditingCourseId == course.Id)
        {
            // Someone deleted the course they were mid-edit on; bail out of the form.
            EditingCourseId = null;
            CurrentView = AppView.Dashboard;
        }
        RaiseSummaryChanged();
    }

    private void RaiseSummaryChanged()
    {
        OnPropertyChanged(nameof(TotalActiveCourses));
        OnPropertyChanged(nameof(TotalHoursWatched));
        OnPropertyChanged(nameof(OverallCompletionRate));
    }
}