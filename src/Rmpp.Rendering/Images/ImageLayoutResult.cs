using Rmpp.Domain.Geometry;

namespace Rmpp.Rendering.Images;

/// <summary>保存从源像素矩形到目标毫米矩形的确定性图片布局。</summary>
public sealed record ImageLayoutResult(MmRect SourcePixels, MmRect DestinationMm);
