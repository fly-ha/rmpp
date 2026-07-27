using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Data.Expressions;

namespace Rmpp.Desktop.ViewModels;

/// <summary>受限表达式编辑器：只调用固定 AST 和函数注册表，不提供脚本、反射或 I/O 入口。</summary>
public sealed partial class ExpressionEditorViewModel : ObservableObject
{
    private readonly ExpressionParser parser;
    private readonly ExpressionEvaluator evaluator;

    public ExpressionEditorViewModel(ExpressionParser? parser = null, ExpressionEvaluator? evaluator = null)
    {
        this.parser = parser ?? new ExpressionParser();
        this.evaluator = evaluator ?? new ExpressionEvaluator();
        ValidateCommand = new RelayCommand(Validate);
        InsertFieldCommand = new RelayCommand<string>(InsertField);
    }

    [ObservableProperty]
    private string source = string.Empty;

    [ObservableProperty]
    private string sampleResult = string.Empty;

    [ObservableProperty]
    private string diagnostic = string.Empty;

    [ObservableProperty]
    private bool hasError;

    public ObservableCollection<string> Fields { get; } = [];
    public IReadOnlyList<string> Functions { get; } =
    ["concat", "default", "if", "eq", "ne", "gt", "gte", "lt", "lte", "substring", "padleft", "padright", "upper", "lower", "formatnumber", "formatdate"];
    public IRelayCommand ValidateCommand { get; }
    public IRelayCommand<string> InsertFieldCommand { get; }
    public event EventHandler? Validated;

    public void SetFields(IEnumerable<string> fields)
    {
        Fields.Clear();
        foreach (string field in fields.Where(static value => !string.IsNullOrWhiteSpace(value))) Fields.Add(field);
    }

    public void SetSampleValues(IReadOnlyDictionary<string, string?> values)
    {
        if (!HasError && !string.IsNullOrWhiteSpace(Source))
        {
            try
            {
                string expression = Source.StartsWith('=') ? Source[1..] : Source;
                SampleResult = evaluator.EvaluateText(parser.Parse(expression), new ExpressionEvaluationContext { Fields = values });
            }
            catch (Exception exception) when (exception is ExpressionParseException or ExpressionEvaluationException)
            {
                SampleResult = string.Empty;
            }
        }
    }

    private void Validate()
    {
        try
        {
            string expression = Source.StartsWith('=') ? Source[1..] : Source;
            parser.Parse(expression);
            Diagnostic = "表达式有效。";
            HasError = false;
        }
        catch (ExpressionParseException exception)
        {
            Diagnostic = exception.Message;
            HasError = true;
            SampleResult = string.Empty;
        }

        Validated?.Invoke(this, EventArgs.Empty);
    }

    private void InsertField(string? field)
    {
        if (!string.IsNullOrWhiteSpace(field))
        {
            Source += (Source.Length == 0 ? string.Empty : " + ") + $"[{field}]";
            Validate();
        }
    }
}
