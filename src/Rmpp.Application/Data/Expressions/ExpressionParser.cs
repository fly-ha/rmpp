using System.Globalization;
using System.Text;

namespace Rmpp.Application.Data.Expressions;

/// <summary>把受限表达式解析为固定 AST，并在构造过程中执行节点和深度上限。</summary>
public sealed class ExpressionParser(ExpressionLimits? limits = null)
{
    private readonly ExpressionLimits limits = limits ?? new ExpressionLimits();
    private IReadOnlyList<Token> tokens = Array.Empty<Token>();
    private int position;
    private int nodeCount;

    public ExpressionNode Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > limits.MaximumSourceCharacters)
        {
            throw new ExpressionParseException(
                $"Expression exceeds the configured {limits.MaximumSourceCharacters} character limit.",
                new ExpressionTextSpan(0, source.Length));
        }

        tokens = Tokenize(source);
        position = 0;
        nodeCount = 0;
        ExpressionNode expression = ParseConditional(1);
        Token end = Current;
        if (end.Kind != TokenKind.End)
        {
            throw Error("Unexpected token after expression.", end);
        }

        return expression;
    }

    private ExpressionNode ParseConditional(int depth)
    {
        EnsureDepth(depth);
        ExpressionNode condition = ParseComparison(depth + 1);
        if (!Match(TokenKind.Question))
        {
            return condition;
        }

        ExpressionNode whenTrue = ParseConditional(depth + 1);
        Expect(TokenKind.Colon, "Conditional expression requires ':'.");
        ExpressionNode whenFalse = ParseConditional(depth + 1);
        return Node(new ConditionalNode(
            condition,
            whenTrue,
            whenFalse,
            Span(condition.Span.Start, whenFalse.Span.End)));
    }

    private ExpressionNode ParseComparison(int depth)
    {
        ExpressionNode left = ParseConcatenation(depth + 1);
        while (Current.Kind is TokenKind.EqualEqual or TokenKind.BangEqual
               or TokenKind.Greater or TokenKind.GreaterEqual
               or TokenKind.Less or TokenKind.LessEqual)
        {
            Token operation = Advance();
            ExpressionNode right = ParseConcatenation(depth + 1);
            left = Node(new BinaryNode(
                operation.Kind switch
                {
                    TokenKind.EqualEqual => ExpressionBinaryOperator.Equal,
                    TokenKind.BangEqual => ExpressionBinaryOperator.NotEqual,
                    TokenKind.Greater => ExpressionBinaryOperator.GreaterThan,
                    TokenKind.GreaterEqual => ExpressionBinaryOperator.GreaterThanOrEqual,
                    TokenKind.Less => ExpressionBinaryOperator.LessThan,
                    _ => ExpressionBinaryOperator.LessThanOrEqual,
                },
                left,
                right,
                Span(left.Span.Start, right.Span.End)));
        }

        return left;
    }

    private ExpressionNode ParseConcatenation(int depth)
    {
        ExpressionNode left = ParsePrimary(depth + 1);
        while (Match(TokenKind.Plus))
        {
            ExpressionNode right = ParsePrimary(depth + 1);
            left = Node(new BinaryNode(
                ExpressionBinaryOperator.Concatenate,
                left,
                right,
                Span(left.Span.Start, right.Span.End)));
        }

        return left;
    }

    private ExpressionNode ParsePrimary(int depth)
    {
        EnsureDepth(depth);
        Token token = Advance();
        return token.Kind switch
        {
            TokenKind.String => Node(new LiteralNode(token.Value, token.Span)),
            TokenKind.Number => Node(new LiteralNode(ParseNumber(token), token.Span)),
            TokenKind.True => Node(new LiteralNode(true, token.Span)),
            TokenKind.False => Node(new LiteralNode(false, token.Span)),
            TokenKind.Null => Node(new LiteralNode(null, token.Span)),
            TokenKind.Field => Node(new FieldNode((string)token.Value!, token.Span)),
            TokenKind.Identifier => ParseFunction(token, depth + 1),
            TokenKind.LeftParen => ParseParenthesized(token, depth + 1),
            _ => throw Error("Expected a literal, field, function, or parenthesized expression.", token),
        };
    }

    private FunctionNode ParseFunction(Token name, int depth)
    {
        if (!ExpressionFunctionRegistry.IsAllowed(name.Text))
        {
            throw Error($"Function '{name.Text}' is not allowed.", name);
        }

        Expect(TokenKind.LeftParen, "Function name must be followed by '('.");
        List<ExpressionNode> arguments = [];
        if (Current.Kind != TokenKind.RightParen)
        {
            do
            {
                if (arguments.Count >= limits.MaximumFunctionArguments)
                {
                    throw Error(
                        $"Function exceeds the configured {limits.MaximumFunctionArguments} argument limit.",
                        Current);
                }

                arguments.Add(ParseConditional(depth + 1));
            }
            while (Match(TokenKind.Comma));
        }

        Token close = Expect(TokenKind.RightParen, "Function call requires ')'.");
        return Node(new FunctionNode(
            name.Text,
            arguments,
            Span(name.Span.Start, close.Span.End)));
    }

    private ExpressionNode ParseParenthesized(Token open, int depth)
    {
        ExpressionNode expression = ParseConditional(depth + 1);
        Token close = Expect(TokenKind.RightParen, "Parenthesized expression requires ')'.");
        return expression with { Span = Span(open.Span.Start, close.Span.End) };
    }

    private T Node<T>(T node) where T : ExpressionNode
    {
        nodeCount++;
        if (nodeCount > limits.MaximumNodes)
        {
            throw new ExpressionParseException(
                $"Expression exceeds the configured {limits.MaximumNodes} node limit.",
                node.Span);
        }

        return node;
    }

    private void EnsureDepth(int depth)
    {
        if (depth > limits.MaximumDepth)
        {
            throw Error(
                $"Expression exceeds the configured {limits.MaximumDepth} nesting limit.",
                Current);
        }
    }

    private static decimal ParseNumber(Token token)
    {
        if (!decimal.TryParse(token.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            throw new ExpressionParseException("Number literal is invalid.", token.Span);
        }

        return value;
    }

    private Token Current => tokens[Math.Min(position, tokens.Count - 1)];

    private Token Advance() => tokens[Math.Min(position++, tokens.Count - 1)];

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        position++;
        return true;
    }

    private Token Expect(TokenKind kind, string message)
    {
        if (Current.Kind != kind)
        {
            throw Error(message, Current);
        }

        return Advance();
    }

    private static ExpressionParseException Error(string message, Token token) => new(message, token.Span);

    private static ExpressionTextSpan Span(int start, int end) => new(start, checked(end - start));

    private static List<Token> Tokenize(string source)
    {
        List<Token> result = [];
        int index = 0;
        while (index < source.Length)
        {
            char character = source[index];
            if (char.IsWhiteSpace(character))
            {
                index++;
                continue;
            }

            int start = index;
            switch (character)
            {
                case '(':
                    result.Add(Token.Simple(TokenKind.LeftParen, source, index++));
                    break;
                case ')':
                    result.Add(Token.Simple(TokenKind.RightParen, source, index++));
                    break;
                case ',':
                    result.Add(Token.Simple(TokenKind.Comma, source, index++));
                    break;
                case '?':
                    result.Add(Token.Simple(TokenKind.Question, source, index++));
                    break;
                case ':':
                    result.Add(Token.Simple(TokenKind.Colon, source, index++));
                    break;
                case '+':
                    result.Add(Token.Simple(TokenKind.Plus, source, index++));
                    break;
                case '=' when index + 1 < source.Length && source[index + 1] == '=':
                    result.Add(Token.Pair(TokenKind.EqualEqual, source, ref index));
                    break;
                case '!' when index + 1 < source.Length && source[index + 1] == '=':
                    result.Add(Token.Pair(TokenKind.BangEqual, source, ref index));
                    break;
                case '>' when index + 1 < source.Length && source[index + 1] == '=':
                    result.Add(Token.Pair(TokenKind.GreaterEqual, source, ref index));
                    break;
                case '<' when index + 1 < source.Length && source[index + 1] == '=':
                    result.Add(Token.Pair(TokenKind.LessEqual, source, ref index));
                    break;
                case '>':
                    result.Add(Token.Simple(TokenKind.Greater, source, index++));
                    break;
                case '<':
                    result.Add(Token.Simple(TokenKind.Less, source, index++));
                    break;
                case '\'' or '"':
                    result.Add(ReadString(source, ref index));
                    break;
                case '[':
                    result.Add(ReadField(source, ref index));
                    break;
                default:
                    if (char.IsAsciiDigit(character)
                        || character == '-' && index + 1 < source.Length && char.IsAsciiDigit(source[index + 1]))
                    {
                        result.Add(ReadNumber(source, ref index));
                    }
                    else if (char.IsAsciiLetter(character) || character == '_')
                    {
                        result.Add(ReadIdentifier(source, ref index));
                    }
                    else
                    {
                        throw new ExpressionParseException(
                            $"Unexpected character '{character}'.",
                            new ExpressionTextSpan(start, 1));
                    }
                    break;
            }
        }

        result.Add(new Token(TokenKind.End, string.Empty, null, new ExpressionTextSpan(source.Length, 0)));
        return result;
    }

    private static Token ReadString(string source, ref int index)
    {
        int start = index;
        char quote = source[index++];
        StringBuilder value = new();
        while (index < source.Length)
        {
            char character = source[index++];
            if (character == quote)
            {
                string text = source[start..index];
                return new Token(TokenKind.String, text, value.ToString(), new ExpressionTextSpan(start, index - start));
            }

            if (character == '\\')
            {
                if (index >= source.Length)
                {
                    break;
                }

                char escaped = source[index++];
                value.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => escaped,
                });
            }
            else
            {
                value.Append(character);
            }
        }

        throw new ExpressionParseException("String literal is not closed.", new ExpressionTextSpan(start, source.Length - start));
    }

    private static Token ReadField(string source, ref int index)
    {
        int start = index++;
        int contentStart = index;
        while (index < source.Length && source[index] != ']')
        {
            index++;
        }

        if (index >= source.Length)
        {
            throw new ExpressionParseException("Field reference is not closed.", new ExpressionTextSpan(start, source.Length - start));
        }

        string name = source[contentStart..index].Trim();
        index++;
        if (name.Length == 0)
        {
            throw new ExpressionParseException("Field name cannot be empty.", new ExpressionTextSpan(start, index - start));
        }

        return new Token(TokenKind.Field, source[start..index], name, new ExpressionTextSpan(start, index - start));
    }

    private static Token ReadNumber(string source, ref int index)
    {
        int start = index;
        if (source[index] == '-')
        {
            index++;
        }

        while (index < source.Length && char.IsAsciiDigit(source[index]))
        {
            index++;
        }

        if (index < source.Length && source[index] == '.')
        {
            index++;
            while (index < source.Length && char.IsAsciiDigit(source[index]))
            {
                index++;
            }
        }

        string text = source[start..index];
        return new Token(TokenKind.Number, text, null, new ExpressionTextSpan(start, index - start));
    }

    private static Token ReadIdentifier(string source, ref int index)
    {
        int start = index;
        while (index < source.Length && (char.IsAsciiLetterOrDigit(source[index]) || source[index] == '_'))
        {
            index++;
        }

        string text = source[start..index];
        TokenKind kind = text.ToLowerInvariant() switch
        {
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            "null" => TokenKind.Null,
            _ => TokenKind.Identifier,
        };
        return new Token(kind, text, null, new ExpressionTextSpan(start, index - start));
    }

    private enum TokenKind
    {
        End,
        String,
        Number,
        True,
        False,
        Null,
        Field,
        Identifier,
        LeftParen,
        RightParen,
        Comma,
        Question,
        Colon,
        Plus,
        EqualEqual,
        BangEqual,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
    }

    private sealed record Token(TokenKind Kind, string Text, object? Value, ExpressionTextSpan Span)
    {
        public static Token Simple(TokenKind kind, string source, int index) =>
            new(kind, source[index].ToString(), null, new ExpressionTextSpan(index, 1));

        public static Token Pair(TokenKind kind, string source, ref int index)
        {
            Token token = new(kind, source.Substring(index, 2), null, new ExpressionTextSpan(index, 2));
            index += 2;
            return token;
        }
    }
}
