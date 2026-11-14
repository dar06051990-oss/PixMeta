using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using ImageMagick;
using PixMeta.Services;

namespace PixMeta;

public partial class MainWindow : Window
{
    string? current;
    List<string> files = new();
    int index;
    MagickImageCollection? animation;
    Bitmap? animationFrameBitmap;
    int animationIndex;
    DispatcherTimer? animationTimer;
    bool animationPlaying;
    double zoom = 0; // 0 = fit
    uint imageWidth, imageHeight;
    Point panStart;
    Vector panOffsetStart;
    bool panning;
    const double PreviewMax = 2200;
    const double FastPreviewMax = 1100;
    double transformScale = 1;
    Vector panOffset;
    readonly ScaleTransform scaleTransform = new(1, 1);
    readonly TranslateTransform translateTransform = new(0, 0);
    readonly TransformGroup viewerTransform = new();
    readonly Dictionary<string, Bitmap> previewCache = new(StringComparer.OrdinalIgnoreCase);
    readonly LinkedList<string> previewLru = new();
    CancellationTokenSource? loadCts;
    int loadGeneration;
    bool fullResolutionLoaded;
    Bitmap? fullResolutionBitmap;
    readonly Dictionary<string, Bitmap> thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    readonly LinkedList<string> thumbnailLru = new();
    const int ThumbnailCacheLimit = 32;
    int filmstripGeneration;
    const int PreviewCacheLimit = 14;
    int navigationDirection = 1;
    bool picasaMode;
    string generationExportData = "";
    string generationExportKind = "";

    public MainWindow(string[]? args)
    {
        InitializeComponent();
        viewerTransform.Children.Add(scaleTransform);
        viewerTransform.Children.Add(translateTransform);
        Viewer.RenderTransform = viewerTransform;
        // Tunnel handler makes the image stage own the wheel completely.
        ImageScroller.AddHandler(InputElement.PointerWheelChangedEvent, ImageScroller_Wheel, RoutingStrategies.Tunnel, true);

        if (args?.Length > 0 && File.Exists(args[0]))
        {
            // Explorer/file-association launches use the Picasa-style desktop overlay.
            SetPicasaMode(true);
            Load(args[0]);
        }
    }

    async void Open_Click(object? s, RoutedEventArgs e)
    {
        var f = await StorageProvider.OpenFilePickerAsync(new() { AllowMultiple = false, Title = "Open image" });
        if (f.Count > 0) Load(f[0].Path.LocalPath);
    }

