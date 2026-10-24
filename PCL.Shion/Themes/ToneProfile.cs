namespace PCL.Shion.Themes;

public record ToneProfile(
    double L1 = 0.35,
    double L2 = 0.5,
    double L3 = 0.575,
    double L4 = 0.65,
    double L5 = 0.8,
    double L6 = 0.92,
    double L7 = 0.94,
    double L8 = 0.96,
    double LWhite = 1,
    double LForeground = 0,
    double LBackground = 0.995,
    double C1 = 0.025,
    double C2 = 0.188,
    double C3 = 0.213,
    double C4 = 0.168,
    double C5 = 0.093,
    double C6 = 0.036,
    double C7 = 0.028,
    double C8 = 0.018,
    double ASemiWhite = 0.733,
    double AHalfWhite = 0.333,
    double ASemiTransparent = 0.004,
    double AHalfTransparent = 0.5,
    double ATransparent = 0,
    double ABackground = 0.824,
    double AToolTip = 0.9
);

public record ToneProfileConfig
{
    public static readonly ToneProfile DefaultLight = new();

    public static readonly ToneProfile DefaultDark = new(
        L1: 0.96, L2: 0.75, L3: 0.6, L4: 0.65,
        L5: 0.45, L6: 0.25, L7: 0.225, L8: 0.2,
        LBackground: 0.3, LForeground: 1, LWhite: 0.275
    );

    public ToneProfile Light { get => field ?? DefaultLight; init; } = null!;

    public ToneProfile Dark { get => field ?? DefaultDark; init; } = null!;
}
