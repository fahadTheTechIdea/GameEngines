namespace EngineCommon;

/// <summary>What a message with forms chooses on: how many (CLDR cardinal), which place (CLDR ordinal), or the exact value of a text argument such as a gender (ICU <c>select</c>).</summary>
public enum MessageSelect { Cardinal, Ordinal, Value }