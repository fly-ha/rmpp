using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rmpp.Desktop.Utilities;
using Rmpp.Domain.Styles;

namespace Rmpp.Desktop.Controls;

/// <summary>提供离线预设调色板、Alpha 调整和可提交十六进制手输的统一颜色编辑器。</summary>
public partial class RgbaColorEditor : UserControl
{
    private bool suppressTextTracking;
    private bool suppressAlphaTracking;
    private bool textDirty;

    public RgbaColorEditor()
    {
        InitializeComponent();
        Loaded += (_, _) => SynchronizeFromValue(Value);
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
            ShowError("请输入 #RRGGBB 或 #AARRGGBB 颜色值。");
            return false;
        }

        ApplyColor(color);
        return true;
    }

    public void CancelEdit()
    {
        textDirty = false;
        ClearError();
        SynchronizeFromValue(Value);
    }

    public void ApplyPaletteColor(string rgbText)
    {
        if (!RgbaColorText.TryParse(rgbText, out RgbaColor selected))
        {
            return;
        }

        byte alpha = RgbaColorText.TryParse(Value, out RgbaColor current) ? current.Alpha : byte.MaxValue;
        ApplyColor(selected with { Alpha = alpha });
    }

    /// <summary>提交调色板透明度，供滑块和键盘辅助操作复用同一颜色更新路径。</summary>
    public void ApplyAlpha(byte alpha)
    {
        RgbaColor color = RgbaColorText.TryParse(Value, out RgbaColor current) ? current : RgbaColor.Black;
        ApplyColor(color with { Alpha = alpha });
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        RgbaColorEditor editor = (RgbaColorEditor)dependencyObject;
        if (editor.ColorTextBox.IsKeyboardFocusWithin && editor.textDirty)
        {
            return;
        }
        editor.SynchronizeFromValue(e.NewValue as string);
    }

    private void OnPaletteColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string rgb })
        {
            ApplyPaletteColor(rgb);
        }
    }

    private void OnAlphaChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (suppressAlphaTracking || !IsLoaded)
        {
            return;
        }

        ApplyAlpha(checked((byte)Math.Round(e.NewValue)));
    }

    private void OnColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!suppressTextTracking)
        {
            textDirty = true;
            ClearError();
        }
    }

    private void OnColorTextLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (textDirty)
        {
            _ = Commit();
        }
    }

    private void OnColorTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = Commit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    private void ApplyColor(RgbaColor color)
    {
        string normalized = RgbaColorText.Format(color);
        textDirty = false;
        ClearError();
        SetCurrentValue(ValueProperty, normalized);
        SynchronizeFromValue(normalized);
    }

    private void SynchronizeFromValue(string? value)
    {
        RgbaColor color = RgbaColorText.TryParse(value, out RgbaColor parsed) ? parsed : RgbaColor.Black;
        string normalized = RgbaColorText.Format(color);
        suppressTextTracking = true;
        ColorTextBox.Text = normalized;
        suppressTextTracking = false;
        suppressAlphaTracking = true;
        AlphaSlider.Value = color.Alpha;
        suppressAlphaTracking = false;
        AlphaText.Text = $"{color.Alpha / 255d:P0}";
        ColorPreview.Background = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }

    private void ShowError(string message)
    {
        ColorTextBox.BorderBrush = Brushes.IndianRed;
        ColorTextBox.ToolTip = message;
        AutomationProperties.SetHelpText(ColorTextBox, message);
    }

    private void ClearError()
    {
        ColorTextBox.ClearValue(BorderBrushProperty);
        ColorTextBox.ClearValue(ToolTipProperty);
        AutomationProperties.SetHelpText(ColorTextBox, string.Empty);
    }
}
