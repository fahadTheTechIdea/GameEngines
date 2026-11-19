namespace AIEngine;

/// <summary>How a select node chooses among the matches: the lowest or highest value of a number attribute, or the one at a given place in id order.</summary>
public enum PickKind { Min, Max, Nth }