using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SpaceSense.Core;

namespace SpaceSense.Desktop;

public partial class MainWindow : Window
{
    private readonly AssessmentEngine assessments = new(AssessmentContext.Local());
    private readonly Stack<StorageNode> navigation = new();
    private CancellationTokenSource? scanCancellation;
    private StorageNode? currentFolder;
    private bool closed;

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => { closed = true; scanCancellation?.Cancel(); };
    }

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose a folder or drive to inspect", Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation;
        ChooseButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        ScanActivity.Visibility = Visibility.Visible;
        ScanPath.Text = picker.FolderName;
        FolderTree.Items.Clear();
        ItemsGrid.ItemsSource = null;
        SizeMap.Children.Clear();
        SizeMap.ColumnDefinitions.Clear();
        navigation.Clear();
        currentFolder = null;
        UpButton.IsEnabled = false;
        TotalSize.Text = "Scanning…";
        ScanSummary.Text = "Reading file names, locations, and sizes.";
        CurrentFolder.Text = "Scanning…";
        DetailName.Text = "Scan in progress";
        DetailPath.Text = "Select an item when the scan finishes.";
        AssessmentLabel.Text = "Awaiting selection";
        PurposeText.Text = ImpactText.Text = RecommendationText.Text = "—";
        EvidenceText.Text = "Local rules. No file contents are read.";
        ConfidenceText.Text = "";
        WarningsPanel.Visibility = Visibility.Collapsed;
        var timer = Stopwatch.StartNew();
        long lastUpdate = -200;
        var progress = new Progress<ScanProgress>(update =>
        {
            if (closed || scanCancellation != cancellation || cancellation.IsCancellationRequested) return;
            StatusText.Text = $"Scanning {update.Items:N0} items · {update.CurrentPath}";
        });
        // Throttle reports on the worker before they reach the UI message queue.
        var throttled = new CallbackProgress<ScanProgress>(update =>
        {
            if (timer.ElapsedMilliseconds - lastUpdate < 150) return;
            lastUpdate = timer.ElapsedMilliseconds;
            ((IProgress<ScanProgress>)progress).Report(update);
        });
        try
        {
            StatusText.Text = "Scanning… You can cancel at any time.";
            var result = await Task.Run(() => new StorageScanner().Scan(picker.FolderName, throttled, cancellation.Token));
            if (closed) return;
            TotalSize.Text = StorageNode.FormatBytes(result.Root.Bytes);
            ScanSummary.Text = $"{result.Items:N0} items inspected · {(result.Root.IsIncomplete ? "Partial scan — some sizes are missing" : "Scan complete")}";
            var treeRoot = MakeTreeItem(result.Root);
            FolderTree.Items.Add(treeRoot);
            treeRoot.IsExpanded = true;
            treeRoot.IsSelected = true;
            Browse(result.Root);
            ShowAssessment(result.Root);
            StatusText.Text = $"Finished in {timer.Elapsed.TotalSeconds:0.0}s · {result.UnreadItems:N0} unread/skipped entries reported" +
                (result.LimitReached ? " · 250,000-item limit reached; scan a smaller folder" : "");
            if (result.Warnings.Count > 0)
            {
                WarningsText.Text = string.Join(Environment.NewLine, result.Warnings) +
                    (result.UnreadItems > result.Warnings.Count ? Environment.NewLine + "Additional scan notes omitted." : "");
                WarningsPanel.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException)
        {
            if (closed) return;
            TotalSize.Text = "—";
            ScanSummary.Text = "Scan cancelled. Choose a folder to start again.";
            CurrentFolder.Text = "No completed scan";
            DetailName.Text = "Scan cancelled";
            StatusText.Text = "Cancelled · No files were changed.";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            if (closed) return;
            TotalSize.Text = "—";
            ScanSummary.Text = "This location could not be scanned.";
            CurrentFolder.Text = "No completed scan";
            DetailName.Text = "Scan unavailable";
            StatusText.Text = ex.Message;
        }
        finally
        {
            scanCancellation = null;
            if (!closed)
            {
                ChooseButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                ScanActivity.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        scanCancellation?.Cancel();
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling…";
    }

    private TreeViewItem MakeTreeItem(StorageNode node)
    {
        var item = new TreeViewItem { Header = node.Name, Tag = node, ToolTip = node.FullPath };
        if (node.Children.Any(child => child.IsDirectory))
        {
            item.Items.Add("Expand to browse");
            item.Expanded += (_, e) =>
            {
                if (e.OriginalSource != item) return;
                if (item.Items.Count == 1 && item.Items[0] is string)
                {
                    item.Items.Clear();
                    foreach (var child in node.Children.Where(child => child.IsDirectory)) item.Items.Add(MakeTreeItem(child));
                }
                e.Handled = true;
            };
        }
        return item;
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem { Tag: StorageNode node }) return;
        navigation.Clear();
        Browse(node);
        ShowAssessment(node);
    }

    private void Browse(StorageNode folder)
    {
        currentFolder = folder;
        CurrentFolder.Text = folder.FullPath;
        CurrentFolder.ToolTip = folder.FullPath;
        UpButton.IsEnabled = navigation.Count > 0;
        ItemsGrid.ItemsSource = folder.Children;
        SizeMap.Children.Clear();
        SizeMap.ColumnDefinitions.Clear();
        string[] palette = ["#68DCC1", "#7AA7FC", "#B09AF5", "#F1BC74", "#7DC9E3", "#DE91AB"];
        var top = folder.Children.Where(node => node.Bytes > 0).Take(10).ToList();
        for (int i = 0; i < top.Count; i++)
        {
            var node = top[i];
            SizeMap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(node.Bytes, GridUnitType.Star) });
            var button = new Button
            {
                Background = (Brush)new BrushConverter().ConvertFromString(palette[i % palette.Length])!,
                Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 0), MinWidth = 0,
                ToolTip = $"{node.Name} · {node.SizeLabel}"
            };
            System.Windows.Automation.AutomationProperties.SetName(button, $"Inspect {node.Name}, {node.SizeLabel}");
            button.Click += (_, _) => { ItemsGrid.SelectedItem = node; ItemsGrid.ScrollIntoView(node); ShowAssessment(node); };
            Grid.SetColumn(button, i);
            SizeMap.Children.Add(button);
        }
        long otherBytes = folder.Bytes - top.Sum(node => node.Bytes);
        if (otherBytes > 0)
        {
            SizeMap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(otherBytes, GridUnitType.Star) });
            var other = new Border { Background = new SolidColorBrush(Color.FromRgb(65, 79, 102)), ToolTip = $"Other items · {StorageNode.FormatBytes(otherBytes)}" };
            Grid.SetColumn(other, top.Count);
            SizeMap.Children.Add(other);
        }
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (!navigation.TryPop(out var parent)) return;
        Browse(parent);
        ShowAssessment(parent);
    }

    private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is StorageNode node) ShowAssessment(node);
    }

    private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // A double-click on an empty area or a column header must not navigate a stale selection.
        if (e.OriginalSource is not DependencyObject source || FindRow(source) is not { Item: StorageNode node }) return;
        if (!node.IsDirectory || node.IsLink || currentFolder is null) return;
        navigation.Push(currentFolder);
        Browse(node);
        ShowAssessment(node);
    }

    private static DataGridRow? FindRow(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is DataGridRow row) return row;
        return null;
    }

    private void ShowAssessment(StorageNode node)
    {
        var assessment = assessments.Assess(node);
        DetailName.Text = node.Name;
        DetailPath.Text = node.FullPath;
        AssessmentLabel.Text = assessment.Label;
        AssessmentLabel.Foreground = new SolidColorBrush(assessment.Level switch
        {
            AssessmentLevel.LikelyRemovable => Color.FromRgb(128, 226, 201),
            AssessmentLevel.KeepOrUseSupportedCleanup => Color.FromRgb(244, 170, 160),
            _ => Color.FromRgb(241, 202, 139)
        });
        PurposeText.Text = assessment.Purpose;
        ImpactText.Text = assessment.DeletionImpact;
        RecommendationText.Text = assessment.Recommendation;
        EvidenceText.Text = assessment.Evidence + (node.IsIncomplete ? ". Scan incomplete; displayed size is partial." : ".");
        ConfidenceText.Text = assessment.Confidence;
    }

    private sealed class CallbackProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
