namespace Comic.Core.Selection;

public static class ReaderDisplayWidthPolicy
{
    public const double Minimum = 480;
    public const double Default = 760;
    public const double Maximum = 1200;
    public const double Step = 80;

    public static double Clamp(double width) =>
        double.IsFinite(width)
            ? Math.Clamp(width, Minimum, Maximum)
            : Default;

    public static double Decrease(double width) =>
        Math.Max(Minimum, Clamp(width) - Step);

    public static double Increase(double width) =>
        Math.Min(Maximum, Clamp(width) + Step);
}