    async void Load(string path)
    {
        var generation = ++loadGeneration;
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = new CancellationTokenSource();
        var token = loadCts.Token;
        try
        {
            StopAnimation();
            // A 1:1 bitmap can be tens or hundreds of MB. It belongs only to the
            // current image; never carry it into navigation/preload caches.
            if (fullResolutionBitmap != null)
            {
                if (ReferenceEquals(Viewer.Source, fullResolutionBitmap)) Viewer.Source = null;
                fullResolutionBitmap.Dispose(); fullResolutionBitmap = null;
            }
            // Clear even for animated formats, whose decoding starts asynchronously.
            Viewer.Source = null;
            current = path;
            BuildList(path);
            FileName.Text = Path.GetFileName(path);
            // While the next image decodes, do not expose/copy metadata from the previous file.
            Source.Text = "Reading metadata…";
            Prompt.Text = Negative.Text = Params.Text = Raw.Text = "";
            generationExportData = generationExportKind = "";
            CopyGenerationButton.IsVisible = PromptMissing.IsVisible = false;
            imageWidth = imageHeight = 0;
            zoom = 0; transformScale = 1; panOffset = default; fullResolutionLoaded = false;
            ApplyTransform();
            Empty.IsVisible = false;
            Status.Text = $"{index + 1} / {files.Count}";

            // Animated formats keep the frame collection path. Static images use the async preview cache.
            bool maybeAnimated = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gif", ".webp", ".apng" }.Contains(Path.GetExtension(path));
            if (maybeAnimated)
            {
                var collection = await Task.Run(() => new MagickImageCollection(path), token);
                if (token.IsCancellationRequested || generation != loadGeneration) { collection.Dispose(); return; }
                if (collection.Count > 1)
                {
                    animation = collection; animation.Coalesce(); animationIndex = 0;
                    AnimationPanel.IsVisible = AnimationDivider.IsVisible = true;
                    animationPlaying = true; PlayPause.Content = "⏸";
                    imageWidth = animation[0].Width; imageHeight = animation[0].Height;
                    ShowAnimationFrame(); ScheduleNextFrame();
                }
                else { collection.Dispose(); await LoadStaticPreviewAsync(path, generation, token); }
            }
            else
            {
                // Picasa-style deferred decode: navigation itself never starts a heavy
                // decode immediately. A short quiet period collapses rapid wheel/key
                // navigation into one decode for the image where the user stops.
                ShowInstantPlaceholder(path);
                await Task.Delay(85, token);
                await LoadStaticPreviewAsync(path, generation, token);
            }

            if (token.IsCancellationRequested || generation != loadGeneration) return;
            var md = await Task.Run(() => MetadataReader.Read(path), token);
            if (token.IsCancellationRequested || generation != loadGeneration) return;
            Status.Text = $"{imageWidth} × {imageHeight} • {md.ActualFormat}" + (md.ExtensionMismatch ? " • ⚠ extension mismatch" : "") + $"   •   {index + 1} / {files.Count}";
            Source.Text = md.Source; Prompt.Text = md.Prompt; Negative.Text = md.NegativePrompt;
            Params.Text = string.Join("\n", md.Fields.Select(x => $"{x.Key}: {x.Value}")); Raw.Text = md.Raw;
            generationExportData = md.ExportData; generationExportKind = md.ExportKind;
            CopyGenerationButton.IsVisible = !string.IsNullOrWhiteSpace(generationExportData);
            ToolTip.SetTip(CopyGenerationButton, string.IsNullOrWhiteSpace(generationExportKind) ? "Copy generation data" : $"Copy {generationExportKind}");
            PromptMissing.IsVisible = md.Source == "Grok / xAI" && string.IsNullOrWhiteSpace(md.Prompt);
            UpdateFilmstrip();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (generation == loadGeneration) Status.Text = ex.Message; }
    }

    void ShowInstantPlaceholder(string path)
    {
        // Prefer an already decoded preview, otherwise reuse the filmstrip thumbnail.
        // This makes selection immediate without competing with the decoder.
        if (previewCache.TryGetValue(path, out var p))
        {
            TouchPreview(path); Viewer.Source = p;
            return;
        }
        if (thumbnailCache.TryGetValue(path, out var t))
        {
            TouchThumbnail(path); Viewer.Source = t;
        }
        else
        {
            // An uncached file must not briefly display the previous file's image.
            Viewer.Source = null;
        }
    }

    async Task LoadStaticPreviewAsync(string path, int generation, CancellationToken token)
    {
        AnimationPanel.IsVisible = AnimationDivider.IsVisible = false;
        if (previewCache.TryGetValue(path, out var cached))
        {
            TouchPreview(path); Viewer.Source = cached;
            var size = await Task.Run(() => ReadImageSize(path), token);
            imageWidth = size.w; imageHeight = size.h; ApplyZoom(); return;
        }
        var result = await Task.Run(() => DecodePreview(path, FastPreviewMax), token);
        if (token.IsCancellationRequested || generation != loadGeneration) return;
        imageWidth = result.w; imageHeight = result.h;
        var bitmap = new Bitmap(new MemoryStream(result.png));
        AddPreview(path, bitmap); Viewer.Source = bitmap; ApplyZoom();
        // Stage 2: improve only the currently visible image after the fast preview is on screen.
        _ = UpgradeCurrentPreviewAsync(path, generation, token);
    }

    static (uint w, uint h) ReadImageSize(string path)
    {
        var info = new MagickImageInfo(path); return (info.Width, info.Height);
    }

