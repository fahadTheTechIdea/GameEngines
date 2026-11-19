using System.Globalization;

namespace GameRuntime.Combat.Headless;

/// <summary>One thing the commander asks of the combat component. Each kind is its own class; the input argument is its <see cref="Key"/>.</summary>
public abstract record SkirmishOrder
{
    public abstract string Action { get; }

    /// <summary>The input argument, also the identity of the order.</summary>
    public abstract string Key { get; }

    /// <summary>Raises this order as its request event and records what the commander now assumes (the distance it ordered) in the view.</summary>
    public abstract void Request(IEventPort events, string requestId, SkirmishView view);

    public static SkirmishOrder Parse(string action, string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return action switch
        {
            SkirmishActions.Engage => EngageOrder.Parse(argument),
            SkirmishActions.March => MarchOrder.Parse(argument),
            SkirmishActions.Round => RoundOrderSet.Parse(argument),
            SkirmishActions.Resupply => ResupplyOrder.Parse(argument),
            // An action the commander does not know is a usage error, named; there is no neutral reading of it.
            _ => throw new SkirmishUsageException("the commander does not know the action '" + action + "'; it knows " + string.Join(", ", SkirmishActions.All)),
        };
    }

    /// <summary>A whole non-negative number, or a usage error naming what it was for.</summary>
    internal static int Whole(string text, string what, string argument) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new SkirmishUsageException(what + " must be a whole number but got '" + text + "' in '" + argument + "'");
}
