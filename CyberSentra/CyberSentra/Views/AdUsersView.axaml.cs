using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CyberSentra
{
    public partial class AdUsersView : UserControl
    {
        // Full list (all users, unfiltered)
        private List<UserRiskInfo> _allUsers = new();

        // What the grid shows
        public ObservableCollection<UserRiskInfo> Users { get; } = new();

        // Events for the selected user
        public ObservableCollection<UserEventInfo> SelectedUserEvents { get; } = new();

        private DataGrid? _usersGrid;

        public AdUsersView()
        {
            InitializeComponent();

            _usersGrid = this.FindControl<DataGrid>("UsersGrid");

            DataContext = this;
            LoadUsersFromEvents();
        }

        private void LoadUsersFromEvents()
        {
            var events = EventContext.GetCurrentEvents();
            _allUsers = UserRiskCalculator.GetUserRisks(events);

            ApplyFilter(string.Empty);
        }
        private void UpdateSelectedUserEvents(UserRiskInfo? user)
        {
            SelectedUserEvents.Clear();

            if (user == null)
                return;

            var allEvents = EventContext.GetCurrentEvents();

            var matching = allEvents.Where(ev =>
            {
                if (string.IsNullOrWhiteSpace(ev.User))
                    return false;

                var evUser = ev.User.Trim();
                var parts = evUser.Split('\\');
                var evUserName = parts.Length == 2 ? parts[1] : evUser;

                return evUserName.Equals(user.UserName, System.StringComparison.OrdinalIgnoreCase);
            });

            foreach (var ev in matching.Take(20))
            {
                SelectedUserEvents.Add(new UserEventInfo
                {
                    Time = ev.Time,
                    Type = ev.Type,
                    Severity = ev.Severity,
                    Process = ev.Process,
                    Details = ev.Details
                });
            }
        }

        /// <summary>
        /// Apply search filter to Users based on text.
        /// </summary>
        private void ApplyFilter(string searchText)
        {
            Users.Clear();

            IEnumerable<UserRiskInfo> filtered;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                filtered = _allUsers;
            }
            else
            {
                var term = searchText.Trim().ToLowerInvariant();

                filtered = _allUsers.Where(u =>
                    (!string.IsNullOrEmpty(u.UserName) && u.UserName.ToLowerInvariant().Contains(term)) ||
                    (!string.IsNullOrEmpty(u.DisplayName) && u.DisplayName.ToLowerInvariant().Contains(term)) ||
                    (!string.IsNullOrEmpty(u.Domain) && u.Domain.ToLowerInvariant().Contains(term)) ||
                    (!string.IsNullOrEmpty(u.Email) && u.Email.ToLowerInvariant().Contains(term)));
            }

            foreach (var u in filtered)
            {
                Users.Add(u);
            }

            // When filter changes, auto-select first user (if any) and update details
            // Auto-select the highest risk user (best default for SOC)
            if (_usersGrid != null && Users.Count > 0)
            {
                var top = Users
                    .OrderByDescending(u => u.RiskScore)
                    .First();

                _usersGrid.SelectedItem = top;
                UpdateSelectedUserEvents(top);
            }
            else
            {
                SelectedUserEvents.Clear();
            }

           
        }

        private void UsersGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid grid)
                return;

            var selected = grid.SelectedItem as UserRiskInfo;
            UpdateSelectedUserEvents(selected);
        }
        private async void ViewUserEventDetails_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not Control c) return;
            if (c.DataContext is not UserEventInfo ev) return;

            var textBox = new TextBox
            {
                Text = ev.Details ?? "",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            var scroll = new ScrollViewer
            {
                Content = textBox
            };

            var copyBtn = new Button
            {
                Content = "Copy",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            copyBtn.Click += async (_, __) =>
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                    await top.Clipboard.SetTextAsync(textBox.Text);
            };

            var panel = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(14)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Event Details",
                FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"{ev.Time} • {ev.Type} • {ev.Severity} • {ev.Process}",
                FontSize = 12,
                Opacity = 0.7
            });

            panel.Children.Add(copyBtn);
            panel.Children.Add(scroll);

            var win = new Window
            {
                Title = "Details",
                Width = 900,
                Height = 520,
                Content = panel
            };

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();
        }


        // Search box text changed
        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                ApplyFilter(tb.Text ?? string.Empty);
            }
        }

        public class UserEventInfo
        {
            public string Time { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Severity { get; set; } = string.Empty;
            public string Process { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
        }
    }

}

