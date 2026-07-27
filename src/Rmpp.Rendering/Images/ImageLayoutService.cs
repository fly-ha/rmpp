using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;

namespace Rmpp.Rendering.Images;

/// <summary>计算 contain、cover、stretch 和原始尺寸图片布局，不隐式改变页面比例。</summary>
public static class ImageLayoutService
{
    public static ImageLayoutResult Layout(
        int pixelWidth,
        int pixelHeight,
        MmRect target,
        ImageFitMode fitMode,
        MmRect? crop = null,
        double sourceDpi = 96)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Image dimensions must be positive.");
        }

        if (target.Width <= 0 || target.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(target), "Target bounds must be positive.");
        }

        if (!double.IsFinite(sourceDpi) || sourceDpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceDpi));
        }

        MmRect source = crop ?? new MmRect(0, 0, pixelWidth, pixelHeight);
        if (source.Width <= 0 || source.Height <= 0 || source.X < 0 || source.Y < 0
            || source.Right > pixelWidth || source.Bottom > pixelHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(crop), "Crop rectangle must be inside the source image.");
        }

        return fitMode switch
        {
            ImageFitMode.Stretch => new ImageLayoutResult(source, target),
            ImageFitMode.Contain => Contain(source, target),
            ImageFitMode.Cover => Cover(source, target),
            ImageFitMode.OriginalSize => OriginalSize(source, target, sourceDpi),
            _ => throw new ArgumentOutOfRangeException(nameof(fitMode)),
        };
    }

    private static ImageLayoutResult Contain(MmRect source, MmRect target)
    {
        double scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
        double width = source.Width * scale;
        double height = source.Height * scale;
        return new ImageLayoutResult(source, new MmRect(
            target.X + (target.Width - width) / 2,
            target.Y + (target.Height - height) / 2,
            width,
            height));
    }

    private static ImageLayoutResult Cover(MmRect source, MmRect target)
    {
        double targetRatio = target.Width / target.Height;
        double sourceRatio = source.Width / source.Height;
        MmRect cropped = sourceRatio > targetRatio
            ? new MmRect(source.X + (source.Width - source.Height * targetRatio) / 2, source.Y, source.Height * targetRatio, source.Height)
            : new MmRect(source.X, source.Y + (source.Height - source.Width / targetRatio) / 2, source.Width, source.Width / targetRatio);
        return new ImageLayoutResult(cropped, target);
    }

    private static ImageLayoutResult OriginalSize(MmRect source, MmRect target, double sourceDpi)
    {
        double width = source.Width * 25.4 / sourceDpi;
        double height = source.Height * 25.4 / sourceDpi;
        return new ImageLayoutResult(source, new MmRect(target.X, target.Y, width, height));
    }
}
