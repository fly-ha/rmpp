using System.Text.Json;
using System.Text.Json.Serialization;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Styles;

namespace Rmpp.Infrastructure.Templates;

/// <summary>为只读领域值对象提供稳定、精简并严格校验的 JSON 表示。</summary>
internal static class DomainValueObjectJsonConverters
{
    public static void AddTo(JsonSerializerOptions options)
    {
        options.Converters.Add(new MmPointJsonConverter());
        options.Converters.Add(new MmSizeJsonConverter());
        options.Converters.Add(new MmRectJsonConverter());
        options.Converters.Add(new MmThicknessJsonConverter());
        options.Converters.Add(new MmRangeJsonConverter());
        options.Converters.Add(new AngleJsonConverter());
        options.Converters.Add(new RgbaColorJsonConverter());
        options.Converters.Add(new LabelCellJsonConverter());
    }

    /// <summary>统一拒绝未知、重复和缺失字段，避免模板值对象被静默降级。</summary>
    private abstract class StrictObjectJsonConverter<T>(params string[] propertyNames) : JsonConverter<T>
    {
        private readonly HashSet<string> allowedProperties = new(propertyNames, StringComparer.Ordinal);

        public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement element = json.RootElement;
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"Expected an object for {typeof(T).Name}.");
            }

            HashSet<string> encountered = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!allowedProperties.Contains(property.Name))
                {
                    throw new JsonException($"Unknown property '{property.Name}' for {typeof(T).Name}.");
                }

                if (!encountered.Add(property.Name))
                {
                    throw new JsonException($"Duplicate property '{property.Name}' for {typeof(T).Name}.");
                }
            }

            if (encountered.Count != allowedProperties.Count)
            {
                string[] missing = allowedProperties.Except(encountered, StringComparer.Ordinal).ToArray();
                throw new JsonException($"Missing properties for {typeof(T).Name}: {string.Join(", ", missing)}.");
            }

            return Create(element, options);
        }

        public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            WriteProperties(writer, value, options);
            writer.WriteEndObject();
        }

        protected abstract T Create(JsonElement element, JsonSerializerOptions options);

        protected abstract void WriteProperties(Utf8JsonWriter writer, T value, JsonSerializerOptions options);

        protected static double GetDouble(JsonElement element, string propertyName) =>
            element.GetProperty(propertyName).GetDouble();

        protected static int GetInt32(JsonElement element, string propertyName) =>
            element.GetProperty(propertyName).GetInt32();

        protected static byte GetByte(JsonElement element, string propertyName) =>
            element.GetProperty(propertyName).GetByte();

        protected static TValue GetValue<TValue>(
            JsonElement element,
            string propertyName,
            JsonSerializerOptions options) =>
            element.GetProperty(propertyName).Deserialize<TValue>(options)
            ?? throw new JsonException($"Property '{propertyName}' cannot be null.");
    }

    private sealed class MmPointJsonConverter() : StrictObjectJsonConverter<MmPoint>("x", "y")
    {
        protected override MmPoint Create(JsonElement element, JsonSerializerOptions options) =>
            new(GetDouble(element, "x"), GetDouble(element, "y"));

        protected override void WriteProperties(Utf8JsonWriter writer, MmPoint value, JsonSerializerOptions options)
        {
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
        }
    }

    private sealed class MmSizeJsonConverter() : StrictObjectJsonConverter<MmSize>("width", "height")
    {
        protected override MmSize Create(JsonElement element, JsonSerializerOptions options) =>
            new(GetDouble(element, "width"), GetDouble(element, "height"));

        protected override void WriteProperties(Utf8JsonWriter writer, MmSize value, JsonSerializerOptions options)
        {
            writer.WriteNumber("width", value.Width);
            writer.WriteNumber("height", value.Height);
        }
    }

    private sealed class MmRectJsonConverter() : StrictObjectJsonConverter<MmRect>("x", "y", "width", "height")
    {
        protected override MmRect Create(JsonElement element, JsonSerializerOptions options) =>
            new(
                GetDouble(element, "x"),
                GetDouble(element, "y"),
                GetDouble(element, "width"),
                GetDouble(element, "height"));

        protected override void WriteProperties(Utf8JsonWriter writer, MmRect value, JsonSerializerOptions options)
        {
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("width", value.Width);
            writer.WriteNumber("height", value.Height);
        }
    }

    private sealed class MmThicknessJsonConverter()
        : StrictObjectJsonConverter<MmThickness>("left", "top", "right", "bottom")
    {
        protected override MmThickness Create(JsonElement element, JsonSerializerOptions options) =>
            new(
                GetDouble(element, "left"),
                GetDouble(element, "top"),
                GetDouble(element, "right"),
                GetDouble(element, "bottom"));

        protected override void WriteProperties(
            Utf8JsonWriter writer,
            MmThickness value,
            JsonSerializerOptions options)
        {
            writer.WriteNumber("left", value.Left);
            writer.WriteNumber("top", value.Top);
            writer.WriteNumber("right", value.Right);
            writer.WriteNumber("bottom", value.Bottom);
        }
    }

    private sealed class MmRangeJsonConverter() : StrictObjectJsonConverter<MmRange>("minimum", "maximum")
    {
        protected override MmRange Create(JsonElement element, JsonSerializerOptions options) =>
            new(GetDouble(element, "minimum"), GetDouble(element, "maximum"));

        protected override void WriteProperties(Utf8JsonWriter writer, MmRange value, JsonSerializerOptions options)
        {
            writer.WriteNumber("minimum", value.Minimum);
            writer.WriteNumber("maximum", value.Maximum);
        }
    }

    private sealed class AngleJsonConverter() : StrictObjectJsonConverter<Angle>("degrees")
    {
        protected override Angle Create(JsonElement element, JsonSerializerOptions options) =>
            new(GetDouble(element, "degrees"));

        protected override void WriteProperties(Utf8JsonWriter writer, Angle value, JsonSerializerOptions options) =>
            writer.WriteNumber("degrees", value.Degrees);
    }

    private sealed class RgbaColorJsonConverter()
        : StrictObjectJsonConverter<RgbaColor>("red", "green", "blue", "alpha")
    {
        protected override RgbaColor Create(JsonElement element, JsonSerializerOptions options) =>
            new(
                GetByte(element, "red"),
                GetByte(element, "green"),
                GetByte(element, "blue"),
                GetByte(element, "alpha"));

        protected override void WriteProperties(Utf8JsonWriter writer, RgbaColor value, JsonSerializerOptions options)
        {
            writer.WriteNumber("red", value.Red);
            writer.WriteNumber("green", value.Green);
            writer.WriteNumber("blue", value.Blue);
            writer.WriteNumber("alpha", value.Alpha);
        }
    }

    private sealed class LabelCellJsonConverter()
        : StrictObjectJsonConverter<LabelCell>("index", "row", "column", "bounds")
    {
        protected override LabelCell Create(JsonElement element, JsonSerializerOptions options) =>
            new(
                GetInt32(element, "index"),
                GetInt32(element, "row"),
                GetInt32(element, "column"),
                GetValue<MmRect>(element, "bounds", options));

        protected override void WriteProperties(Utf8JsonWriter writer, LabelCell value, JsonSerializerOptions options)
        {
            writer.WriteNumber("index", value.Index);
            writer.WriteNumber("row", value.Row);
            writer.WriteNumber("column", value.Column);
            writer.WritePropertyName("bounds");
            JsonSerializer.Serialize(writer, value.Bounds, options);
        }
    }
}
