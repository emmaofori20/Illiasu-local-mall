namespace ILLiasu_Local_Mall.Domain;

// Shared by the staff form service and the tracked persistence validation boundary.
public static class StockConsumptionRules
{
    public static string? Validate(decimal unitsPerSale, decimal soldQuantityStep, int baseDecimalPlaces, bool baseAllowsFractions)
    {
        const decimal maximum = 999999999999.999999m;
        if (unitsPerSale <= 0 || unitsPerSale > maximum || decimal.Round(unitsPerSale, 6) != unitsPerSale)
            return "Enter a positive stock quantity with at most six decimal places and no more than 999999999999.999999.";
        if (baseDecimalPlaces is < 0 or > 6 || decimal.Round(unitsPerSale, baseDecimalPlaces) != unitsPerSale ||
            !baseAllowsFractions && decimal.Truncate(unitsPerSale) != unitsPerSale)
            return "The stock quantity must match the source option's unit precision. Whole items cannot be split into fractions.";
        try
        {
            var quantity = checked(soldQuantityStep * unitsPerSale);
            if (quantity <= 0 || quantity > maximum || decimal.Round(quantity, baseDecimalPlaces) != quantity ||
                !baseAllowsFractions && decimal.Truncate(quantity) != quantity)
                return "The selling increment must remove a valid quantity in the source option's stock unit.";
        }
        catch (OverflowException) { return "The resulting stock quantity is too large."; }
        return null;
    }
}
