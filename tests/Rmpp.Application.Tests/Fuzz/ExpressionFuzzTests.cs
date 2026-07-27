using Rmpp.Application.Data.Expressions;
using Xunit;

namespace Rmpp.Application.Tests.Fuzz;

public sealed class ExpressionFuzzTests
{
    [Fact]
    public void RandomExpressionTextOnlyProducesAstOrControlledDiagnostic()
    {
        Random random = new(20260727);
        const string alphabet = "abcXYZ012[]()?,:+-_' \"$./\\";
        ExpressionParser parser = new();
        ExpressionEvaluator evaluator = new();
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            string source = new(Enumerable.Range(0, random.Next(0, 160)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            try
            {
                ExpressionNode ast = parser.Parse(source);
                try
                {
                    string result = evaluator.EvaluateText(ast, new ExpressionEvaluationContext
                    {
                        Fields = new Dictionary<string, string?> { ["a"] = "值" },
                    });
                    Assert.True(result.Length <= new ExpressionLimits().MaximumOutputCharacters);
                }
                catch (ExpressionEvaluationException) { }
            }
            catch (ExpressionParseException exception)
            {
                Assert.InRange(exception.Span.Start, 0, source.Length);
            }
        }
    }

    [Fact]
    public void RandomUnsupportedFunctionNamesNeverExecute()
    {
        Random random = new(17);
        ExpressionParser parser = new();
        for (int iteration = 0; iteration < 200; iteration++)
        {
            string name = "unsafe" + random.NextInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Throws<ExpressionParseException>(() => parser.Parse(name + "('x')"));
        }
    }
}
