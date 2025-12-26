using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using CyberSentra.Database;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CyberSentra
{
    public partial class HistoricalThreatsView : UserControl
    {
        // All threats loaded from DB
        private List<ThreatInfo> _allThreats = new();

        // Filtered threats for the grid
        public ObservableCollection<ThreatInfo> Threats { get; } = new();

        private enum HistoryTimeRange
        {
            All,
            Last24h,
            Last7d
        }

        private HistoryTimeRange _currentRange = HistoryTimeRange.All;
        private string _currentSeverity = "All";

        // Small summary label in filter bar
        public string FilterSummary => $"Showing {Threats.Count} record(s)";

        public HistoricalThreatsView()
        {
            InitializeComponent();
            DataContext = this;
            LoadData();
        }

        private void LoadData()
        {
            Threats.Clear();

            // Load from SQLite (already limited in ThreatRepository)
            _allThreats = ThreatRepository.LoadThreatHistory();

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Threats.Clear();

            DateTime now = DateTime.Now;
            DateTime? cutoff = null;

            if (_currentRange == HistoryTimeRange.Last24h)
                cutoff = now.AddHours(-24);
            else if (_currentRange == HistoryTimeRange.Last7d)
                cutoff = now.AddDays(-7);

            foreach (var t in _allThreats)
            {
                // Time filter
                if (cutoff.HasValue)
                {
                    if (!DateTime.TryParse(t.Time, out var tTime))
                        continue;

                    if (tTime < cutoff.Value)
                        continue;
                }

                // Severity filter
                if (!string.Equals(_currentSeverity, "All", StringComparison.OrdinalIgnoreCase))
                {
                    if (!t.Severity.Equals(_currentSeverity, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                Threats.Add(t);
            }

            // quick refresh of FilterSummary (no MVVM)
            DataContext = null;
            DataContext = this;
        }

        private void TimeFilterButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn)
                return;

            if (btn == AllTimeButton)
                _currentRange = HistoryTimeRange.All;
            else if (btn == Last24hButton)
                _currentRange = HistoryTimeRange.Last24h;
            else if (btn == Last7dButton)
                _currentRange = HistoryTimeRange.Last7d;

            // Ensure only one is checked
            AllTimeButton.IsChecked = btn == AllTimeButton;
            Last24hButton.IsChecked = btn == Last24hButton;
            Last7dButton.IsChecked = btn == Last7dButton;

            ApplyFilter();
        }

        private void SeverityFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            {
                _currentSeverity = item.Content?.ToString() ?? "All";
                ApplyFilter();
            }
        }

        // View full details dialog (Avalonia-safe)
        private async void ViewThreatDetails_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control c) return;
            if (c.DataContext is not ThreatInfo t) return;

            var textBox = new TextBox
            {
                Text = t.Details ?? "",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            var scroll = new ScrollViewer { Content = textBox };

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
                Text = "Threat Details",
                FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"{t.Time} • {t.Technique} • {t.Severity} • {t.Name}",
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
            if (owner != null) await win.ShowDialog(owner);
            else win.Show();
        }
    }
}
