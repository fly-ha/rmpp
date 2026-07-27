using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Layout;

public readonly record struct LabelCell(int Index, int Row, int Column, MmRect Bounds);
