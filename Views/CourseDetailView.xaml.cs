using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace AuraCoursePlanner.Views
{
    public partial class CourseDetailView : UserControl
    {
        public CourseDetailView()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (ExecuteCloseCommand(this.DataContext)) return;

            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is FrameworkElement fe && fe.DataContext != null)
                {
                    if (ExecuteCloseCommand(fe.DataContext)) return;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        private bool ExecuteCloseCommand(object context)
        {
            if (context == null) return false;

            var prop = context.GetType().GetProperty("CloseCourseDetailCommand");
            if (prop != null)
            {
                if (prop.GetValue(context) is ICommand command)
                {
                    if (command.CanExecute(null))
                    {
                        command.Execute(null);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}