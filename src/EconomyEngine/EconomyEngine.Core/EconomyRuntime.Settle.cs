namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    /// <summary>
    /// Settles one schedule, in a fixed order: for each market in the document, the taxes its policies accrued are remitted to the treasury, then its wage and upkeep lines are paid (what the treasury cannot pay stays owed and
    /// is paid first next time); then each loan on the schedule is serviced. Money moves only between treasuries and the world account of the currency.
    /// </summary>
    private void SettleSchedule(int schedule, EconomyChanges changes)
    {
        foreach (var market in Set.Markets)
        {
            var state = Markets[market.Index];
            var currency = Currencies[market.Currency];
            for (var slot = 0; slot < market.Policies.Count; slot++)
            {
                var policy = Set.Policies[market.Policies[slot]];
                if (policy.Schedule != schedule || state.Accrued[slot] == 0) continue;
                var amount = state.Accrued[slot];
                state.Accrued[slot] = 0;
                state.Treasury += amount;
                currency.World -= amount;
                changes.Settlements.Add(new Settled(market.Index, SettleKind.Tax, policy.Id, amount, amount));
            }
            foreach (var line in market.Budget)
            {
                if (line.Schedule != schedule) continue;
                var amount = checked(line.Amount + line.PerHead * market.Staff);
                if (line.Indexed) amount = EconomyMath.Nearest(amount, currency.Level, EconomyMath.Ppm);
                var owed = amount + state.Arrears[line.Index];
                var paid = Math.Min(owed, state.Treasury);
                state.Treasury -= paid;
                currency.World += paid;
                state.Arrears[line.Index] = owed - paid;
                changes.Settlements.Add(new Settled(market.Index, line.Kind == BudgetKind.Wage ? SettleKind.Wage : SettleKind.Upkeep, line.Id, paid, owed));
            }
        }
        foreach (var loan in _loans)
            if (loan.Status == LoanStatus.Active && loan.Schedule == schedule) Service(loan, changes);
    }

    /// <summary>
    /// One payment of a loan: the interest on the balance (rounded to the nearest minor unit) plus the instalment of principal (the annuity payment less the interest; all of the balance at the last period; none for a
    /// bullet until then). A payment is made whole or not at all: when the borrower's treasury cannot pay it, the interest is added to the balance and the miss is counted, and after the loan's limit of misses in a row
    /// it is in default and is not serviced again.
    /// </summary>
    private void Service(LoanState loan, EconomyChanges changes)
    {
        var borrower = Set.Markets[loan.Borrower];
        var treasury = Markets[loan.Borrower];
        var currency = Currencies[borrower.Currency];
        var interest = EconomyMath.Nearest(loan.Balance, loan.RatePpm, EconomyMath.Ppm);
        var last = loan.Remaining == 1;
        var principal = last ? loan.Balance : loan.Kind == LoanKind.Bullet ? 0 : Math.Min(loan.Balance, Math.Max(0, loan.Payment - interest));
        var due = interest + principal;
        if (treasury.Treasury >= due)
        {
            treasury.Treasury -= due;
            if (loan.Lender >= 0) Markets[loan.Lender].Treasury += due;
            else currency.World += due;
            loan.Balance -= principal;
            loan.Remaining--;
            loan.Missed = 0;
            changes.Settlements.Add(new Settled(loan.Borrower, SettleKind.Loan, loan.Id, due, due));
            if (loan.Remaining == 0 || loan.Balance == 0)
            {
                loan.Status = LoanStatus.Closed;
                changes.Loans.Add(new LoanChanged(loan.Id, LoanOutcome.Closed, loan.Balance));
            }
            return;
        }
        loan.Balance += interest;
        loan.Missed++;
        changes.Settlements.Add(new Settled(loan.Borrower, SettleKind.Loan, loan.Id, 0, due));
        changes.Loans.Add(new LoanChanged(loan.Id, LoanOutcome.Missed, loan.Balance));
        if (loan.Missed < loan.DefaultAfter) return;
        loan.Status = LoanStatus.Defaulted;
        changes.Loans.Add(new LoanChanged(loan.Id, LoanOutcome.Defaulted, loan.Balance));
    }
}
