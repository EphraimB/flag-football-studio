using System;

namespace FlagFootballStudio.Domain;

public sealed class HairAppearance
{
    public const float MinimumLength = 0.5f;
    public const float MaximumLength = 1.5f;
    public const float MinimumVolume = 0.7f;
    public const float MaximumVolume = 1.4f;
    public const float MinimumHairlineHeight = -0.15f;
    public const float MaximumHairlineHeight = 0.15f;
    public const float MinimumPartPosition = -1f;
    public const float MaximumPartPosition = 1f;
    public const float MinimumCurlAmount = 0f;
    public const float MaximumCurlAmount = 1f;
    public const float MinimumPonytailLength = 0.5f;
    public const float MaximumPonytailLength = 1.5f;
    public const float MinimumPonytailVolume = 0.7f;
    public const float MaximumPonytailVolume = 1.4f;
    public const float MinimumBunSize = 0.7f;
    public const float MaximumBunSize = 1.4f;

    public HairAppearance()
    {
        SetParameters(HairStyle.Short, 0.8f, 1, 0, 0.15f, 0.15f, 0.9f, 1, 1);
        Color = new AppearanceColor(48, 31, 24);
    }

    public HairStyle Style { get; private set; }
    public float Length { get; private set; }
    public float Volume { get; private set; }
    public float HairlineHeight { get; private set; }
    public float PartPosition { get; private set; }
    public float CurlAmount { get; private set; }
    public AppearanceColor Color { get; private set; }
    public float PonytailLength { get; private set; }
    public float PonytailVolume { get; private set; }
    public float BunSize { get; private set; }

    public void SetStyleAndColor(HairStyle style, AppearanceColor color)
    {
        ValidateStyle(style);
        Style = style;
        Color = color;
    }

    public void SetParameters(
        HairStyle style,
        float length,
        float volume,
        float hairlineHeight,
        float partPosition,
        float curlAmount,
        float ponytailLength,
        float ponytailVolume,
        float bunSize)
    {
        ValidateStyle(style);
        Style = style;
        Length = Math.Clamp(length, MinimumLength, MaximumLength);
        Volume = Math.Clamp(volume, MinimumVolume, MaximumVolume);
        HairlineHeight = Math.Clamp(hairlineHeight, MinimumHairlineHeight, MaximumHairlineHeight);
        PartPosition = Math.Clamp(partPosition, MinimumPartPosition, MaximumPartPosition);
        CurlAmount = Math.Clamp(curlAmount, MinimumCurlAmount, MaximumCurlAmount);
        PonytailLength = Math.Clamp(ponytailLength, MinimumPonytailLength, MaximumPonytailLength);
        PonytailVolume = Math.Clamp(ponytailVolume, MinimumPonytailVolume, MaximumPonytailVolume);
        BunSize = Math.Clamp(bunSize, MinimumBunSize, MaximumBunSize);
    }

    public void SetColor(AppearanceColor color) => Color = color;

    private static void ValidateStyle(HairStyle style)
    {
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
    }
}
