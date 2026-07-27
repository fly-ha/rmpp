using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;
using Rmpp.Rendering.Scene;

namespace Rmpp.Printing.Windows.Calibration;

/// <summary>把本机校准档案转换为最终物理输出变换，不回写模板或打印计划。</summary>
public static class CalibrationTransform
{
    public static RenderTransform Create(CalibrationProfile? profile, MmSize pageSize)
    {
        if (profile is null)
        {
            return RenderTransform.Identity;
        }

        CalibrationProfile validated = profile.Validate();
        MmPoint center = new(pageSize.Width / 2, pageSize.Height / 2);
        return RenderTransform.Scale(validated.ScaleX, validated.ScaleY)
            .Then(RenderTransform.RotationAt(validated.RotationDegrees, center))
            .Then(RenderTransform.Translation(validated.OffsetMm.X, validated.OffsetMm.Y));
    }
}
