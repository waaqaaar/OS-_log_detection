using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CyberSentra
{
    public partial class ThreatsView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private List<ThreatInfo> _allThreats = new();
        public ObservableCollection<ThreatInfo> Threats { get; } = new();

        public string TotalThreatsText => $"Total threats: {Threats.Count}";

        public string HighSeverityText
        {
            get
            {
                var count = Threats.Count(t => t.Severity.Equals("High", StringComparison.OrdinalIgnoreCase));
                return $"High severity: {count}";
            }
        }

        private string _currentSeverity = "All";
        private string _techniqueFilter = string.Empty;

        public ThreatsView()
        {
            InitializeComponent();
            DataContext = this;
            LoadThreatsFromEvents();
        }

        private void LoadThreatsFromEvents()
        {
            var events = EventContext.GetCurrentEvents();
            _allThreats = ThreatDetector.GetThreats(events);

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Threats.Clear();

            foreach (var t in _allThreats)
            {
                if (!string.Equals(_currentSeverity, "All", StringComparison.OrdinalIgnoreCase) &&
                    !t.Severity.Equals(_currentSeverity, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(_techniqueFilter) &&
                    !t.Technique.Contains(_techniqueFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                Threats.Add(t);
            }

            OnPropertyChanged(nameof(TotalThreatsText));
            OnPropertyChanged(nameof(HighSeverityText));
        }

        private void SeverityCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            {
                _currentSeverity = item.Content?.ToString() ?? "All";
                ApplyFilter();
            }
        }

        private void TechniqueSearchBox_KeyUp(object? sender, KeyEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _techniqueFilter = tb.Text ?? string.Empty;
                ApplyFilter();
            }
        }

        private async void ViewThreatDetails_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control c) return;
            if (c.DataContext is not ThreatInfo t) return;

            var tb = new TextBox
            {
                Text = t.Details ?? "",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
               // VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                //HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var copyBtn = new Button { Content = "Copy", HorizontalAlignment = HorizontalAlignment.Right };
            copyBtn.Click += async (_, __) =>
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                    await top.Clipboard.SetTextAsync(tb.Text);
            };

            var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = "Threat Details", FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold });
            panel.Children.Add(new TextBlock
            {
                Text = $"{t.Time} • {t.Technique} • {t.Tactic} • {t.Severity}",
                Opacity = 0.8,
                FontSize = 12
            });
            panel.Children.Add(copyBtn);
            panel.Children.Add(tb);

            var win = new Window
            {
                Title = "Threat Details",
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
