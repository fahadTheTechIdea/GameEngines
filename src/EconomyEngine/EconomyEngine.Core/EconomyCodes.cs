using EngineCommon;

namespace EconomyEngine;

/// <summary>Every code the economy engine refuses with: the document, the calls and the save each have their own. Locale-table refusals keep the <see cref="LocaleCodes"/> of the shared locale machinery.</summary>
public static class EconomyCodes
{
    // Document.
    public const string Format = "ECON_FORMAT";
    public const string UnknownField = "ECON_UNKNOWN_FIELD";
    public const string MissingField = "ECON_MISSING_FIELD";
    public const string Range = "ECON_RANGE";
    public const string Duplicate = "ECON_DUPLICATE";
    public const string Reference = "ECON_REFERENCE";
    public const string Reserved = "ECON_RESERVED_NAME";
    public const string Currency = "ECON_CURRENCY";
    public const string Price = "ECON_PRICE";
    public const string Band = "ECON_BAND";
    public const string Schedule = "ECON_SCHEDULE";
    public const string Policy = "ECON_POLICY";
    public const string Budget = "ECON_BUDGET";
    public const string Route = "ECON_ROUTE";
    public const string Loan = "ECON_LOAN";
    public const string Shock = "ECON_SHOCK";
    public const string Format2 = "ECON_FORMAT_2";
    public const string Format3 = "ECON_FORMAT_3";
    public const string Format4 = "ECON_FORMAT_4";
    public const string Format5 = "ECON_FORMAT_5";
    public const string Recipe = "ECON_RECIPE";
    public const string RecipeCycle = "ECON_RECIPE_CYCLE";
    public const string Line = "ECON_LINE";

    // Calls.
    public const string UnknownMarket = "ECON_UNKNOWN_MARKET";
    public const string UnknownGood = "ECON_UNKNOWN_GOOD";
    public const string UnknownCurrency = "ECON_UNKNOWN_CURRENCY";
    public const string UnknownRoute = "ECON_UNKNOWN_ROUTE";
    public const string UnknownRecipe = "ECON_UNKNOWN_RECIPE";
    public const string NoLine = "ECON_NO_LINE";
    public const string RecipeOpen = "ECON_RECIPE_OPEN";
    public const string UnknownShock = "ECON_UNKNOWN_SHOCK";
    public const string UnknownSchedule = "ECON_UNKNOWN_SCHEDULE";
    public const string UnknownLoan = "ECON_UNKNOWN_LOAN";
    public const string DuplicateLoan = "ECON_DUPLICATE_LOAN";
    public const string NotTraded = "ECON_NOT_TRADED";
    public const string Stock = "ECON_STOCK";
    public const string Funds = "ECON_FUNDS";
    public const string Capacity = "ECON_CAPACITY";
    public const string StepOrder = "ECON_STEP_ORDER";
    public const string FactMissing = "ECON_FACT_MISSING";
    public const string FactInvalid = "ECON_FACT_INVALID";
    public const string UnknownName = "ECON_UNKNOWN_NAME";

    // Save.
    public const string SaveFormat = "ECON_SAVE_FORMAT";
    public const string SaveFormat3 = "ECON_SAVE_FORMAT_3";
    public const string SaveFormat4 = "ECON_SAVE_FORMAT_4";
    public const string SaveFormat5 = "ECON_SAVE_FORMAT_5";
    public const string SaveDigest = "ECON_SAVE_DIGEST";
    public const string SaveDocument = "ECON_SAVE_DOCUMENT";
    public const string SaveState = "ECON_SAVE_STATE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
