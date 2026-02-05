using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TempleLampSystem.Helpers;

public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static readonly DependencyProperty TargetOffsetProperty =
        DependencyProperty.RegisterAttached(
            "TargetOffset",
            typeof(double),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(0.0));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
                element.PreviewMouseWheel += OnPreviewMouseWheel;
            else
                element.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        if (sender is not DependencyObject senderObj) return;

        // 找到事件來源元素
        var originalSource = e.OriginalSource as DependencyObject;

        // 找到 sender 自己的 ScrollViewer
        ScrollViewer? ownScrollViewer;
        if (senderObj is ScrollViewer sv)
            ownScrollViewer = sv;
        else
            ownScrollViewer = FindChildScrollViewer(senderObj);

        if (ownScrollViewer == null) return;

        // 檢查滑鼠位置下是否有「中間層」的其他可捲動 ScrollViewer
        // 如果有，讓那個 ScrollViewer 自己處理，不攔截
        if (originalSource != null && HasScrollableAncestorBefore(originalSource, ownScrollViewer))
            return;

        AnimateScroll(ownScrollViewer, e);
    }

    /// <summary>
    /// 檢查 source 往上找，在遇到 stopAt 之前是否有其他可捲動的 ScrollViewer
    /// </summary>
    private static bool HasScrollableAncestorBefore(DependencyObject source, ScrollViewer stopAt)
    {
        var current = source;
        while (current != null && current != stopAt)
        {
            if (current is ScrollViewer innerSv && innerSv != stopAt && innerSv.ScrollableHeight > 0)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static void AnimateScroll(ScrollViewer scrollViewer, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var currentTarget = (double)scrollViewer.GetValue(TargetOffsetProperty);

        if (Math.Abs(currentTarget) < 0.001 ||
            currentTarget < scrollViewer.VerticalOffset - 100 ||
            currentTarget > scrollViewer.VerticalOffset + 100)
        {
            currentTarget = scrollViewer.VerticalOffset;
        }

        var delta = e.Delta > 0 ? -80 : 80;
        var newTarget = Math.Max(0, Math.Min(currentTarget + delta, scrollViewer.ScrollableHeight));

        scrollViewer.SetValue(TargetOffsetProperty, newTarget);

        var animation = new DoubleAnimation
        {
            From = scrollViewer.VerticalOffset,
            To = newTarget,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            scrollViewer.ScrollToVerticalOffset(newTarget);
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);

        var helper = new ScrollAnimationHelper(scrollViewer);
        Storyboard.SetTarget(animation, helper);
        Storyboard.SetTargetProperty(animation, new PropertyPath(ScrollAnimationHelper.CurrentOffsetProperty));

        storyboard.Begin();
    }

    private static ScrollViewer? FindChildScrollViewer(DependencyObject element)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is ScrollViewer sv) return sv;
            var result = FindChildScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }
}

public class ScrollAnimationHelper : FrameworkElement
{
    private readonly ScrollViewer _scrollViewer;

    public ScrollAnimationHelper(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
    }

    public static readonly DependencyProperty CurrentOffsetProperty =
        DependencyProperty.Register(
            nameof(CurrentOffset),
            typeof(double),
            typeof(ScrollAnimationHelper),
            new PropertyMetadata(0.0, OnCurrentOffsetChanged));

    public double CurrentOffset
    {
        get => (double)GetValue(CurrentOffsetProperty);
        set => SetValue(CurrentOffsetProperty, value);
    }

    private static void OnCurrentOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollAnimationHelper helper)
        {
            helper._scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }
    }
}