    static (byte[] png, uint w, uint h) DecodePreview(string path, double max)
    {
        using var m = new MagickImage(path); var w = m.Width; var h = m.Height;
        if (m.Width > max || m.Height > max) m.Resize(new MagickGeometry((uint)max, (uint)max) { IgnoreAspectRatio = false });
        m.Format = MagickFormat.Png; return (m.ToByteArray(), w, h);
    }

    void AddPreview(string path, Bitmap bitmap)
    {
        if (previewCache.TryGetValue(path, out var old) && !ReferenceEquals(old, bitmap)) old.Dispose();
        previewCache[path] = bitmap; TouchPreview(path);
        while (previewLru.Count > PreviewCacheLimit)
        {
            var victim = previewLru.First!.Value; previewLru.RemoveFirst();
            if (string.Equals(victim, current, StringComparison.OrdinalIgnoreCase)) { previewLru.AddLast(victim); continue; }
            if (previewCache.Remove(victim, out var b)) b.Dispose();
        }
    }

    void TouchPreview(string path) { previewLru.Remove(path); previewLru.AddLast(path); }

    async Task UpgradeCurrentPreviewAsync(string path, int generation, CancellationToken token)
    {
        try
        {
            // The 1100px preview is enough for instant navigation. Higher quality is
            // deliberately deferred so a cache miss never blocks the image switch.
            await Task.Delay(350, token);
            if (token.IsCancellationRequested || generation != loadGeneration ||
                !string.Equals(current, path, StringComparison.OrdinalIgnoreCase)) return;
            var r = await Task.Run(() => DecodePreview(path, PreviewMax), token);
            if (token.IsCancellationRequested || generation != loadGeneration ||
                !string.Equals(current, path, StringComparison.OrdinalIgnoreCase)) return;
            var bmp = await Task.Run(() => new Bitmap(new MemoryStream(r.png)), token);
            if (token.IsCancellationRequested || generation != loadGeneration ||
                !string.Equals(current, path, StringComparison.OrdinalIgnoreCase)) { bmp.Dispose(); return; }
            AddPreview(path, bmp);
            Viewer.Source = bmp;
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    static Bitmap ToBitmap(IMagickImage<byte> image)
    {
        using var ms = new MemoryStream();
        image.Format = MagickFormat.Png; image.Write(ms); ms.Position = 0;
        return new Bitmap(ms);
    }

    static Bitmap ToPreviewBitmap(IMagickImage<byte> source)
    {
        using var preview = source.Clone();
        if (preview.Width > PreviewMax || preview.Height > PreviewMax)
            preview.Resize(new MagickGeometry((uint)PreviewMax, (uint)PreviewMax) { IgnoreAspectRatio = false });
        return ToBitmap(preview);
    }

    int filmstripStart = -1;
    readonly List<Button> filmstripButtons = new();

    void UpdateFilmstrip()
    {
        if (files.Count == 0) return;
        int start = Math.Max(0, Math.Min(index - 5, Math.Max(0, files.Count - 11)));
        int end = Math.Min(files.Count - 1, start + 10);

        if (filmstripStart == start && filmstripButtons.Count == end - start + 1)
        {
            for (int n = 0; n < filmstripButtons.Count; n++)
                filmstripButtons[n].Opacity = (start + n == index) ? 1 : .62;
            return;
        }

        // Never decode thumbnails on the UI thread. Rebuilding the visible strip is
        // cheap: cached thumbs appear immediately, missing ones are filled in later.
        int stripGen = ++filmstripGeneration;
        filmstripStart = start;
        filmstripButtons.Clear();
        ThumbnailStrip.Children.Clear();
        for (int i = start; i <= end; i++)
        {
            int target = i;
            string thumbPath = files[i];
            var button = new Button { Width = 82, Height = 76, Padding = new Avalonia.Thickness(3), Opacity = i == index ? 1 : .62 };
            if (thumbnailCache.TryGetValue(thumbPath, out var cachedThumb))
            {
                TouchThumbnail(thumbPath);
                button.Content = new Image { Source = cachedThumb, Stretch = Avalonia.Media.Stretch.Uniform };
            }
            else
            {
                button.Content = new TextBlock { Text = "…", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Opacity = .45 };
                _ = LoadThumbnailAsync(thumbPath, button, stripGen);
            }
            button.Click += (_, _) => { index = target; Load(files[index]); };
            filmstripButtons.Add(button);
            ThumbnailStrip.Children.Add(button);
        }
    }

    async Task LoadThumbnailAsync(string path, Button button, int stripGen)
    {
        try
        {
            var bytes = await Task.Run(() =>
            {
                using var m = new MagickImage(path);
                if (m.Width > 180 || m.Height > 180)
                    m.Resize(new MagickGeometry(160, 160) { IgnoreAspectRatio = false });
                m.Format = MagickFormat.Png;
                return m.ToByteArray();
            });
            var bmp = new Bitmap(new MemoryStream(bytes));
            if (thumbnailCache.TryGetValue(path, out var existing)) { bmp.Dispose(); bmp = existing; }
            else AddThumbnail(path, bmp);

            // The strip may have moved while decoding. Only paint this button if it
            // still represents the same visible generation.
            if (stripGen == filmstripGeneration && button.Parent != null)
                button.Content = new Image { Source = bmp, Stretch = Avalonia.Media.Stretch.Uniform };
        }
        catch { }
    }

    void AddThumbnail(string path, Bitmap bitmap)
    {
        thumbnailCache[path] = bitmap; TouchThumbnail(path);
        while (thumbnailLru.Count > ThumbnailCacheLimit)
        {
            var victim = thumbnailLru.First!.Value; thumbnailLru.RemoveFirst();
            if (thumbnailCache.Remove(victim, out var b)) b.Dispose();
        }
    }

    void TouchThumbnail(string path) { thumbnailLru.Remove(path); thumbnailLru.AddLast(path); }

    void Info_Click(object? s, RoutedEventArgs e)
    {
        InfoPanel.IsVisible = !InfoPanel.IsVisible;
        if (zoom <= 0) Dispatcher.UIThread.Post(ApplyZoom, DispatcherPriority.Background);
    }

    void SetPicasaMode(bool enabled)
    {
        picasaMode = enabled;
        OverlayCloseButton.IsVisible = enabled;
        OverlayMinimizeButton.IsVisible = enabled;
        if (enabled)
        {
            SystemDecorations = SystemDecorations.None;
            WindowState = WindowState.FullScreen;
            Background = new SolidColorBrush(Color.Parse("#B8000000"));
            Stage.Background = Brushes.Transparent;
            TopBar.Background = new SolidColorBrush(Color.Parse("#44171A20"));
            FilmstripArea.Background = new SolidColorBrush(Color.Parse("#A6101217"));
            FullscreenButton.Content = "❐";
            ToolTip.SetTip(FullscreenButton, "Windowed mode");
        }
        else
        {
            WindowState = WindowState.Normal;
            SystemDecorations = SystemDecorations.Full;
            Background = new SolidColorBrush(Color.Parse("#E80B0D11"));
            Stage.Background = new SolidColorBrush(Color.Parse("#22000000"));
            TopBar.Background = new SolidColorBrush(Color.Parse("#55171A20"));
            FilmstripArea.Background = new SolidColorBrush(Color.Parse("#B5101217"));
            FullscreenButton.Content = "⛶";
            ToolTip.SetTip(FullscreenButton, "Picasa mode");
        }
        if (zoom <= 0) Dispatcher.UIThread.Post(ApplyZoom, DispatcherPriority.Background);
    }

    void Fullscreen_Click(object? s, RoutedEventArgs e)
    {
        SetPicasaMode(!picasaMode);
    }

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_MINIMIZE = 6;

    void Minimize_Click(object? s, RoutedEventArgs e)
    {
        // In Picasa mode let Windows minimize the native HWND directly.
        // Do not assign Avalonia WindowState=Minimized here: that overwrites the
        // fullscreen restore state and causes the small-window flash/restore race.
        if (picasaMode && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var handle = this.TryGetPlatformHandle();
            if (handle is not null && handle.Handle != IntPtr.Zero)
            {
                ShowWindow(handle.Handle, SW_MINIMIZE);
                return;
            }
        }

        WindowState = WindowState.Minimized;
    }

    void Close_Click(object? s, RoutedEventArgs e) => Close();

    void Window_KeyDown(object? s, KeyEventArgs e)
    {
        if (e.Key == Key.Left) Prev_Click(null, new RoutedEventArgs());
        else if (e.Key == Key.Right) Next_Click(null, new RoutedEventArgs());
        else if (e.Key == Key.Escape)
        {
            if (InfoPanel.IsVisible) InfoPanel.IsVisible = false;
            else if (picasaMode) Close();
            else if (WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;
            else Close();
        }
        else if (e.Key == Key.I) InfoPanel.IsVisible = !InfoPanel.IsVisible;
        else if (e.Key == Key.F11) Fullscreen_Click(null, new RoutedEventArgs());
        else if (e.Key == Key.Space && animation != null && animation.Count > 1) PlayPause_Click(null, new RoutedEventArgs());
    }

    void Fit_Click(object? s, RoutedEventArgs e) { zoom = 0; transformScale = 1; panOffset = default; ApplyZoom(); }
    async void ActualSize_Click(object? s, RoutedEventArgs e) { zoom = 1; await EnsureFullResolutionAsync(); ApplyZoom(); }
    void ZoomIn_Click(object? s, RoutedEventArgs e) { ZoomBy(1.2); }
    void ZoomOut_Click(object? s, RoutedEventArgs e) { ZoomBy(1 / 1.2); }

    void ZoomBy(double factor, Point? anchor = null)
    {
        if (imageWidth == 0 || imageHeight == 0) return;
        var fit = GetFitScale();
        var absolute = zoom <= 0 ? fit : zoom;
        var oldScale = transformScale;
        absolute = Math.Clamp(absolute * factor, Math.Max(.02, fit * .15), 8);
        zoom = absolute;
        var newScale = Math.Max(.01, absolute / fit);

        // Keep the pixel under the mouse pointer under the pointer while zooming.
        // RenderTransformOrigin is the centre of Viewer, so compensate translation
        // around that same origin instead of always zooming toward the centre.
        if (anchor is Point a && oldScale > 0)
        {
            var c = new Point(ImageScroller.Bounds.Width / 2.0, ImageScroller.Bounds.Height / 2.0);
            var ratio = newScale / oldScale;
            panOffset = new Vector(
                a.X - c.X - ratio * (a.X - c.X - panOffset.X),
                a.Y - c.Y - ratio * (a.Y - c.Y - panOffset.Y));
        }

        transformScale = newScale;
        if (absolute > fit * 1.15 && !fullResolutionLoaded) _ = EnsureFullResolutionAsync();
        ApplyTransform(); ZoomText.Text = $"{Math.Round(absolute * 100)}%";
    }

    double GetFitScale()
    {
        // Fit uses the normal viewing zone between the compact top bar and bottom filmstrip.
        // Once the user zooms/pans, the image viewport itself is full-screen and may continue
        // underneath the translucent UI, Picasa-style.
        var w = Math.Max(100, ImageScroller.Bounds.Width - 24 - (InfoPanel.IsVisible ? 430 : 0));
        var h = Math.Max(100, ImageScroller.Bounds.Height - 40 - 76 - 24);
        if (imageWidth == 0 || imageHeight == 0) return 1;
        return Math.Min(1, Math.Min(w / imageWidth, h / imageHeight));
    }

    void ApplyZoom()
    {
        if (imageWidth == 0 || imageHeight == 0) return;
        // In Fit, keep the picture in the normal unobstructed zone. During manual zoom/pan
        // remove those reservations so pixels can extend beneath the translucent overlays.
        Viewer.Margin = zoom <= 0
            ? new Thickness(0, 40, InfoPanel.IsVisible ? 430 : 0, 76)
            : new Thickness(0);
        if (zoom <= 0)
        {
            transformScale = 1;
            panOffset = default;
            ZoomText.Text = $"{Math.Round(GetFitScale() * 100)}%";
        }
        else
        {
            transformScale = Math.Max(.01, zoom / GetFitScale());
            ZoomText.Text = $"{Math.Round(zoom * 100)}%";
        }
        ApplyTransform();
    }

    void ApplyTransform()
    {
        scaleTransform.ScaleX = transformScale; scaleTransform.ScaleY = transformScale;
        translateTransform.X = panOffset.X; translateTransform.Y = panOffset.Y;
    }

    async Task EnsureFullResolutionAsync()
    {
        if (current == null || fullResolutionLoaded || animation is { Count: > 1 }) return;
        var path = current; var generation = loadGeneration;
        try
        {
            // Decode the original directly. The old path decoded with Magick.NET and then
            // re-encoded the whole image as PNG before Avalonia decoded it again, which made
            // high-resolution JPEGs noticeably slow when zooming.
            var bmp = await Task.Run(() => new Bitmap(path));
            if (generation != loadGeneration || current != path) { bmp.Dispose(); return; }
            fullResolutionBitmap?.Dispose();
            fullResolutionBitmap = bmp;
            Viewer.Source = bmp; fullResolutionLoaded = true;
        } catch { }
    }

    void ImageScroller_Wheel(object? s, PointerWheelEventArgs e)
    {
        // Read the pointer in the stable, untransformed stage coordinate system on every wheel tick.
        // This makes the zoom anchor immediately follow the cursor after it moves.
        ZoomBy(e.Delta.Y > 0 ? 1.12 : 1 / 1.12, e.GetPosition(ImageScroller));
        e.Handled = true;
    }

    void Bottom_Wheel(object? s, PointerWheelEventArgs e)
    {
        if (e.Delta.Y > 0) Prev_Click(null, new RoutedEventArgs());
        else if (e.Delta.Y < 0) Next_Click(null, new RoutedEventArgs());
        e.Handled = true;
    }

    void ImageScroller_Pressed(object? s, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(ImageScroller).Properties;
        if (properties.IsMiddleButtonPressed)
        {
            Fit_Click(null, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (!properties.IsLeftButtonPressed) return;
        panning = true;
        panStart = e.GetPosition(ImageScroller);
        panOffsetStart = panOffset;
        e.Pointer.Capture(ImageScroller);
        e.Handled = true;
    }

    void ImageScroller_Moved(object? s, PointerEventArgs e)
    {
        if (!panning) return;
        var p = e.GetPosition(ImageScroller);
        var d = p - panStart;
        panOffset = new Vector(panOffsetStart.X + d.X, panOffsetStart.Y + d.Y); ApplyTransform();
        e.Handled = true;
    }

    void ImageScroller_Released(object? s, PointerReleasedEventArgs e)
    {
        if (!panning) return;
        panning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    void ShowAnimationFrame()
    {
        if (animation == null || animation.Count == 0) return;
        // One animation frame owns one Avalonia bitmap. Release the prior frame:
        // leaving each decoded frame alive used unbounded native memory on long GIFs.
        var nextFrame = ToBitmap(animation[animationIndex]);
        var previousFrame = animationFrameBitmap;
        animationFrameBitmap = nextFrame;
        Viewer.Source = nextFrame;
        previousFrame?.Dispose();
        FrameInfo.Text = $"{animationIndex + 1} / {animation.Count}";
    }

    void ScheduleNextFrame()
    {
        animationTimer?.Stop();
        if (!animationPlaying || animation == null || animation.Count < 2) return;
        var delay = Math.Max(20, animation[animationIndex].AnimationDelay * 10);
        animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
        animationTimer.Tick += (_, _) => { animationTimer?.Stop(); if (animation == null || !animationPlaying) return; animationIndex = (animationIndex + 1) % animation.Count; ShowAnimationFrame(); ScheduleNextFrame(); };
        animationTimer.Start();
    }

    void StopAnimation()
    {
        animationTimer?.Stop();
        animationTimer = null;
        animation?.Dispose();
        animation = null;
        animationPlaying = false;
        if (animationFrameBitmap != null)
        {
            if (ReferenceEquals(Viewer.Source, animationFrameBitmap)) Viewer.Source = null;
            animationFrameBitmap.Dispose();
            animationFrameBitmap = null;
        }
    }
    void PlayPause_Click(object? s, RoutedEventArgs e) { if (animation == null || animation.Count < 2) return; animationPlaying = !animationPlaying; PlayPause.Content = animationPlaying ? "⏸" : "▶"; if (animationPlaying) ScheduleNextFrame(); else animationTimer?.Stop(); }
    void FramePrev_Click(object? s, RoutedEventArgs e) { if (animation == null || animation.Count < 2) return; animationPlaying = false; PlayPause.Content = "▶"; animationTimer?.Stop(); animationIndex = (animationIndex - 1 + animation.Count) % animation.Count; ShowAnimationFrame(); }
    void FrameNext_Click(object? s, RoutedEventArgs e) { if (animation == null || animation.Count < 2) return; animationPlaying = false; PlayPause.Content = "▶"; animationTimer?.Stop(); animationIndex = (animationIndex + 1) % animation.Count; ShowAnimationFrame(); }

    async void CopyGeneration_Click(object? s, RoutedEventArgs e) => await CopyTextWithFeedback(CopyGenerationButton, generationExportData, generationExportKind);
    async void CopyPrompt_Click(object? s, RoutedEventArgs e) => await CopyTextWithFeedback(CopyPromptButton, Prompt.Text ?? "");
    async void CopyNegative_Click(object? s, RoutedEventArgs e) => await CopyTextWithFeedback(CopyNegativeButton, Negative.Text ?? "");
    async void CopyParams_Click(object? s, RoutedEventArgs e) => await CopyTextWithFeedback(CopyParamsButton, Params.Text ?? "");

    async Task CopyTextWithFeedback(Button button, string text, string? copiedWhat = null)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null || string.IsNullOrEmpty(text)) return;

        await clipboard.SetTextAsync(text);
        // Windows may otherwise clear the clipboard when PixMeta exits.
        await clipboard.FlushAsync();
        button.Content = "✓";
        ToolTip.SetTip(button, string.IsNullOrWhiteSpace(copiedWhat) ? "Copied" : $"Copied {copiedWhat}");
        await Task.Delay(900);
        button.Content = "⧉";
        ToolTip.SetTip(button, string.IsNullOrWhiteSpace(copiedWhat) ? "Copy" : $"Copy {copiedWhat}");
    }

    void BuildList(string p)
    {
        var dir = Path.GetDirectoryName(p)!;
        var currentDir = files.Count > 0 ? Path.GetDirectoryName(files[0]) : null;
        if (!string.Equals(dir, currentDir, StringComparison.OrdinalIgnoreCase) ||
            !files.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)))
        {
            var ext = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".tif", ".tiff", ".avif", ".heic", ".apng" };
            files = Directory.EnumerateFiles(dir).Where(x => ext.Contains(Path.GetExtension(x))).OrderBy(x => x).ToList();
            filmstripStart = -1;
        }
        index = files.FindIndex(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase));
    }
    void Prev_Click(object? s, RoutedEventArgs e) { if (files.Count > 0 && index > 0) { navigationDirection = -1; index--; Load(files[index]); } }
    void Next_Click(object? s, RoutedEventArgs e) { if (files.Count > 0 && index < files.Count - 1) { navigationDirection = 1; index++; Load(files[index]); } }
    protected override void OnClosed(EventArgs e)
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = null;
        StopAnimation();
        fullResolutionBitmap?.Dispose(); fullResolutionBitmap = null;
        foreach (var b in previewCache.Values) b.Dispose(); previewCache.Clear();
        foreach (var b in thumbnailCache.Values) b.Dispose(); thumbnailCache.Clear();
        base.OnClosed(e);
    }
}
