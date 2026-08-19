namespace Application.ChipSets;

public sealed record ChipDenominationModel(
    int FaceValue,
    int EffectiveValue,
    int Quantity,
    string? Colour);
