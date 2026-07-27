using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Encodings.Web;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Styles;

namespace Rmpp.Infrastructure.Templates;

/// <summary>集中定义开放JSON格式的命名、枚举和多态类型判别器。</summary>
public static class TemplateJsonOptions
{
    public static JsonSerializerOptions Create(bool writeIndented = true)
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(ConfigurePolymorphism);

        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = writeIndented,
            TypeInfoResolver = resolver,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        DomainValueObjectJsonConverters.AddTo(options);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void ConfigurePolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(TemplateElement))
        {
            typeInfo.PolymorphismOptions = CreateOptions(
                (typeof(TextElement), "text"), (typeof(DateTimeElement), "dateTime"),
                (typeof(SerialElement), "serial"), (typeof(ImageElement), "image"),
                (typeof(BarcodeElement), "barcode"), (typeof(LineElement), "line"),
                (typeof(RectangleElement), "rectangle"), (typeof(EllipseElement), "ellipse"),
                (typeof(ArcElement), "arc"), (typeof(SectorElement), "sector"),
                (typeof(PolylineElement), "polyline"), (typeof(PolygonElement), "polygon"));
        }
        else if (typeInfo.Type == typeof(FillStyle))
        {
            typeInfo.PolymorphismOptions = CreateOptions(
                (typeof(NoFill), "none"), (typeof(SolidFill), "solid"), (typeof(HatchFill), "hatch"));
        }
        else if (typeInfo.Type == typeof(DocumentLayout))
        {
            typeInfo.PolymorphismOptions = CreateOptions(
                (typeof(SinglePageLayout), "singlePage"),
                (typeof(RollLabelLayout), "rollLabel"),
                (typeof(SheetLabelLayout), "sheetLabel"));
        }
    }

    private static JsonPolymorphismOptions CreateOptions(params (Type Type, string Discriminator)[] derivedTypes)
    {
        JsonPolymorphismOptions options = new()
        {
            TypeDiscriminatorPropertyName = "$type",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach ((Type type, string discriminator) in derivedTypes)
        {
            options.DerivedTypes.Add(new JsonDerivedType(type, discriminator));
        }

        return options;
    }
}
