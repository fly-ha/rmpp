using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Rmpp.Desktop.Utilities;
using Rmpp.Domain.Styles;

namespace Rmpp.Desktop.Controls;

/// <summary>提供完全离线的 HSV 连续调色、RGBA/Hex 精确输入、透明度和实时双向更新。</summary>
public partial class RgbaColorEditor : UserControl
{
    private enum PickerDragMode
    {
        None,
        SaturationValue,
        Hue,
        Alpha,
    }

    private bool suppressInputTracking;
    private bool applyingValue;
    private bool editSessionActive;
    private PickerDragMode dragMode;
    private RgbaColor currentColor = RgbaColor.Black;
    private RgbaColor editStartColor = RgbaColor.Black;
    private HsvColor currentHsv;

    public RgbaColorEditor()
    {
        InitializeComponent();
        Loaded += OnEditorLoaded;
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(RgbaColorEditor),
        new FrameworkPropertyMetadata(
            "#FF000000",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool Commit()
    {
        if (!RgbaColorText.TryParse(ColorTextBox.Text, out RgbaColor color))
        {
            RestoreInvalidInput(ColorTextBox, "请输入 #RRGGBB 或 #AARRGGBB 颜色值。");
            return false;
        }

        ApplyColor(color);
        return true;
    }

    public void CancelEdit()
    {
        ClearAllErrors();
        if (editSessionActive)
        {
            ApplyColor(editStartColor);
            return;
        }

        SynchronizeFromValue(Value);
    }

    public void ApplyPaletteColor(string rgbText)
    {
        if (!RgbaColorText.TryParse(rgbText, out RgbaColor selected))
        {
            return;
        }

        ApplyColor(selected with { Alpha = currentColor.Alpha });
    }

    /// <summary>应用 HSV 连续值并保留当前透明度，供鼠标、键盘和自动化测试复用。</summary>
    public void ApplyHsv(double hue, double saturation, double value)
    {
        HsvColor normalized = HsvColorConverter.Normalize(new HsvColor(hue, saturation, value));
        ApplyColor(HsvColorConverter.ToRgba(normalized, currentColor.Alpha), hsvOverride: normalized);
    }

    /// <summary>应用四个完整颜色通道，避免不同输入方式绕过统一实时更新路径。</summary>
    public void ApplyChannels(byte red, byte green, byte blue, byte alpha)
    {
        ApplyColor(new RgbaColor(red, green, blue, alpha));
    }

    /// <summary>应用调色器透明度，并保持当前 RGB 通道不变。</summary>
    public void ApplyAlpha(byte alpha)
    {
        ApplyColor(currentColor with { Alpha = alpha }, hsvOverride: currentHsv);
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        RgbaColorEditor editor = (RgbaColorEditor)dependencyObject;
        if (!editor.applyingValue)
        {
            editor.SynchronizeFromValue(e.NewValue as string);
        }
    }

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        SynchronizeFromValue(Value);
        Dispatcher.BeginInvoke(UpdateIndicators, DispatcherPriority.Loaded);
    }

    private void OnPaletteOpened(object? sender, EventArgs e)
    {
        BeginEditSession();
    }

    private void OnPaletteClosed(object? sender, EventArgs e)
    {
        EndEditSession();
    }

    /// <summary>捕获调色器打开时的原始颜色，使连续试色可以通过 Escape 一次恢复。</summary>
    internal void BeginEditSession()
    {
        SynchronizeFromValue(Value);
        editStartColor = currentColor;
        editSessionActive = true;
        UpdateOriginalPreview();
        Dispatcher.BeginInvoke(UpdateIndicators, DispatcherPriority.Loaded);
    }

    /// <summary>结束调色会话并接受当前颜色，同时清理未完成输入和指针状态。</summary>
    internal void EndEditSession()
    {
        dragMode = PickerDragMode.None;
        editSessionActive = false;
        editStartColor = currentColor;
        ClearAllErrors();
        SynchronizeFromValue(Value);
    }

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CancelEdit();
        PaletteButton.IsChecked = false;
        e.Handled = true;
    }

