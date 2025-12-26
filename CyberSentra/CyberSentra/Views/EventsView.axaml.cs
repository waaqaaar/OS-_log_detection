using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace CyberSentra
{
    public partial class EventsView : UserControl
    {
        public ObservableCollection<EventRecord> Events { get; } = new();

        private bool _useNoiseFilter = false;

        // Source modes
        private enum SourceMode { All, SysmonOnly, EventViewerOnly }
        private SourceMode _sourceMode = SourceMode.EventViewerOnly;

        public EventsView()
        {
            InitializeComponent();
            DataContext = this;

            // default selection
            SysmonBtn.IsChecked = false;
            EventViewerBtn.IsChecked = true;

            LoadEventsFromSource();
        }

        private void LoadEventsFromSource()
        {
            Events.Clear();

            string? include = null;
            string? exclude = null;

            if (_sourceMode == SourceMode.SysmonOnly)
                include = "sysmon";
            else if (_sourceMode == SourceMode.EventViewerOnly)
                exclude = "sysmon";

            var events = EventContext.GetCurrentEvents(
                applyNoiseFilter: _useNoiseFilter,
                sourceContains: include,
                sourceNotContains: exclude);

            foreach (var ev in events)
                Events.Add(ev);
        }

        private void FilterButton_Click(object? sender, RoutedEventArgs e)
        {
            _useNoiseFilter = !_useNoiseFilter;

            FilterButton.Content = _useNoiseFilter ? "🔎 Filter (ON)" : "🔎 Filter";
            LoadEventsFromSource();
        }

        private void SourceButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender == SysmonBtn)
            {
                SysmonBtn.IsChecked = true;
                EventViewerBtn.IsChecked = false;
                _sourceMode = SourceMode.SysmonOnly;
            }
            else if (sender == EventViewerBtn)
            {
                EventViewerBtn.IsChecked = true;
                SysmonBtn.IsChecked = false;
                _sourceMode = SourceMode.EventViewerOnly;
            }

            LoadEventsFromSource();
        }

        private async void ViewDetails_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control c) return;
            if (c.DataContext is not EventRecord ev) return;

            var tb = new TextBox
            {
                Text = ev.Details ?? "",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
              //  VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
               // HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var copyBtn = new Button { Content = "Copy", HorizontalAlignment = HorizontalAlignment.Right };
            copyBtn.Click += async (_, __) =>
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                    await top.Clipboard.SetTextAsync(tb.Text);
            };

            var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = "Event Details", FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold });
            panel.Children.Add(new TextBlock
            {
                Text = $"{ev.Time} • {ev.Type} • {ev.Severity} • {ev.User} • {ev.Source}",
                Opacity = 0.8,
                FontSize = 12
            });
            panel.Children.Add(copyBtn);
            panel.Children.Add(tb);

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
