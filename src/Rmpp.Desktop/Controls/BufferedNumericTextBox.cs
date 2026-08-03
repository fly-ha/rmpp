using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rmpp.Desktop.Controls;

/// <summary>在编辑期间保留数字文本中间态，仅在确认时把有效值写回文档绑定。</summary>
public sealed class BufferedNumericTextBox : TextBox
{
    private bool suppressTextTracking;
    private bool isDirty;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double?),
        typeof(BufferedNumericTextBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(BufferedNumericTextBox),
        new PropertyMetadata(double.NegativeInfinity));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(BufferedNumericTextBox),
        new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty IsIntegerProperty = DependencyProperty.Register(
        nameof(IsInteger),
        typeof(bool),
        typeof(BufferedNumericTextBox),
        new PropertyMetadata(false));

    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(
        nameof(AllowEmpty),
        typeof(bool),
        typeof(BufferedNumericTextBox),
        new PropertyMetadata(false));

    public static readonly DependencyProperty DisallowZeroProperty = DependencyProperty.Register(
        nameof(DisallowZero),
        typeof(bool),
        typeof(BufferedNumericTextBox),
        new PropertyMetadata(false));

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool IsInteger
    {
        get => (bool)GetValue(IsIntegerProperty);
        set => SetValue(IsIntegerProperty, value);
    }

    public bool AllowEmpty
    {
        get => (bool)GetValue(AllowEmptyProperty);
        set => SetValue(AllowEmptyProperty, value);
    }

    public bool DisallowZero
    {
        get => (bool)GetValue(DisallowZeroProperty);
        set => SetValue(DisallowZeroProperty, value);
    }

    public bool Commit()
    {
        string candidate = Text.Trim();
        if (candidate.Length == 0 && AllowEmpty)
        {
            SetCurrentValue(ValueProperty, null);
            isDirty = false;
            ClearError();
            return true;
        }

        if (!TryParse(candidate, out double parsed))
        {
            ShowError(IsInteger ? "请输入整数。" : "请输入有效数字，例如 1.25。");
            return false;
        }

        if (parsed < Minimum || parsed > Maximum)
        {
            string minimum = double.IsNegativeInfinity(Minimum) ? "不限" : Format(Minimum);
            string maximum = double.IsPositiveInfinity(Maximum) ? "不限" : Format(Maximum);
            ShowError($"数值范围应为 {minimum} 至 {maximum}。");
            return false;
        }

        if (DisallowZero && parsed == 0)
        {
            ShowError("数值不能为 0。");
            return false;
        }

        SetCurrentValue(ValueProperty, parsed);
        isDirty = false;
        ClearError();
        SetText(Format(parsed));
        return true;
    }

    public void CancelEdit()
    {
        isDirty = false;
        ClearError();
        SetText(Value is { } value ? Format(value) : string.Empty);
        SelectAll();
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if (!suppressTextTracking)
        {
            isDirty = true;
            ClearError();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = Commit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        if (isDirty)
        {
            _ = Commit();
        }
        base.OnLostKeyboardFocus(e);
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        BufferedNumericTextBox control = (BufferedNumericTextBox)dependencyObject;
        if (control.IsKeyboardFocusWithin && control.isDirty)
        {
            return;
        }

        control.isDirty = false;
        control.ClearError();
        control.SetText(e.NewValue is double value ? control.Format(value) : string.Empty);
    }

    private bool TryParse(string text, out double value)
    {
        if (IsInteger)
        {
            bool success = long.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out long integer);
            value = integer;
            return success;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value);
    }

    private string Format(double value) => IsInteger
        ? value.ToString("0", CultureInfo.CurrentCulture)
        : value.ToString("G15", CultureInfo.CurrentCulture);

    private void SetText(string value)
    {
        suppressTextTracking = true;
        Text = value;
        suppressTextTracking = false;
    }

    private void ShowError(string message)
    {
        BorderBrush = Brushes.IndianRed;
        ToolTip = message;
        AutomationProperties.SetHelpText(this, message);
    }

    private void ClearError()
    {
        ClearValue(BorderBrushProperty);
        ClearValue(ToolTipProperty);
        AutomationProperties.SetHelpText(this, string.Empty);
    }
}