    private void OnPaletteColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string rgb })
        {
            ApplyPaletteColor(rgb);
        }
    }

    private void OnColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressInputTracking || sender is not TextBox textBox)
        {
            return;
        }

        ClearError(textBox);
        if (RgbaColorText.TryParse(textBox.Text, out RgbaColor color))
        {
            ApplyColor(color, textBox);
        }
    }

    private void OnChannelTextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressInputTracking || sender is not TextBox textBox)
        {
            return;
        }

        ClearError(textBox);
        if (TryReadChannels(out RgbaColor color))
        {
            ApplyColor(color, textBox);
        }
    }

    private void OnInputLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (IsHexInput(textBox))
        {
            if (RgbaColorText.TryParse(textBox.Text, out RgbaColor color))
            {
                ApplyColor(color);
            }
            else
            {
                RestoreInvalidInput(textBox, "请输入 #RRGGBB 或 #AARRGGBB 颜色值。");
            }

            return;
        }

        if (TryReadChannels(out RgbaColor channels))
        {
            ApplyColor(channels);
        }
        else
        {
            RestoreInvalidInput(textBox, "颜色通道必须是 0 至 255 的完整整数。");
        }
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitInput(textBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            if (editSessionActive)
            {
                PaletteButton.IsChecked = false;
            }
            e.Handled = true;
        }
    }

    private void CommitInput(TextBox textBox)
    {
        if (IsHexInput(textBox))
        {
            if (RgbaColorText.TryParse(textBox.Text, out RgbaColor color))
            {
                ApplyColor(color);
            }
            else
            {
                RestoreInvalidInput(textBox, "请输入 #RRGGBB 或 #AARRGGBB 颜色值。");
            }

            return;
        }

        if (TryReadChannels(out RgbaColor channels))
        {
            ApplyColor(channels);
        }
        else
        {
            RestoreInvalidInput(textBox, "颜色通道必须是 0 至 255 的完整整数。");
        }
    }

    private void OnPickerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas)
        {
            return;
        }

        dragMode = GetDragMode(canvas);
        canvas.Focus();
        _ = canvas.CaptureMouse();
        UpdateFromPointer(canvas, e.GetPosition(canvas));
        e.Handled = true;
    }

    private void OnPickerMouseMove(object sender, MouseEventArgs e)
    {
        if (dragMode == PickerDragMode.None || e.LeftButton != MouseButtonState.Pressed || sender is not Canvas canvas)
        {
            return;
        }

        UpdateFromPointer(canvas, e.GetPosition(canvas));
        e.Handled = true;
    }

    private void OnPickerMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (dragMode == PickerDragMode.None || sender is not Canvas canvas)
        {
            return;
        }

        UpdateFromPointer(canvas, e.GetPosition(canvas));
        canvas.ReleaseMouseCapture();
        dragMode = PickerDragMode.None;
        e.Handled = true;
    }

    private void OnPickerLostMouseCapture(object sender, MouseEventArgs e)
    {
        dragMode = PickerDragMode.None;
    }

    private void OnPickerKeyDown(object sender, KeyEventArgs e)
    {
        double coarse = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10d : 1d;
        bool handled = true;

        if (ReferenceEquals(sender, HueInput))
        {
            double hue = e.Key switch
            {
                Key.Left or Key.Down => currentHsv.Hue - coarse,
                Key.Right or Key.Up => currentHsv.Hue + coarse,
                Key.Home => 0d,
                Key.End => 359d,
                _ => currentHsv.Hue,
            };
            handled = e.Key is Key.Left or Key.Down or Key.Right or Key.Up or Key.Home or Key.End;
            if (handled)
            {
                ApplyHsv(hue, currentHsv.Saturation, currentHsv.Value);
            }
        }
        else if (ReferenceEquals(sender, AlphaInput))
        {
            int alpha = e.Key switch
            {
                Key.Left or Key.Down => currentColor.Alpha - (int)coarse,
                Key.Right or Key.Up => currentColor.Alpha + (int)coarse,
                Key.Home => 0,
                Key.End => 255,
                _ => currentColor.Alpha,
            };
            handled = e.Key is Key.Left or Key.Down or Key.Right or Key.Up or Key.Home or Key.End;
            if (handled)
            {
                ApplyAlpha(checked((byte)Math.Clamp(alpha, 0, 255)));
            }
        }
        else if (ReferenceEquals(sender, SaturationValueInput))
        {
            double step = coarse / 100d;
            double saturation = currentHsv.Saturation;
            double value = currentHsv.Value;
            switch (e.Key)
            {
                case Key.Left: saturation -= step; break;
                case Key.Right: saturation += step; break;
                case Key.Down: value -= step; break;
                case Key.Up: value += step; break;
                default: handled = false; break;
            }

            if (handled)
            {
                ApplyHsv(currentHsv.Hue, saturation, value);
            }
        }
        else
        {
            handled = false;
        }

        e.Handled = handled;
    }

    private void OnPickerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateIndicators();
    }

    private void UpdateFromPointer(Canvas canvas, Point position)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        double x = Math.Clamp(position.X, 0d, width);
        double y = Math.Clamp(position.Y, 0d, height);
        switch (GetDragMode(canvas))
        {
            case PickerDragMode.SaturationValue:
                ApplyHsv(currentHsv.Hue, x / width, 1d - (y / height));
                break;
            case PickerDragMode.Hue:
                ApplyHsv((x / width) * 360d, currentHsv.Saturation, currentHsv.Value);
                break;
            case PickerDragMode.Alpha:
                ApplyAlpha(checked((byte)Math.Round((x / width) * 255d, MidpointRounding.AwayFromZero)));
                break;
        }
    }

    private PickerDragMode GetDragMode(Canvas canvas)
    {
        if (ReferenceEquals(canvas, SaturationValueInput))
        {
            return PickerDragMode.SaturationValue;
        }

        if (ReferenceEquals(canvas, HueInput))
        {
            return PickerDragMode.Hue;
        }

        return ReferenceEquals(canvas, AlphaInput) ? PickerDragMode.Alpha : PickerDragMode.None;
    }

    private bool TryReadChannels(out RgbaColor color) =>
        RgbaColorText.TryParseChannels(
            RedTextBox.Text,
            GreenTextBox.Text,
            BlueTextBox.Text,
            AlphaTextBox.Text,
            out color);

    private static bool IsHexInput(TextBox textBox) =>
        textBox.Name is nameof(ColorTextBox) or nameof(HexTextBox);

    private void ApplyColor(RgbaColor color, TextBox? preservedInput = null, HsvColor? hsvOverride = null)
    {
        string normalized = RgbaColorText.Format(color);
        currentColor = color;
        currentHsv = hsvOverride ?? HsvColorConverter.FromRgba(color);
        ClearAllErrors();

        if (!editSessionActive)
        {
            editStartColor = color;
        }

        if (!string.Equals(Value, normalized, StringComparison.OrdinalIgnoreCase))
        {
            applyingValue = true;
            try
            {
                SetCurrentValue(ValueProperty, normalized);
            }
            finally
            {
                applyingValue = false;
            }
        }

        SynchronizeControls(color, preservedInput);
    }

    private void SynchronizeFromValue(string? value)
    {
        RgbaColor color = RgbaColorText.TryParse(value, out RgbaColor parsed) ? parsed : RgbaColor.Black;
        currentColor = color;
        currentHsv = HsvColorConverter.FromRgba(color);
        if (!editSessionActive)
        {
            editStartColor = color;
        }
        SynchronizeControls(color);
    }

    private void SynchronizeControls(RgbaColor color, TextBox? preservedInput = null)
    {
        string normalized = RgbaColorText.Format(color);
        suppressInputTracking = true;
        try
        {
            SetText(ColorTextBox, normalized, preservedInput);
            SetText(HexTextBox, normalized, preservedInput);
            SetText(RedTextBox, color.Red.ToString(CultureInfo.InvariantCulture), preservedInput);
            SetText(GreenTextBox, color.Green.ToString(CultureInfo.InvariantCulture), preservedInput);
            SetText(BlueTextBox, color.Blue.ToString(CultureInfo.InvariantCulture), preservedInput);
            SetText(AlphaTextBox, color.Alpha.ToString(CultureInfo.InvariantCulture), preservedInput);
        }
        finally
        {
            suppressInputTracking = false;
        }

        HueText.Text = $"{currentHsv.Hue:0}°";
        AlphaText.Text = $"{color.Alpha / 255d:P0} · A {color.Alpha}";
        Color wpfColor = ToWpfColor(color);
        ColorPreviewFill.Background = new SolidColorBrush(wpfColor);
        CurrentColorFill.Background = new SolidColorBrush(wpfColor);
        CurrentColorText.Text = normalized;

        RgbaColor hueColor = HsvColorConverter.ToRgba(new HsvColor(currentHsv.Hue, 1d, 1d));
        SaturationValueHueLayer.Background = new SolidColorBrush(ToWpfColor(hueColor));
        AlphaColorLayer.Background = CreateAlphaBrush(color);

        if (!editSessionActive)
        {
            UpdateOriginalPreview();
        }

        UpdateIndicators();
    }

    private static void SetText(TextBox textBox, string value, TextBox? preservedInput)
    {
        if (!ReferenceEquals(textBox, preservedInput))
        {
            textBox.Text = value;
        }
    }

    private void UpdateOriginalPreview()
    {
        OriginalColorFill.Background = new SolidColorBrush(ToWpfColor(editStartColor));
        OriginalColorText.Text = RgbaColorText.Format(editStartColor);
    }

    private void UpdateIndicators()
    {
        PositionIndicator(
            SaturationValueIndicator,
            SaturationValueInput,
            currentHsv.Saturation * SaturationValueInput.ActualWidth,
            (1d - currentHsv.Value) * SaturationValueInput.ActualHeight);
        PositionIndicator(
            HueIndicator,
            HueInput,
            (currentHsv.Hue / 360d) * HueInput.ActualWidth,
            HueInput.ActualHeight / 2d);
        PositionIndicator(
            AlphaIndicator,
            AlphaInput,
            (currentColor.Alpha / 255d) * AlphaInput.ActualWidth,
            AlphaInput.ActualHeight / 2d);
    }

    private static void PositionIndicator(FrameworkElement indicator, Canvas canvas, double x, double y)
    {
        if (canvas.ActualWidth <= 0d || canvas.ActualHeight <= 0d)
        {
            return;
        }

        Canvas.SetLeft(indicator, Math.Clamp(x - (indicator.Width / 2d), -indicator.Width / 2d, canvas.ActualWidth - (indicator.Width / 2d)));
        Canvas.SetTop(indicator, Math.Clamp(y - (indicator.Height / 2d), -indicator.Height / 2d, canvas.ActualHeight - (indicator.Height / 2d)));
    }

    private static LinearGradientBrush CreateAlphaBrush(RgbaColor color)
    {
        LinearGradientBrush brush = new()
        {
            StartPoint = new Point(0d, 0.5d),
            EndPoint = new Point(1d, 0.5d),
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.Red, color.Green, color.Blue), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, color.Red, color.Green, color.Blue), 1d));
        return brush;
    }

    private static Color ToWpfColor(RgbaColor color) =>
        Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

    private void RestoreInvalidInput(TextBox textBox, string message)
    {
        SynchronizeControls(currentColor);
        ShowError(textBox, message);
    }

    private static void ShowError(TextBox textBox, string message)
    {
        textBox.BorderBrush = Brushes.IndianRed;
        textBox.ToolTip = message;
        AutomationProperties.SetHelpText(textBox, message);
    }

    private static void ClearError(TextBox textBox)
    {
        textBox.ClearValue(BorderBrushProperty);
        textBox.ClearValue(ToolTipProperty);
        AutomationProperties.SetHelpText(textBox, string.Empty);
    }

    private void ClearAllErrors()
    {
        ClearError(ColorTextBox);
        ClearError(HexTextBox);
        ClearError(RedTextBox);
        ClearError(GreenTextBox);
        ClearError(BlueTextBox);
        ClearError(AlphaTextBox);
    }
}
