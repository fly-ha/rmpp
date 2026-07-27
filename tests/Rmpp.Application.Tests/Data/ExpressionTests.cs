using System.Globalization;
using Rmpp.Application.Data.Expressions;
using Xunit;

namespace Rmpp.Application.Tests.Data;

public sealed class ExpressionTests
{
    [Fact]
    public void EvaluatesFieldsConcatenationConditionAndFormatting()
    {
        ExpressionNode node = new ExpressionParser().Parse(
            "[数量] > 1 ? concat('订单-', upper([编号]), '-', formatNumber([金额], '0.00')) : '单件'");
        ExpressionEvaluationContext context = new()
        {
            Fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["数量"] = "2",
                ["编号"] = "ab12",
                ["金额"] = "12.5",
            },
            Culture = CultureInfo.InvariantCulture,
        };

        string result = new ExpressionEvaluator().EvaluateText(node, context);

        Assert.Equal("订单-AB12-12.50", result);
    }

    [Fact]
    public void DefaultSubstringPaddingAndCaseAreDeterministic()
    {
        ExpressionNode node = new ExpressionParser().Parse(
            "padLeft(substring(default([缺失], 'abcdef'), 1, 3), 5, '0') + lower('XY')");

        string result = new ExpressionEvaluator().EvaluateText(node, new ExpressionEvaluationContext());

        Assert.Equal("00bcdxy", result);
    }

    [Theory]
    [InlineData("file('secret.txt')")]
    [InlineData("process('cmd')")]
    [InlineData("environment('PATH')")]
    [InlineData("reflection('System.IO.File')")]
    [InlineData("network('https://example.com')")]
    public void ProhibitedOrUnknownFunctionsFailDuringParsing(string source)
    {
        ExpressionParseException error = Assert.Throws<ExpressionParseException>(() => new ExpressionParser().Parse(source));

        Assert.Equal(0, error.Span.Start);
        Assert.Contains("not allowed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParserReportsSourceLocation()
    {
        ExpressionParseException error = Assert.Throws<ExpressionParseException>(
            () => new ExpressionParser().Parse("concat('a', [Field]"));

        Assert.True(error.Span.Start > 0);
        Assert.Contains(")", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComplexityAndOutputLimitsAreEnforced()
    {
        ExpressionParser parser = new(new ExpressionLimits { MaximumNodes = 3 });
        Assert.Throws<ExpressionParseException>(() => parser.Parse("concat('a', 'b', 'c')"));

        ExpressionNode output = new ExpressionParser().Parse("'abcd' + 'efgh'");
        ExpressionEvaluator evaluator = new(new ExpressionLimits { MaximumOutputCharacters = 5 });
        Assert.Throws<ExpressionEvaluationException>(() => evaluator.EvaluateText(output, new ExpressionEvaluationContext()));
    }
}
