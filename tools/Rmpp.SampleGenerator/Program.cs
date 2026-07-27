using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Templates;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Skia;
using SkiaSharp;

namespace Rmpp.SampleGenerator;

/// <summary>生成可公开再分发、无第三方资产和真实个人数据的首版模板样例。</summary>
internal static class Program
{
    private static readonly DateTimeOffset SampleTimestamp = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    public static async Task<int> Main(string[] args)
    {
        string outputDirectory = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Environment.CurrentDirectory, "samples"));
        Directory.CreateDirectory(outputDirectory);
        AtomicTemplateFileWriter writer = new();
        RmppPackageReader reader = new();

        foreach ((string fileName, TemplateDocument document) in CreateSamples())
        {
            string path = Path.Combine(outputDirectory, fileName);
            byte[] preview = CreatePreview(document);
            await writer.WriteAsync(path, new TemplatePackageContent { Document = document, PreviewPng = preview });
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(fileName) + ".png"), preview);
            await using FileStream validation = File.OpenRead(path);
            TemplatePackageContent reopened = await reader.ReadAsync(validation);
            if (reopened.Document.Id != document.Id || reopened.Document.Elements.Count != document.Elements.Count)
            {
                throw new InvalidOperationException($"样例回读校验失败：{fileName}");
            }
            Console.WriteLine(path);
        }

        return 0;
    }

    private static IReadOnlyList<(string FileName, TemplateDocument Document)> CreateSamples() =>
    [
        ("certificate-overlay.rmpp", CertificateOverlay()),
        ("waybill.rmpp", Waybill()),
        ("roll-label.rmpp", RollLabel()),
        ("a4-sheet-labels.rmpp", A4SheetLabels()),
        ("polygons-hatches.rmpp", PolygonsAndHatches()),
        ("serial-numbers.rmpp", SerialNumbers()),
        ("csv-workflow.rmpp", CsvWorkflow()),
        ("excel-workflow.rmpp", ExcelWorkflow()),
    ];

    private static TemplateDocument CertificateOverlay()
    {
        Guid layer = StableGuid(1, 1);
        return CreateDocument(
            1,
            "证书套打",
            "A4 预印证书定位示例，浅色边框用于确认物理边界。",
            new MediaDefinition("A4", new MmSize(210, 297)),
            new SinglePageLayout(),
            layer,
            [
                Text(1, layer, "证书标题", new MmRect(35, 50, 140, 18), "荣誉证书", 26, true, TextHorizontalAlignment.Center),
                Text(2, layer, "姓名", new MmRect(65, 105, 80, 12), "张三", 18, true, TextHorizontalAlignment.Center),
                Text(3, layer, "正文", new MmRect(35, 130, 140, 35), "在离线定位打印与模板设计实践中表现优秀，特发此证。", 14, false, TextHorizontalAlignment.Center),
                Text(4, layer, "日期", new MmRect(125, 220, 55, 10), "2026年7月27日", 11, false, TextHorizontalAlignment.Center),
                Rectangle(5, layer, "证书边框", new MmRect(18, 18, 174, 261), new StrokeStyle { WidthMm = 0.6, Color = new RgbaColor(170, 30, 30) }, FillStyle.None),
            ]);
    }

    private static TemplateDocument Waybill()
    {
        Guid layer = StableGuid(2, 1);
        FieldDefinition[] fields = [new("运单号"), new("收件人"), new("地址")];
        SampleValue[] samples = [new("运单号", "RMPP-20260727-001"), new("收件人", "示例客户"), new("地址", "示例市示例路 100 号")];
        return CreateDocument(
            2,
            "快递运单",
            "100 × 150 mm 运单示例，包含固定文字、字段和 Code 128。",
            new MediaDefinition("Waybill 100x150", new MmSize(100, 150)),
            new SinglePageLayout(),
            layer,
            [
                Rectangle(1, layer, "外框", new MmRect(3, 3, 94, 144), new StrokeStyle { WidthMm = 0.35 }, FillStyle.None),
                Text(2, layer, "标题", new MmRect(8, 8, 84, 12), "红枫叶离线运单", 16, true, TextHorizontalAlignment.Center),
                Text(3, layer, "收件人", new MmRect(8, 28, 84, 12), "='收件人：' + [收件人]", 12, true),
                Text(4, layer, "地址", new MmRect(8, 43, 84, 28), "='地址：' + [地址]", 11),
                Barcode(5, layer, "运单条码", new MmRect(10, 82, 80, 30), BarcodeSymbology.Code128, "=[运单号]"),
                Text(6, layer, "运单号", new MmRect(10, 115, 80, 10), "=[运单号]", 10, false, TextHorizontalAlignment.Center),
            ], fields, samples);
    }

    private static TemplateDocument RollLabel()
    {
        Guid layer = StableGuid(3, 1);
        FieldDefinition[] fields = [new("品名"), new("批次"), new("二维码")];
        SampleValue[] samples = [new("品名", "离线标签纸"), new("批次", "B20260727"), new("二维码", "RMPP://sample/B20260727")];
        return CreateDocument(
            3,
            "卷筒产品标签",
            "60 × 40 mm 卷筒标签示例。",
            new MediaDefinition("Roll 60x40", new MmSize(60, 40)),
            new RollLabelLayout { LabelSize = new MmSize(60, 40), GapMm = 3 },
            layer,
            [
                Rectangle(1, layer, "边框", new MmRect(1, 1, 58, 38), new StrokeStyle { WidthMm = 0.3 }, FillStyle.None),
                Text(2, layer, "品名", new MmRect(4, 4, 35, 9), "=[品名]", 12, true),
                Text(3, layer, "批次", new MmRect(4, 16, 35, 7), "='批次：' + [批次]", 8),
                Barcode(4, layer, "二维码", new MmRect(41, 4, 15, 15), BarcodeSymbology.QrCode, "=[二维码]", false),
                Barcode(5, layer, "批次条码", new MmRect(4, 26, 52, 9), BarcodeSymbology.Code128, "=[批次]"),
            ], fields, samples);
    }

    private static TemplateDocument A4SheetLabels()
    {
        Guid layer = StableGuid(4, 1);
        SheetLabelLayout layout = new()
        {
            LabelSize = new MmSize(63.5, 33.9),
            Margins = new MmThickness(7.25, 12.9, 7.25, 12.9),
            Rows = 8,
            Columns = 3,
            HorizontalGapMm = 2.5,
            VerticalGapMm = 0,
            TraversalOrder = TraversalOrder.RowMajor,
            StartingCell = 1,
        };
        return CreateDocument(
            4,
            "A4 三列标签纸",
            "A4 3 × 8 标签纸拼版示例。",
            new MediaDefinition("A4", new MmSize(210, 297)),
            layout,
            layer,
            [
                Rectangle(1, layer, "标签边界", new MmRect(0.8, 0.8, 61.9, 32.3), new StrokeStyle { WidthMm = 0.2, DashPattern = new DashPattern([2, 1]) }, FillStyle.None),
                Text(2, layer, "标签标题", new MmRect(4, 4, 55.5, 8), "RMPP A4 标签", 11, true, TextHorizontalAlignment.Center),
                Text(3, layer, "标签说明", new MmRect(4, 15, 55.5, 12), "同一不可变计划用于预览、PDF 与打印", 8, false, TextHorizontalAlignment.Center),
            ]);
    }

    private static TemplateDocument PolygonsAndHatches()
    {
        Guid layer = StableGuid(5, 1);
        HatchFill hatch = new()
        {
            Pattern = HatchPattern.DiagonalCross,
            Foreground = new RgbaColor(180, 30, 30),
            Background = new RgbaColor(255, 245, 245),
            SpacingMm = 3,
            LineWidthMm = 0.25,
        };
        return CreateDocument(
            5,
            "多边形与底纹",
            "首版多边形、底纹、旋转和透明度示例。",
            new MediaDefinition("A5", new MmSize(148, 210)),
            new SinglePageLayout(),
            layer,
            [
                Text(1, layer, "标题", new MmRect(15, 12, 118, 12), "多边形与底纹", 18, true, TextHorizontalAlignment.Center),
                new PolygonElement
                {
                    Id = StableGuid(5, 2), Name = "五边形", LayerId = layer, Bounds = new MmRect(24, 45, 100, 90),
                    Points = [new MmPoint(50, 0), new MmPoint(100, 35), new MmPoint(80, 90), new MmPoint(20, 90), new MmPoint(0, 35)],
                    Stroke = new StrokeStyle { WidthMm = 0.8, Color = new RgbaColor(120, 0, 0) }, Fill = hatch, Opacity = 0.9,
                },
                Rectangle(3, layer, "网格底纹", new MmRect(30, 150, 88, 32), new StrokeStyle { WidthMm = 0.4 }, new HatchFill { Pattern = HatchPattern.Grid, SpacingMm = 4, Foreground = new RgbaColor(30, 80, 160), Background = new RgbaColor(235, 245, 255) }),
            ]);
    }

    private static TemplateDocument SerialNumbers()
    {
        Guid layer = StableGuid(6, 1);
        return CreateDocument(
            6,
            "流水号票券",
            "前缀、补零、步长和条码组合示例。",
            new MediaDefinition("Ticket 90x55", new MmSize(90, 55)),
            new SinglePageLayout(),
            layer,
            [
                Rectangle(1, layer, "票券边框", new MmRect(1, 1, 88, 53), new StrokeStyle { WidthMm = 0.4 }, new SolidFill(new RgbaColor(252, 248, 238))),
                Text(2, layer, "标题", new MmRect(8, 7, 74, 10), "离线定位打印票券", 13, true, TextHorizontalAlignment.Center),
                new SerialElement
                {
                    Id = StableGuid(6, 3), Name = "流水号", LayerId = layer, Bounds = new MmRect(12, 22, 66, 12),
                    Definition = new SerialDefinition { Start = 1, Step = 1, MinimumDigits = 6, Prefix = "RMPP-", AdvancePerCopy = false },
                    TextStyle = new TextStyle { FontSizePoints = 20, IsBold = true, HorizontalAlignment = TextHorizontalAlignment.Center },
                },
                Barcode(4, layer, "流水条码", new MmRect(12, 38, 66, 10), BarcodeSymbology.Code128, "RMPP-000001"),
            ]);
    }

    private static TemplateDocument CsvWorkflow()
    {
        Guid layer = StableGuid(7, 1);
        FieldDefinition[] fields = [new("订单号"), new("客户"), new("数量", FieldDataType.Number)];
        SampleValue[] samples = [new("订单号", "CSV-001"), new("客户", "示例客户"), new("数量", "12")];
        return CreateDocument(
            7,
            "CSV 变量数据",
            "导入 CSV 后绑定订单号、客户和数量。",
            new MediaDefinition("A6", new MmSize(105, 148)),
            new SinglePageLayout(),
            layer,
            [
                Text(1, layer, "订单号", new MmRect(10, 15, 85, 12), "='订单号：' + [订单号]", 14, true),
                Text(2, layer, "客户", new MmRect(10, 35, 85, 12), "='客户：' + default([客户], '未填写')", 12),
                Text(3, layer, "数量", new MmRect(10, 55, 85, 12), "='数量：' + formatNumber([数量], '0')", 12),
                Barcode(4, layer, "订单二维码", new MmRect(30, 82, 45, 45), BarcodeSymbology.QrCode, "=concat([订单号], '|', [客户], '|', [数量])", false),
            ], fields, samples);
    }

    private static TemplateDocument ExcelWorkflow()
    {
        Guid layer = StableGuid(8, 1);
        FieldDefinition[] fields = [new("物料编码"), new("物料名称"), new("单价", FieldDataType.Number), new("生产日期", FieldDataType.DateTime)];
        SampleValue[] samples = [new("物料编码", "MAT-001"), new("物料名称", "示例物料"), new("单价", "12.50"), new("生产日期", "2026-07-27")];
        return CreateDocument(
            8,
            "Excel 物料标签",
            "只读取 XLSX 缓存值，不执行公式、宏和外部链接。",
            new MediaDefinition("Label 80x50", new MmSize(80, 50)),
            new RollLabelLayout { LabelSize = new MmSize(80, 50), GapMm = 2 },
            layer,
            [
                Text(1, layer, "物料名称", new MmRect(4, 4, 50, 9), "=[物料名称]", 12, true),
                Text(2, layer, "物料编码", new MmRect(4, 15, 50, 7), "=[物料编码]", 9),
                Text(3, layer, "单价", new MmRect(4, 24, 50, 7), "='¥ ' + formatNumber([单价], '0.00')", 9),
                Text(4, layer, "生产日期", new MmRect(4, 33, 50, 7), "=formatDate([生产日期], 'yyyy-MM-dd')", 8),
                Barcode(5, layer, "物料二维码", new MmRect(58, 5, 18, 18), BarcodeSymbology.DataMatrix, "=[物料编码]", false),
            ], fields, samples);
    }

    private static TemplateDocument CreateDocument(
        int sampleNumber,
        string title,
        string description,
        MediaDefinition media,
        DocumentLayout layout,
        Guid layerId,
        IReadOnlyList<TemplateElement> elements,
        IReadOnlyList<FieldDefinition>? fields = null,
        IReadOnlyList<SampleValue>? sampleValues = null) => new()
    {
        Id = StableGuid(sampleNumber, 0),
        Metadata = new DocumentMetadata
        {
            Title = title,
            Description = description,
            CreatedAt = SampleTimestamp,
            ModifiedAt = SampleTimestamp,
        },
        Page = new PageDefinition { Media = media, Layout = layout },
        Layers = [new LayerDefinition(layerId, "内容")],
        Elements = elements,
        Fields = fields ?? Array.Empty<FieldDefinition>(),
        SampleValues = sampleValues ?? Array.Empty<SampleValue>(),
    };

    private static TextElement Text(
        int id,
        Guid layer,
        string name,
        MmRect bounds,
        string content,
        double points,
        bool bold = false,
        TextHorizontalAlignment alignment = TextHorizontalAlignment.Left) => new()
    {
        Id = StableGuid(layer.ToByteArray()[15], id),
        Name = name,
        LayerId = layer,
        Bounds = bounds,
        Content = new ElementExpression(content),
        TextStyle = new TextStyle { FontSizePoints = points, IsBold = bold, HorizontalAlignment = alignment, VerticalAlignment = TextVerticalAlignment.Center },
    };

    private static RectangleElement Rectangle(int id, Guid layer, string name, MmRect bounds, StrokeStyle stroke, FillStyle fill) => new()
    {
        Id = StableGuid(layer.ToByteArray()[15], id), Name = name, LayerId = layer, Bounds = bounds, Stroke = stroke, Fill = fill,
    };

    private static BarcodeElement Barcode(int id, Guid layer, string name, MmRect bounds, BarcodeSymbology symbology, string content, bool showText = true) => new()
    {
        Id = StableGuid(layer.ToByteArray()[15], id), Name = name, LayerId = layer, Bounds = bounds, Symbology = symbology,
        Content = new ElementExpression(content), ShowHumanReadableText = showText, QuietZoneMm = 1,
    };

    private static Guid StableGuid(int group, int item) => Guid.Parse($"10000000-0000-0000-{group:X4}-{item:X12}");

    private static byte[] CreatePreview(TemplateDocument document)
    {
        Dictionary<Guid, ResolvedElement> resolved = document.Elements.ToDictionary(
            static element => element.Id,
            element => new ResolvedElement
            {
                ElementId = element.Id,
                Text = element switch
                {
                    BarcodeElement { Symbology: BarcodeSymbology.Code128 or BarcodeSymbology.Code39 or BarcodeSymbology.Interleaved2Of5 or BarcodeSymbology.Codabar } => "RMPP-001",
                    BarcodeElement => "RMPP sample",
                    SerialElement serial => serial.Definition.Prefix + serial.Definition.Start.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(serial.Definition.MinimumDigits, '0') + serial.Definition.Suffix,
                    TextElement text => ResolvePreviewText(text.Content.Source, document.SampleValues),
                    _ => null,
                },
            });
        RenderSceneBuilder builder = new();
        RenderContext context = new() { ResolvedElements = resolved };
        using SKBitmap bitmap = new SkiaBitmapRenderer().Render(builder.Build(document, context).Pages[0], 120);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string ResolvePreviewText(string source, IReadOnlyList<SampleValue> samples)
    {
        string result = source;
        foreach (SampleValue sample in samples)
        {
            result = result.Replace($"[{sample.FieldName}]", sample.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result.StartsWith('=') ? result[1..].Replace("'", string.Empty, StringComparison.Ordinal) : result;
    }
}
