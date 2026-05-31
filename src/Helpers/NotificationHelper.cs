using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace QuickLink.Helpers;

internal static class NotificationHelper
{
    internal static async Task ShowAsync(
        InfoBar bar,
        string message,
        InfoBarSeverity severity,
        CancellationToken token)
    {
        var visual = ElementCompositionPreview.GetElementVisual(bar);

        // Stop any in-progress animation and snap to neutral so we start from a known state
        visual.StopAnimation("Opacity");
        visual.StopAnimation("Offset");
        visual.Opacity = 1f;
        visual.Offset = Vector3.Zero;

        bar.Message = message;
        bar.Severity = severity;

        var needsAnimateIn = !bar.IsOpen;
        bar.IsOpen = true;

        // Only animate in when opening from closed; if already visible just update the content
        if (needsAnimateIn)
            AnimateIn(bar);

        try
        {
            await Task.Delay(3000, token);
            // Guard against the race where the delay finishes just as a new notification cancels us
            token.ThrowIfCancellationRequested();
            await AnimateOutAsync(bar, token);
            visual.Opacity = 1f;
            visual.Offset = Vector3.Zero;
            bar.IsOpen = false;
        }
        catch (OperationCanceledException) { }
    }

    private static void AnimateIn(InfoBar bar)
    {
        var visual = ElementCompositionPreview.GetElementVisual(bar);
        var compositor = visual.Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));

        visual.Opacity = 0f;
        visual.Offset = new Vector3(0f, 12f, 0f);

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(1f, 1f, ease);
        opacity.Duration = TimeSpan.FromMilliseconds(250);

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, new Vector3(0f, 12f, 0f));
        offset.InsertKeyFrame(1f, Vector3.Zero, ease);
        offset.Duration = TimeSpan.FromMilliseconds(300);

        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);
    }

    private static async Task AnimateOutAsync(InfoBar bar, CancellationToken token)
    {
        var visual = ElementCompositionPreview.GetElementVisual(bar);
        var compositor = visual.Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.7f, 0f), new Vector2(1f, 0.5f));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 1f);
        opacity.InsertKeyFrame(1f, 0f, ease);
        opacity.Duration = TimeSpan.FromMilliseconds(200);

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, Vector3.Zero);
        offset.InsertKeyFrame(1f, new Vector3(0f, 8f, 0f), ease);
        offset.Duration = TimeSpan.FromMilliseconds(200);

        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);

        await Task.Delay(200, token);
    }
}
