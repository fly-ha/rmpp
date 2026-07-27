namespace Rmpp.Domain.Documents;

public enum GuideOrientation { Horizontal, Vertical }

public sealed record GuideDefinition(Guid Id, GuideOrientation Orientation, double PositionMm, bool IsLocked = false);
