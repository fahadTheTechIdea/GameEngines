# EconomyEngine: research, boundary, document, criteria and result

*Pass 41 (2026-10-29) changed how routes load and the document format (format 2): see sections 5.5, 6 and 11. Pass 46 (2026-10-29) added production chains and the document format 3: see sections 6 and 13. Sections 1 to 10 are as written in pass 36 except where they say otherwise.*

Pass 36, 2026-10-29. The third of six planned engines (Calendar in pass 34, Relationship in pass 35; Economy, Tutorial/Onboarding, WorldEvents, Diplomacy follow). Written before the code; section 10 records what was built and measured.

## 1. What already owns this (evidence, and the decision)

Read in the source on 2026-10-29:

| Owner | What it holds | Decision |
|---|---|---|
| `EngineCommon` | strict JSON reading, the one condition grammar (ScenarioEngine's), digest, stateless seeded roll (`DeterministicRoll`), the locale machinery (tables, chains, CLDR plurals, coverage) | **Reused.** Documents are strict JSON; seasonal modifiers use the shared condition grammar over host facts; the demand noise is `DeterministicRoll`; words are locale keys |
| `GameRuntime.Channels` | the one `ChannelRegistry` and `ChannelProtocol`, the journal text escapes and the event transcript | **Reused.** `EconomyChannels` is a sealed subclass (rank 3); the journal uses `JournalText`; the transcript is `EventTranscript` |
| `CalendarEngine` | the one coordinate of time, days, seasons, holidays, facts `cal.*` | **Reused through its contract** (`docs/CALENDAR_ENGINE.md` section 5): the economy keeps no clock; the game layer converts the calendar instant to a step; a manual schedule takes its date (a pay day, a tax decree) from the calendar's day and holiday events |
| `DialogueEngine` | conversations, external facts (`ext.*`), signals and quest outcomes | Reads market facts as external facts; writes back by raising an economy request (deferred). The trigger vocabulary (`signal:`, `quest:`, the `{0}` arguments) was private to the relationship bridge; it is now `DialogueTriggers` in `GameRuntime.Dialogue`, used by both bridges |
| `AIEngine` | percepts, utility, behaviour trees; knowledge is not truth | A merchant agent is told prices and shortages as a percept by a bridge; it decides by what it was last told |
| SimEngine (`ResourceInventory`, the stock ledger `ResourceFlowFact`, the director effect `stock.flows`; the optional tycoon `IEconomy` with per-company cash) | **Holders' goods and cash as inventory resources and their lifecycle flows**; tycoon cash is a separate optional module | **Not duplicated.** The economy engine never holds a trader's money or goods. A trade it made is written to the ledger by the bridge **through the existing `StockFlowEffect`** (the one writer of that ledger), as `Issued` and `Consumed` flows; a source guard fails if the bridge touches the inventory's transaction interface. Tycoon `IEconomy` cash is not bridged (section 10.7) |
| nobody | goods and markets, supply and demand price formation with bounded elasticity and inertia, trade routes with delay, taxes, tariffs and subsidies, currencies with rates, inflation, loans, wages and upkeep, shocks, price history | **The economy engine** |

The engine is a pure library over a step counter. It has no clock, no files, no world and no float. The game layer's clock stays the only clock.

## 2. Research (searched 2026-10-29)

P means the page itself was fetched and a question answered on it (the fetch tool summarises the page with a small model, so a number is quoted from that answer, not read by me line by line); S means a search summary or snippet only; N means not reached.

- **Victoria 3 wiki, "Goods".** P. Each good has a base price, "the cost of this good when buy and sell orders are equal"; a market's price "varies ... depending on relative supply (sales) and demand (purchases)"; it can be at most 75 percent above or below base. The page does not say how often prices change.
- **Europa Universalis IV wiki, "Trade".** P, first 100,000 of 162,930 characters, last verified for 1.25 by the page. Goods value pools monthly in trade nodes; value moves along fixed one-way links that "cannot be altered during the course of play"; only relative trade power matters; the merchant bonus on a link stops growing after five merchants (about 11.3 percent).
- **RimWorld wiki, "Trade".** P, last 15,032 of 115,032 characters not read. Goods sell at 60 percent of market value and are bought from traders at 140 percent; a negotiator's skill and a faction discount (2 percent) change it; traders have limited silver. The page gives no minimum or maximum price.
- **Offworld Trading Company.** S (reviews and forums only). Described as supply and demand in which buying raises and selling lowers prices; no official document about price formation was found.
- **Anno 1800.** S. A trading post can be set to buy, sell or "balanced" (alternate to hold a target stock); trade routes move chosen goods between places; a community rule of thumb is demand times travel time. No price formula found; no Patrician source found.
- **Walrasian auction / tâtonnement (Wikipedia).** P. "The rate of change for any commodity's price is proportional to the excess demand"; no trade happens until equilibrium; the Sonnenschein-Mantel-Debreu result means the process does not reliably converge.
- **Supply and demand (Wikipedia).** P, first 100,000 of 131,232 characters. Marshall popularised the curves in 1890; "prices adjust until supply equals demand". The page does not define elasticity or Marshall's own adjustment rule.
- **Price elasticity of supply (Wikipedia).** P. Below 1 is inelastic (output changes by a smaller percentage than price), above 1 elastic.
- **Quantity theory of money (Wikipedia).** P. MV = PQ is an identity; with velocity constant and real output exogenous, steady money growth gives the same steady inflation.
- **Purchasing power parity (Wikipedia).** P. Relative PPP "covering changes in the exchange rate and inflation rates"; the page gives no formula or speed of convergence, and says gaps persist (tariffs, transaction costs, Balassa-Samuelson). The form used here (the rate follows the ratio of price levels) is the standard textbook one, which this page does not state.
- **Day count convention (Wikipedia).** P. 30/360, Actual/365 and Actual/360 are the three conventions it covers; it does not cover compounding.
- **Amortization calculator (Wikipedia).** P. The fixed payment A = P i / (1 - (1+i)^-n), valid for i > 0 (A = P/n for i = 0).
- **Not reached:** Stardew Valley shops (HTTP 403), Dwarf Fortress trade (HTTP 429), Capitalism (not searched), Patrician (nothing found), Marshall's own text, any primary Paradox, Ubisoft or Mohawk document, the ISDA definitions of day counts.

## 3. Adopt, adapt, avoid

| Source idea | Decision |
|---|---|
| Victoria 3: a base price at balance, price bounded around it (75 percent) | **Adopt** a base price per good and bounded prices; **adapt**: the bounds are the document's per good (`floor` and `ceiling`, per mille of the reference price), not a fixed 75 percent. **Avoid** the unstated update rule: the cadence is the document's `periodSteps` |
| Walras: price change proportional to excess demand | **Adopt** as the one price rule. **Avoid** claiming it converges (the page says it does not reliably): every period's change is capped (`maxStep`), blended with the last change (`inertia`) and clamped to the floor and ceiling, and the tests show the price stays inside them over thousands of random periods; no stability proof is claimed |
| Elasticity (supply above 1 is elastic) | **Adopt** a linear response of supply and demand to the price over its reference, `1000 + elasticity x (ratio - 1000) / 1000`, **bounded** by the document's `responseMin` and `responseMax`, so no price can drive a flow to a negative or unbounded value |
| Anno's "balanced" setting holds a target stock | **Adapt**: each pair has a `cover` (periods of use it keeps in stock); the gap to that stock, spread over `settlePeriods`, adds to the excess demand |
| EU4: value flows on fixed one-way links with limits | **Adapt**: routes are fixed, one-way, with a delay and a capacity; goods leave the origin when loaded and arrive after the delay. **Not adopted**: trade power shares (a diplomacy matter) |
| RimWorld: sell at 60 percent, buy at 140 percent of value | **Adapt**: a market sells at its price plus a `spread` and buys at its price less the spread (one number per market, symmetric). **Not adopted**: traders with limited silver (a player trade does not spend the market's treasury; said in 10.7) |
| Offworld Trading Company: buying raises and selling lowers prices | **Adopt**: units a trader buys or sells now count as demand or supply in the next period's price, in addition to the stock they move |
| Quantity theory: inflation follows money growth | **Adopt in growth form** with its assumptions stated: each currency has a growth in parts per million per period; the price level and the money supply (minted into the world account) grow by it; velocity and real output are not modelled |
| Relative PPP: rates follow price levels | **Adapt**: the live rate moves toward `anchor x reference level / this level` by at most `maxRateStepPpm` a period (zero is a peg). The speed is the document's; the sources give none |
| Loans: annuity formula, compounding per period | **Adopt** exact big-integer annuity rounded to the nearest minor unit, interest on the balance each period. **Avoid** day-count conventions: the unit is the period, and converting an annual nominal rate is the author's |
| Hard-coded goods, taxes and currencies | **Avoid.** Every name is document data; the engine has none of its own |
| Floats for price and money | **Avoid.** Money is minor units, a price is thousandths of a minor unit, rates are parts per million, ratios per mille; products are formed in 128 bits and every division says how it rounds |
| One market-wide global price | **Avoid.** Every market has its own price; routes and merchants are what link them |

## 4. Layer boundary

- `EconomyEngine.Core` (pure library): document format 1, strict loader, `EconomySet`, `EconomyRuntime` (currencies, markets, pairs, routes, shipments, policies, budgets, loans, shocks, history), facts, explanations, save/open, locale keys and coverage. No clock, no files, no Godot, no world.
- `GameRuntime.Economy` (game layer): `EconomyComponent` with Init and Update only, requests as events, answers in `Update`, its channel registry (`ChannelRegistry`, rank 3), deferred events with a bounded queue, checkpoint, journal and replay.
- `GameRuntime.Economy.Bridges`: calendar (clock, document check, dates for manual schedules), dialogue (facts and outcomes), AI (market percepts), SimEngine ledger (trades as inventory flows).
- `GameRuntime.Economy.Headless`: the town demo. `EconomyEngine.Cli`: check, simulate, explain, coverage.

## 5. The model

### 5.1 Time

The document says `stepsPerDay` (1 to 288) and `periodSteps` (1 to 100,000). A **step** is the smallest time the engine moves in; a **period** is `periodSteps` steps; prices, flows, shipments and settlements move once per period. `Advance(step, hostFacts)` closes each period the step crosses, at most 64 in one call (the rest is owed: `Owed(target)`); inside a period it only moves the step. The game layer converts a calendar instant to a step with `step = (instant - origin) / (ticks per day / steps per day)` (exact, floor).

### 5.2 Money, prices and units

Money is a whole number of minor units of a currency. A price is in thousandths of a minor unit ("milli") of the market's currency. A rate is parts per million of the reference currency. Ratios, elasticities, speeds, tax rates and bounds are per mille. The **reference price** of a good in a currency is `base x level / anchor` (never below 1): its base price in the reference currency, at the currency's price level (1,000,000 at the start), in the currency's own units.

### 5.3 One period of one pair (the price rule)

For each good a market trades, in document order (markets in document order, pairs in the market's order):

1. The price follows the price level: `price = price x level / level before` (money neutrality).
2. Stock spoils by the good's `decay`.
3. The **consumer price** is the price plus the sales tax; the **producer price** is the price plus the subsidy paid (in proportion if the treasury cannot pay it all). Their ratios to the reference give the responses: `supply response = clamp(1000 + supplyElasticity x (producer ratio - 1000) / 1000)`, `demand response = clamp(1000 - demandElasticity x (consumer ratio - 1000) / 1000)`, both between `responseMin` and `responseMax`.
4. `supply = production x supply response x modifiers x shocks x (1 - arrears penalty)`; `demand = consumption x demand response x modifiers x shocks x (1 + noise)`. Noise is the seeded roll for (period, pair) minus its bound; zero when the document says zero.
5. Demand is met from the stock after spoilage plus the supply (`met`); what is left stays, up to the warehouse's `capacity` (the rest is lost). A pair is **short** when demand was not met.
6. Sales tax accrues on the value met; subsidies are paid from the treasury.
7. **Imbalance** (Walras's excess demand over the traded volume, per mille, between -1000 and 1000): `(demand + spot demand - supply - spot supply + (cover x consumption - stock) / settlePeriods) x 1000 / ((demand + supply + spot) / 2)`.
8. **Change** = `speed x imbalance` blended with the last change by `inertia`, clamped to `+-maxStep`; the new price is clamped to the floor and ceiling of the reference.
9. The **band** (named ranges of the price ratio) moves up at once and down only when the ratio is under the band's start by the margin; the price goes into the history ring.

Trades made by traders between periods change the stock at once (`Trade`) and count as spot demand or supply in step 7.

### 5.4 Currencies, inflation and rates

Each period each currency's price level grows by its **growth** (its own plus its active shocks'), and the money supply grows by the same fraction, minted into the world account. Then each rate moves toward `anchor x reference level / level` by at most `maxRateStepPpm` of itself (zero: a peg). The reference currency's rate never moves.

### 5.5 Routes and shipments

A route is one-way from a market to a market, with a delay, a capacity per period (across all goods), a freight per unit of weight, a margin and a share. *Format 2 (pass 41, section 11) changed how the loading rule reads the destination and who gets the capacity; the paragraph below is the format 1 rule, kept because the arithmetic of a single hop is unchanged.* For each good both ends trade merchants compare the destination price converted into the origin's money (through the two rates) less the origin price, the freight and the export and import tariffs. If the gain is at least the margin, they move `min(share, gain ratio)` of the surplus over one period's use, up to the capacity left. Goods leave the origin stock now and join the destination's after the delay, paying the import tariff on arrival. At most `maxShipments` are on their way; a route that cannot load says so (`RouteBlocked`).

**Format 2.** (a) The capacity is shared by gain, not by the order of the document: the goods are ranked by the gain a unit makes (the value at the destination less the origin price and the costs of the way, in the origin's money), then by gain ratio, then by good id, and filled in that order. (b) With `routing.mode` `landed`, the "price at the destination" is the best value a unit can fetch from there by selling it or sending it on through more routes, less everything paid on the way (section 11.2); with `direct` it is the destination's own price, as in format 1. (c) Routes load in the order of their ids, so two routes that leave one market share its stock the same way whatever order the document lists them in.

### 5.6 Taxes, tariffs, subsidies, wages, upkeep, loans

- A **sales**, **import** or **export** policy accrues its rate on the value sold, arriving or leaving (policies can name goods by tag) and is remitted to the treasury on its schedule. A **subsidy** pays an amount per unit made, each period.
- A **schedule** fires every N periods from an offset, or only when asked (`manual`: a pay day on the calendar's date, a decree). Settling a schedule remits its taxes, pays its wage and upkeep lines (amount plus an amount per head, optionally following the price level), and services its loans, market by market in document order.
- A **budget line** the treasury cannot pay in full stays owed and is paid first next time; while any line is owed the market loses `arrearsPenalty` of its output.
- A **loan** (from the world or a market treasury, in one currency) pays interest on the balance each time (rounded to the nearest minor unit) and an instalment of principal (the annuity payment less the interest; the whole balance at the last; none for a bullet loan until then). A payment is made whole or not at all: a miss adds the interest to the balance, and `defaultAfter` misses in a row is a default.
- **Money moves only between treasuries and the world account of its currency.** Per currency, treasuries plus the world account always equal what the document started with plus what was minted (`Conservation()`), and a save that breaks it is refused.

### 5.7 Shocks and modifiers

A **shock** (drought, devaluation, decree, blockade, money printing) is a document entry with a duration and effects: supply, demand, price (an immediate jump), rate (an immediate jump of a currency), growth (added to money growth), route (scales a capacity). `ApplyShock(shock, market, scale)` starts one on a market or every market; its effects act from the next period for its duration. This is the hook a later event system calls (the request `EconomyShockRequested`). A **modifier** multiplies the supply or demand of goods while a condition over host facts holds (winter raises the demand for food); facts missing or of the wrong type refuse the advance before anything moves.

### 5.8 Bounds

The document states the most shipments, loans, shocks and the length of the price history; counts of currencies, goods, markets, routes and pairs, and every quantity, price, rate and amount, have ceilings in the reader. A request that would pass one is refused whole.

### 5.9 Facts

For a market the engine offers `market.treasury`, `market.staff`, `market.currency`, `market.arrears` and, per good, `market.price.<good>`, `market.ratio.<good>`, `market.stock.<good>`, `market.band.<good>`, `market.short.<good>`; per currency `currency.<id>.rate` and `currency.<id>.level`. Whole numbers saturate at one billion because the shared fact model of the dialogue and AI engines carries no more. The declarations are checked against a dialogue document when the game is assembled.

## 6. Document format 6 (summary; formats 1, 2, 3, 4 and 5 are refused)

One strict JSON object, every field present: `format, id, seed, time, limits, price, routing, recipes, currencies, goods, bands, schedules, policies, modifiers, shocks, markets (each with pairs, policies, budget and lines), routes, loans, facts`. An unknown, missing or repeated field, a number out of range, a bad id, a reference that does not resolve, a bad condition, a second or no reference currency, unordered bands and every inconsistent policy, budget, route, loan or shock are refused with a distinct code and a message naming the place. The long form is in `src/EconomyEngine/samples/town.economy.json`, which the tests load.

Format 2 adds two fields to format 1. `routing` is an object with one field, `mode`: `"direct"` or `"landed"` (neither is a default; a document must say). `recipes` was reserved and empty in format 2 (a listed recipe was refused with `ECON_RECIPES`); in format 3 it holds production recipes (section 13) and `ECON_RECIPES` is retired. A format 1 document, or any other format, is refused with `ECON_FORMAT` before anything else about it is read ("format 1 is no longer read"); a save of format 1 is refused with `ECON_SAVE_FORMAT`. There is no migration: the format 1 sample was edited by hand into format 2.

Format 4 (pass 49, section 15) changes what a shock does to a recipe's output and adds two optional fields: recipe `requires` (goods a line must hold but never uses up) and line `throttle` (`{stop, full}`, the margin throttle). A format 3 document is refused with `ECON_FORMAT_3`, a format 3 save with `ECON_SAVE_FORMAT_3`; neither is migrated. The save is format 4 too: batches are grouped by line and a pair is ten numbers.

Format 5 (pass 51, section 16) adds two optional declarations: the `wear` of a `requires` entry (`{units, runs}`: units lost for every `runs` runs of the line) and the `costs` of a `throttle` (`{wage, overhead, indexed}`: the line's own cost of a run, which makes its margin a profit margin). A format 4 document is refused with `ECON_FORMAT_4`, a format 4 save with `ECON_SAVE_FORMAT_4`; neither is migrated. The save is format 5 too: each line saves the runs it has done towards the next loss of each required good, and `LineLimit` gains `Worn`.

Format 6 (pass 57, section 17) adds no field. It changes what `requires` means: a required good (a tool or a catalyst) is held by one line at a time within a period, so a document whose lines share one would run differently. A format 5 document is refused with `ECON_FORMAT_5`, a format 5 save with `ECON_SAVE_FORMAT_5`; neither is migrated. The save is format 6 too (its shape is unchanged; the digest text is `economy-state-6`).

## 7. Localisation

A document holds keys only. `EconomyNames.RequiredKeys` lists every key (currencies, goods, bands, schedules, shocks, markets); `Coverage` reports missing keys, messages no document uses and placeholders a key does not give, through the shared `CoverageRules`; rendering uses the shared `LocaleChain`.

## 8. Acceptance criteria (fixed before coding)

| # | Criterion | Check |
|---|---|---|
| E1 | Strict loader: every document refusal code is reached by one input; a declared code that no case reaches fails | Refusal matrix over `EconomyCodes` by reflection |
| E2 | Arithmetic: rounding of `Trunc` and `Nearest` including negatives and ties; the annuity equals the exact payment rounded; a loan paid by the annuity ends at zero | Tests against big-integer computations |
| E3 | Price rule: one period of any pair equals an independent computation (200 random pairs, three periods each); equilibrium does not move; excess demand raises and excess supply lowers; the price never leaves floor and ceiling and never moves more than `maxStep`; inertia carries; responses are bounded; noise is the seeded roll | Oracle and property tests |
| E4 | Tax, subsidy, wages, upkeep: accrual, remittance on schedule, partial payment and arrears, the output penalty, indexation | Tests |
| E5 | Loans: annuity and bullet payments equal an independent amortisation, a miss capitalises interest, default stops service, a loan from a market moves money between treasuries | Tests |
| E6 | Currencies: growth, minted money, the quantity-theory relation, rates following parity by at most their step, pegs, devaluation and recovery, the reference never moves | Tests |
| E7 | Routes: loading rule equals a recomputation, capacity, delay, tariffs, currency conversion, shipment limit, blockade | Tests |
| E8 | Shocks and modifiers: start, scale, duration, stacking, market scope, goods by id or tag, facts missing or wrong refused before anything moves | Tests |
| E9 | Conservation and bounds hold after every call of 300 random sequences of 80 calls, with a save and open every 20 | Random-sequence invariant test |
| E10 | Save: digest changes with every kind of change; ranges are checked before the digest; the digest; then conservation, bounds and bands; 55 forgeries with recomputed digests are refused as state | Tamper tests |
| E11 | No clock, no float, no random source, no file access in the core and the game layer; no silent catch; no throwing catch-all arm; nothing thrown from a coalesce; the bridge never touches the inventory's transaction interface | Roslyn guards, each shown to fail |
| E12 | Component: requests only queue and are answered in Update in arrival order; an unserved channel is refused in the same tick; events are deferred, bounded per Update and owed, none lost or reordered; time waits while too many are owed; a checkpoint is refused while anything waits | Tests |
| E13 | Replay: a journal replayed from a checkpoint, from the start, or from other quiet ticks ends in the same digest and the same events; a missing request or a changed fact is detected | Tests; the demo does it on every run |
| E14 | Integrations, one proof each: calendar (clock, document check, a decree on the 15th, a pay day on Fridays, a holiday), dialogue (a shopkeeper's line by price band, a spoken trade written back one tick later), AI (a merchant sells by the price it was told), SimEngine ledger (a trade appears as inventory flows and the ledger agrees with the economy to the minor unit) | Tests |
| E15 | Cost: 200 markets x 100 goods (20,000 pairs, 400 routes), with a budget set from measurements and repeats | `ScaleTests`, `docs/measurements` |
| E16 | Cli: check, simulate, explain, coverage; every mistake has an exit code | Tests |
| E17 | Every new test is mutation-checked; survivors are reported | Section 10 |

## 9. Game-layer design notes

- **One clock.** The session `RuntimeClock` is the only clock. `EconomyComponent` asks an `IEconomyClock` for the step of this tick and the economy moves there (at most 64 periods per Update; the rest is owed, and `EconomyStepped.Owed` says how many). Three clocks: `TickEconomyClock(ticksPerStep)`, the calendar's (`EconomyCalendar.Clock`, read after the calendar component has moved this tick, so the economy component is added after the calendar), and `JournalEconomyClock` (the steps a recorded run used).
- **Channels.** `EconomyChannels` is a sealed subclass of the one `ChannelRegistry`, rank 3. Unserved channel: `ECON_CHANNEL_UNKNOWN` in the same tick; second component on a channel: `ECON_CHANNEL_DUPLICATE` at Init.
- **Requests and answers.** Eight requests (trade, shock, settle, growth, fund, lend, quote, price). All only queue; each is answered in the next Update in arrival order with `EconomyApplied` (and the changes it made), `EconomyTradeExecuted` or `EconomyQuoted` (a trader, a market, a good, the unit price, the value, the tax and the total in the market's currency), `EconomyPriceAnswered` or `EconomyRefused` with the code. A refusal leaves the economy as it was (tested by digest). A request emitted by a component that runs earlier in the tick is answered in the same tick.
- **Deferred, bounded events.** The changes of the periods (rates, shocks, arrivals, band changes, shortages, launches, blocked routes, settlements, loans) are queued as owed closures in a fixed order and at most `maxEvents` (default 2,000) are raised per Update, oldest first, none lost. While `maxOwed` (default 50,000) are owed, time does not move (the economy lags its clock instead of growing the queue without end; it can pass the limit by what one Update adds). `IsQuiet` is false while any are owed, and a checkpoint is refused (`ECON_NOT_QUIET`).
- **Journal.** Text, one line per entry: `F|tick|facts`, `S|tick|step`, `R|tick|channel|kind|requestId|a|b|c|d|e|n|m|p|q`; escapes shared with the calendar (`JournalText`). Replay needs a tick rate that divides 1,000,000. The transcript is `EventTranscript`.
- **Calendar bridge.** `EconomyCalendar.Check` refuses a pair of documents at assembly (steps per day must divide the calendar's ticks per day; every host fact must be a calendar fact of the same type whose range the calendar's range fits inside). `EconomyCalendarDates` turns `day:<n>`, `weekday:<id>` and `holiday:<id>` into a settle request for a manual schedule. The calendar announces a day once however many days a skip crossed, so a date inside a skipped stretch is not seen (tested).
- **Dialogue bridge.** `EconomyDialogueFacts` gives a market's facts as `ext.*` external facts; `Check` refuses a dialogue document whose declarations disagree in type or range. `EconomyDialogueOutcomes` turns a signal or a quest status into a trade, shock or settle request on the economy channel; the answer is one tick later, never a call.
- **AI bridge.** `EconomyAiBridge` posts the percept for the market an agent trades in (`ratio_<good>`, `short_<good>` for every good) when it changed, so an agent decides by what it was last told (tested, including acting on stale knowledge). The AI document's percept kind is checked.
- **SimEngine ledger bridge.** `EconomySimLedger` listens to `EconomyTradeExecuted` and sends one `stock.flows` effect per tick (at most `linesPerTick` lines, the rest waits in order): a buy consumes the cash `econ.cash.<currency>` and issues the goods `econ.good.<good>`, a sale the other way, as the inventory's own lifecycle flows. `EconomySimContent.Document` generates the inventory vocabulary from the economy document. The host asks `Affordable` before it requests a trade; a ledger that refuses the flows faults the session, as in the combat stock bridge.
- **What a checkpoint omits:** the registry (fixed by assembly) and the tick (kept by the caller: `EconomyComponent.Tick`).

## 10. Result

**Built (2026-10-29), nothing committed or pushed, no Actions run.** All in `GameFrameWork.slnx`:

| Project | What it is |
|---|---|
| `EconomyEngine.Core` (35 refusal codes, 22 files) | Format 1 and strict loader; `EconomyMath`; `EconomyRuntime` (currencies, markets, pairs, routes, shipments, policies, budgets, loans, shocks, history, requests); facts; explanations; save; locale keys |
| `EconomyEngine.Cli` | `check`, `simulate`, `explain`, `coverage` |
| `GameRuntime.Economy` | `EconomyComponent`, `EconomyChannels` (rank 3), 23 events, clocks, checkpoint, journal, replay, transcript |
| `GameRuntime.Economy.Bridges` | calendar (clock, document check, dates for manual schedules), dialogue (facts and outcomes), AI (market percepts), SimEngine ledger (`EconomySimLedger`, `EconomySimContent`) |
| `GameRuntime.Economy.Headless` | the town demo |
| `GameRuntime.Dialogue` (`DialogueTriggers`) | the trigger vocabulary the relationship and economy bridges share (it was private to the relationship bridge) |
| `EngineCommon.TestSupport` (`SyntaxGuards.Coalesce`) | two new shared Roslyn rules: nothing thrown from the end of a coalesce; no use of named types by a layer |
| `EconomyEngine.Tests` (412), `GameRuntime.Economy.Tests` (118), `EngineCommon.Tests` (64, 14 new) | the tests |
| `src/EconomyEngine/samples` | `town.economy.json`, `town.en.json`, `town.scenario.json` |

### 10.1 Criteria

| # | Result | Evidence |
|---|---|---|
| E1 | Met | `RefusalMatrixTests`: 77 inputs (document, calls, saves); the set of codes they reach equals the 35 constants of `EconomyCodes` by reflection |
| E2 | Met | `MathTests`: rounding table incl. negatives and ties, the annuity against big-integer arithmetic for seven loans, repayment to zero |
| E3 | Met | `PriceTests`: 200 random pairs x 3 periods equal an independent big-integer oracle; equilibrium; direction; bounds over 60 random pairs x 40 periods; the largest step; inertia; bounded responses; spoilage and capacity; shortage events; seeded noise equal to `DeterministicRoll`; history ring; bands with margin (9,000-step property); split advances equal one; 64 periods per call. `BoundaryTests`: units traded between periods against the oracle (120 random pairs) |
| E4 | Met | `SettleTests`, `BoundaryTests`: tax accrual and remittance by schedule and tag, subsidy proportional to the treasury, wages, arrears and the output penalty, indexation |
| E5 | Met | annuity and bullet payments against an independent amortisation, a miss capitalises, default, loans from a market, lending refusals |
| E6 | Met | `CurrencyTests`: growth and minted money per period, the money/level relation, deflation to the floor, the ceiling of the level, rates against an independent sequence, pegs, devaluation and recovery, the reference never moves |
| E7 | Met | `RouteTests`, `BoundaryTests`: the loading rule recomputed, margin and freight at the exact edge, capacity, delay, tariffs, two currencies, shipment limit, blockade, full warehouse |
| E8 | Met | `ShockAndModifierTests`: start, scale, duration, stacking, scope, goods by id and tag, growth and price shocks, facts missing or wrong refused before anything moves |
| E9 | Met | `BoundsTests`: 300 random sequences of 80 calls (24,000), invariants after every call (conservation, price bounds, stock, bands, shipment and loan validity), save and open every 20 calls; found the deflation divide-by-zero (10.2) |
| E10 | Met | `SaveTests`: nine kinds of change change the digest; 67 forgeries with recomputed digests refused as state; the same forgeries with the old digest are refused as state except the seven that need the document (checked after the digest); format, document, range, digest order |
| E11 | Met | `SourceGuardTests`, `RuntimeSourceGuardTests`, `SyntaxRuleTests`: no clock, random, float or file access in the core and game layer; no silent catch; no throwing catch-all arm; nothing thrown from a coalesce; the bridge never names the inventory's transaction interface; each rule is run on synthetic sources that break it |
| E12 | Met | `ComponentTests`: queue until Update, order, same-tick refusal, duplicate channel, two economies, bounded owed events with none lost or reordered, back-pressure, checkpoint refusals, clock faults reported once, fingerprint |
| E13 | Met | `ReplayTests`: from a checkpoint, from the start, from 11 other quiet ticks, with back-pressure; a missing request or a changed fact is detected; journal text strictness; the demo replays on every run |
| E14 | Met, with limits (10.7) | calendar (`CalendarIntegrationTests`), dialogue, AI, SimEngine ledger (`SimLedgerTests`, against a real `SimGame`) |
| E15 | Met, measured | 10.4 |
| E16 | Met | `CliTests`: every command and exit code |
| E17 | Met in part | 10.5 |

### 10.2 What the work found, and decided (source followed)

- **A deflated currency divided by zero.** The negative-growth test drove a price level to 1, which made a good's reference price 0. Fix: a reference price is never below one milli. Regression: `NegativeGrowthDeflates...`, `TheMostDeflatedAndMostDevaluedStates...`.
- **Event backlog without end.** With few events raised per Update and a period that produces more, the owed queue grew without bound (64,000 events in the test). Decision (back-pressure; the relationship component's rule is that none is lost): while `maxOwed` events are owed, time does not move; the economy lags its clock and the queue passes the limit only by what one Update adds. A replay with the same parameters matches.
- **The shared fact model carries one billion.** A dialogue document cannot declare an external fact above 1,000,000,000, so no dialogue could cover a treasury or price range. Facts saturate at one billion (`FactCeiling`); the state itself keeps larger numbers.
- **Allocation.** The tag matcher allocated a delegate per call (10.5 MB a period at 20,000 pairs); a loop brought it to 6.2 MB. Not tuned further.
- **A request from a component that runs earlier in the tick is answered in the same tick**, so a checkpoint is refused only while a request waits between two components; tested with a requester, a taker and the component.
- **Prices sit at their bounds in the demo.** Goods with no supplier in a market stay at the ceiling until a route reaches them (the blockade of day 75 shows it); this is the model working, not tuned to look balanced (10.7).
- **The trigger vocabulary had one owner too many.** It was private to the relationship bridge; it is now `DialogueTriggers`, used by both bridges, with its own tests; the relationship suite and demo are unchanged.
- **Mutation checking found 50 survivors in the first round of the core**, among them untested boundaries (a count exactly at its limit, a loan from the first market, a subsidy the treasury cannot cover), the units a trader moved between periods, and the order of the save checks (a range violation with the old digest must be refused as state). Tests were written for each; see 10.5.

### 10.3 The demo

*Format 1 values; the format 2 ones are in 11.5.* `GameRuntime.Economy.Headless`: three towns (Riverton and Hillfort in crowns, Seaport in florins) on the Gregorian calendar, 120 days by default, six routes, a caravan that trades on seeded days, a drought (day 20), more florins printed (day 30), a devaluation (day 40), a plague (days 50 to 58, a host fact), a loan from the world (day 60), a blockade (day 75), and the tax decree on every 15th. Default: `output-sha256 0A9B16B30991A3287584F651343153DDEC8C46ACA5C26BC8889E75EE59A33889` (twice the same; replay of ticks 57,631 to 172,800 from the checkpoint at 57,630: state same, events same, 3,144 events); `--days 20 --checkpoint-day 7`: `ACCB42B3215C55A763010BDB4F4455EA91291906DE929DF08968B91A6B5DF783`. The earlier demos are unchanged (re-run after the last change): Briefing go `201EA953...F219261`, wait `7FEE8EF5...F71FEE`, calendar `22848928B5F0...A57C`, relationship village `C0758AD7...DE2F`.

### 10.4 Cost (this machine, AMD Ryzen 5 5600X, Release, 2026-10-29; `ScaleTests`, three runs, median of the repeats; `docs/measurements/economy-scale-2026-10-29.csv`)

200 markets x 100 goods (20,000 pairs), 400 routes, three currencies, four policies, wages and upkeep, 20 loans, history 16.

| Question | Run 1 | Run 2 | Run 3 |
|---|---|---|---|
| Load the document (strict JSON, 20,000 pairs) | 515 ms | 381 ms | 417 ms |
| The first period (JIT included) | 399 ms | 354 ms | 342 ms |
| An ordinary period, median (min, max) of 15 | 30.7 ms (27.5 to 38.7) | 28.8 ms (26.6 to 35.5) | 30.8 ms (27.1 to 38.0) |
| Allocation of a period | 6.1 MB | 6.1 MB | 6.1 MB |
| One trade | under 0.01 ms | under 0.01 ms | under 0.01 ms |
| Save: write, open (5.3 MB) | 38 ms, 71 ms | 36 ms, 60 ms | 35 ms, 56 ms |
| State digest | 21 ms | 23 ms | 21 ms |

About 21,000 shipments are on the way at that scale. Budgets in `ScaleTests` (set from these, about 10 times the median): a period 300 ms, the first period 3 s, 20 MB allocated, a trade 5 ms, a save 500 ms, opening one 1 s; the Debug build passes them too (a period took 70 to 75 ms). A period is a day in the demo, so 30 ms is acceptable only because a game closes a period a day; a game that closes one every frame at this scale does not fit a frame. The component's cost per Update (events) was not measured separately; the town demo runs 172,800 ticks and 3,144 events in about 5 seconds.

### 10.5 Mutation checking

Mutants are single-expression changes applied by a script outside the tree, built and run against the test project; the script restored each file and compared its checksum every time. **344 mutants** over the core, the Cli, the component, clocks, journal and replay, the four bridges, the demo, `DialogueTriggers` and the shared coalesce rule. Round 1: core 234 mutants, 183 killed, 50 survived, 1 did not compile; game layer 100 mutants, 76 killed, 14 survived, 10 did not compile. The survivors showed missing tests (boundaries at exactly a limit, a lender that is the first market, two schedules at once, the units a trader moved, a subsidy the treasury cannot cover, the order of the save checks, an empty price history, a trade worth under a minor unit, an exactly sufficient cash balance, the ledger not settled while its flows are in flight); `BoundaryTests` and 7 more forgery cases were written for them and the demo's and replay tests extended; two more survivors appeared when the first fixes were re-run and were killed. **Final: 311 killed, 18 survived, 15 did not compile** (a mutant that cannot compile is not counted as killed).

Survivors, judged one by one:

- **Equivalent (11, core):** `BandOf` `plain >= current` as `>` (the same band comes back either way); `Advance` `target == from` as `<=` (the target is never before the present); three `instance.Start >= period` as `>` (a shock's start is the period it was asked in, and a period being closed is later); `remaining <= 0` as `< 0`, `gain <= 0` as `< 0` and `surplus <= 0` as `< 0` in the loading rule (each gives a quantity of zero, which is skipped); an active loan with `Remaining <= 1` for `== 1` (an active loan has one left at least); `Math.Max(0, jump)` of a price shock (a negative factor is clamped to the floor anyway); the 128 MB save size check at exactly the limit (both refuse it, as a format error).
- **Not observed (7, game layer):** `ToInt` at exactly `int.MaxValue` (the same code either way); the ledger's `Whole` at exactly 2,147,483,647 (**a real untested boundary**); a zero quantity in the ledger's trade handler (cannot arrive); `TextArguments` for a one-character text (cannot start with `s:`); the caravan chance 280 against 290 per mille (no roll in the recorded runs falls between); and the demo's divergence check and exit code 3 (`&&` as `||`, and `return Ok`), which only a diverging replay would show. Nothing makes the demo diverge on purpose.
- **Did not compile (15):** the removal of the journal calls (`WriteFacts`, `WriteStep`; tried again as a plain assignment, still refused by warnings as errors), a flipped `TryGetValue` that leaves its out variable unassigned (five), a flipped `is null` check that dereferences a nullable (one), an unused-field warning (one), a statement made empty (one), and two in the shared rule's own file. Whether tests would catch those is **not shown**; the journal's text is tested line by line (`ReplayTests`) and the replay detects a missing request or fact, which is indirect evidence only.

Not covered by any mutant, so their tests are not shown to fail: the wording of `explain` beyond the tested terms, the Cli's scenario reader beyond the shapes tested, the order of the change events inside the component's `EventsOf` (tested as a whole by the bounded-events comparison, not by mutants), the 64 MB document limit, and the unchanged parts of the relationship bridge that `DialogueTriggers` replaced (its suite passes).

### 10.6 Suites

`dotnet build GameFrameWork.slnx`: 0 warnings, 0 errors. EconomyEngine 412, GameRuntime.Economy 118, EngineCommon 64 (50 before), RelationshipEngine 201, GameRuntime.Relationship 92, CalendarEngine 206, GameRuntime.Calendar 86, DialogueEngine 1,200, AIEngine 209, CombatEngine 139, GameContent 356, GameRuntime 559, ScenarioEngine 90: all passing in the final run (`dotnet run --project <tests> --no-build`; `dotnet test` is not supported).

### 10.7 Not done, and why

- **Tycoon cash (`IEconomy`).** The ledger bridge records cash as an inventory resource per currency. The tycoon module's per-company cash with authored reasons is a different, optional module and is not bridged.
- **The ledger follows the economy; it does not lead.** A trade is made first and written a tick later. A ledger that cannot take the flows faults the session; `Affordable` is advisory and the host must ask it first. A trade that reserves in the ledger before the economy commits (two-phase) is not built.
- **A trader's money is never the market's.** A player trade does not spend or fill the market's treasury (RimWorld's limited-silver traders are not modelled); the world account is the counter party.
- **No production chains (since pass 46: see section 13), no population, no substitution.** Each good's price is its own; no good is made from other goods, no consumer has an income, and no good replaces another. Prices at the bounds in the demo are what an isolated good with no supplier does.
- **Rates do not follow the trade balance and there is no interest-rate policy.** Inflation comes from money growth only; velocity and output are not modelled; the speed at which a rate follows parity is the document's (no source gave one).
- **Loans have no collateral and no insolvency beyond the default flag.** Day counts are not modelled: the period is the unit.
- **A calendar skip hides the dates it crosses** (the calendar announces one day); a game that skips time settles those schedules itself.
- **Facts saturate at one billion** (the shared fact model's limit); a condition that needs more reads the economy directly.
- **The queue of owed events can pass `maxOwed` by what one Update adds** (up to 64 periods).
- **Per-period allocation** (6 MB at 20,000 pairs) was not reduced further.
- **The AI bridge tells an agent about the one market it trades in**; no percept of price history or of other markets.
- **Godot adapter, other locales, other operating systems**: none; Windows only, Debug and Release.
- **Not researched to a primary source:** Offworld Trading Company, Anno, Patrician, Capitalism, Stardew Valley and Dwarf Fortress price formation (section 2). The design rests on the three game pages that were read and on textbook rules.
- **Tutorial/Onboarding, WorldEvents and Diplomacy**, the three remaining engines of the six, were not started.

## 11. Pass 41: capacity by gain, the landed-cost price field, format 2 (2026-10-29)

Work item 2 of `docs/ENGINE_ALGORITHM_REVIEW.md` (section E1, steps A and B), built on `EngineCommon.Graph` (`docs/ENGINE_GRAPH.md`). Production chains (E2, item 7) are **not** built: format 2 only reserves the `recipes` array. Written after the code; the results are in 11.5 to 11.8.

### 11.1 What changed

| Area | Before (format 1) | Now (format 2) |
|---|---|---|
| Who gets a route's capacity | the goods in the order the document lists them; the first goods took it all | the goods that pay most **per unit** first (then the higher gain ratio, then the lower good id, ordinally); the order of the document no longer matters |
| Which route loads first | document order of the routes (two routes leaving one market shared its stock by that order) | order of the route ids, ordinally |
| What a merchant compares | the next market's price | `direct`: as before. `landed`: the best value reachable from the next market through the routes, less everything paid on the way |
| Document | format 1 | format 2: new required `routing: { mode: "direct" \| "landed" }` and `recipes: []` (reserved; non-empty is refused with `ECON_RECIPES`); format 1 refused with `ECON_FORMAT` ("no longer read") |
| Save | format 1, digest text `economy-state-1` | format 2, `economy-state-2`; a format 1 save is refused with `ECON_SAVE_FORMAT`. The field is **not** state (recomputed from prices each period), so the save holds nothing new and the restored economy lives on exactly like the original (tested) |
| Explain / CLI | price, loan, currency | + `route` (why each good got its capacity, in order) and `reach` (the best way on from a market); `check` prints the routing mode |
| AI bridge | percept `ratio_*`, `short_*` per good | + `reach_*` per good: the best value a unit could fetch from the market over its price here, per mille (1,000 = selling here is best); the AI document must declare it (the bridge refuses a document that does not) |
| Codes | 35 | 37: `ECON_RECIPES`, `ECON_UNKNOWN_ROUTE` |

### 11.2 The algorithm

**Capacity by gain** (`EconomyRuntime.Plan.cs`, one method, `Allocate`, used both to load and to explain). For each good both ends trade: `gain = value at the destination - origin price - (freight + export tariff on the origin price + import tariff on the destination price)`, in the origin's money. Over the margin (gain per mille of the origin price) and with a surplus over one period's use, merchants ask for `surplus x min(share, ratio)`. The candidates are sorted by `(gain desc, ratio desc, good id asc)` and the capacity is filled in that order; a good that gets part of what it asked for is `Trimmed`, one that gets none is `NoCapacity`. The review's text said "gain ratio"; this pass ranks by **gain per unit** because the capacity is counted in units: filling a knapsack of units by profit per unit is optimal for a fixed set of quantities (own reasoning), and the test tries every ordering of up to five goods (11.6) to check that nothing earns more. The ratio is the second key, so a tie on money still prefers the better margin.

**The landed field** (`EconomyRuntime.Landed.cs`). For one good, `V(m) = max(price(m), max over routes m -> n of V(n) - cost(route))`, the cost being the route's freight plus its two tariffs on the current prices, all converted to the reference currency (a price is `price x rate / 10^6`, truncating). Costs are not negative, so the best way is a simple path and the question is a shortest path in disguise: build the reversed routes, add a sink joined to each market by an edge of `dearest price - price(m)` (never negative), run one `Dijkstra.Run` from the sink, and `V(m) = dearest - distance(m)`. The graph's tie-break (lowest node, then first edge) makes "sell here" win ties. Markets are numbered by the ordinal rank of their ids and a route is told apart by the rank of its id (carried as the edge's capacity), so nothing depends on document order (tested on shuffled documents). One graph per good (about 100 per period at the 200 x 100 scale).

**Using it.** A route's gain uses `V(next market)` converted into the origin's money, never less than the next market's own price, unless the best way on from the next market passes back through the origin: a good does not travel in a circle (tested: the route back is not counted, so it ships nothing). Shipping is still one hop at a time, so no itinerary is stored; a shipment arrives, joins the stock and the next route decides again with the prices of that period. `direct` mode gives `V(next) = price(next)` and the format 1 gain arithmetic exactly; `landed` equals `direct` wherever no way on exists (tested: one route, and two routes back to back, 25 periods, state equal).

**Limits, stated.** (1) The graph library bounds an edge weight at 10^9. A good whose dearest price or costliest route passes it has all its weights divided by the smallest power of two that fits; the field is then right to within that power per route (exact in the ordinary case, tested exact above 10^9 for multiples of 4). (2) `maxHops` of the review is **not** built: an exact bound on hops needs a graph with a layer per hop (about `hops` times more edges to sort per good), which would multiply the cost below; best ways are simple paths and `reach` names how many routes they take. (3) The field ignores delay and ignores that prices move while goods travel (a merchant may chase a price that has gone by arrival; format 1 had the same exposure over one hop). (4) If the best way on from the next market comes back through the origin, only the next market's own price counts, even when a worse way that avoids the origin exists (the exact answer would need one search per origin). (5) The share of a route's capacity is greedy per route; routes that compete for one market's stock are served in the order of their ids, not optimised together. A min-cost flow over all routes and goods (review: "adapt later, only if contention is shown") is **not** built.

### 11.3 Research basis

No new sources were fetched in this pass. Followed: the review's E1 (steps A and B; its cost estimate for the field, 1 to 3 ms on top of 29 ms, was an estimate and turned out low, see 11.7), `docs/ENGINE_GRAPH.md` (Dijkstra with the (cost, node) tie-break). The review cites OpenTTD's link-graph job ("a slightly modified Dijkstra", S: snippets only, not read). Shortest-path-as-longest-profit with non-negative costs and a super-source is the standard reduction (own reasoning, checked by the brute-force oracle below, not by a cited page). Greedy by profit per unit is the fractional-knapsack rule (own reasoning, checked by trying every ordering). Not read: Anno or Patrician route logic (the document still records those as unresearched).

### 11.4 Tests (independent oracles)

`EconomyEngine.Tests` 412 -> 465 (+53). New files `LandedTests` (field), `CapacityTests` (allocation, explain), `RoutingTests` (format, equivalence, conservation, plan, save), helper `Net`.

- **Hand-worked chain** (A, B, C; prices 10,000 / 10,000 / 30,000; freights 1,000 and 2,000): the field is 27,000 / 28,000 / 30,000 worked on paper; landed routing ships 800 from A in period 1, 600 from B in period 2 (period 3: C holds 580 after using its 20), direct routing ships nothing and C stays empty.
- **Brute force**: 400 fixed-seed (SplitMix64) random networks of 2 to 7 markets, up to 12 routes (parallel routes and cycles included), two currencies, export and import tariffs; for every market the field equals the best of **every simple path** found by depth-first enumeration (the oracle re-implements the cost arithmetic and shares no code with the field), the way named is a real simple path that earns exactly the value, and 150 of the networks are also declared in shuffled order with identical answers.
- **Capacity**: a hand example where the higher-ratio good loses the capacity to the higher-gain one (4 declaration orders, 2 modes); every ordering of 2 to 5 goods tried on 250 random routes, none earns more than the engine's (and in 60+ of them the capacity bound); ties go to the lower id whatever the document order; 40 random networks x 2 modes x 6 periods declared in two orders have identical flows and prices; two routes sharing an origin share by id.
- **Conservation**: 60 random networks x 30 periods in both modes, production and consumption zero: goods in stock plus goods in flight stay exactly what they started as, launched minus arrived equals in flight, no negative stock, money conserved in every currency. (The existing 300 random 80-call sequences on the town sample, which is now landed, also pass.)
- **Plan equals period**: `PlanRoute` before a period names exactly the loads the period makes (120 random networks x 2 modes).
- **Format**: format 1 and 3 documents, a routing mode that does not exist or has the wrong type, recipes non-array, non-empty, with junk, missing `routing` or `recipes`, an unknown field in `routing`, an unknown route, a format 1 and a format 3 save; all by named code, all in the refusal matrix (which requires every declared code to be reached).
- Weights above the graph bound; the percept; the CLI; the explanation text.

### 11.5 The demo and the other demos

The town demo's output hash **changes**, legitimately, for three reasons stacked. (a) The document is a different text (format 2, two new fields), and the state digest it prints includes the document's digest, so the hash changes whatever the economy does. (b) The sample is now `landed`, which sends goods across a middle market (3,349 events against 3,144). (c) Capacity by gain and routes in id order change the flows even without landed routing: the same sample set to `direct` gives 3,148 events and a third hash (an experiment, not recorded as the demo).

| Run | Before (format 1) | Now |
|---|---|---|
| `--days 120` (default) | `0A9B16B3...59A33889`, 3,144 events | `67BA46D30317B3E5F3A3344940E16AAC5F3522A52752DAB82D15C0335B80D65B`, 3,349 events |
| `--days 20 --checkpoint-day 7` | `ACCB42B3...5DF783` | `1A2A0CA38A21F5E8B4574C1DA977EB11A5C4D3B6286C445925FB6E1BD0AA6E2E` |
| (experiment) default with the sample's mode set to `direct` | | `04707B965DAE289CD25A54645FA4B8E1660839257B4DD1037B1F8CCE5277C85C`, 3,148 events |

Twice the same; the replay from the checkpoint (ticks 57,631 to 172,800) gives state same, events same. `HeadlessTests` holds the two new hashes. Diplomacy's demo (the isles economy, `direct`, capacity never binds there) is unchanged: `19D3184D3B71968A0CD61ABAA4EA242AA40407C6EF74F1FDAB8C9B1E473B025D`, twice the same, replay same. The world-events demo runs the town economy beside the world: it now prints `CC2DBBD74C92839DCD02D688B696B2495B5DE9B961CCB7FA27B741AA4252193A` (twice the same, replay same, 441 events), against `0D9248C0...AEC3F` in the plan; part of that is this pass (the economy it runs is the town sample above), but the world-events engine was being edited by pass 42 in the same tree when this was run, so the share of this pass is **not isolated**. No other demo references the economy (checked by project references).

### 11.6 Cost (Release, 200 markets x 100 goods, 400 routes, three currencies; `ScaleTests`, three runs, median of 15; `docs/measurements/economy-routing-2026-10-29.csv`)

| Question | Run 1 | Run 2 | Run 3 | Format 1 (10.4, a quieter machine) |
|---|---|---|---|---|
| Ordinary period, `direct` (median, min to max) | 51.1 ms (45.0 to 66.4) | 49.9 ms (43.4 to 143.5) | 44.6 ms (40.3 to 47.8) | 28.8 to 30.8 ms |
| Ordinary period, `landed` | 85.6 ms (73.1 to 134.7) | 88.3 ms (70.9 to 167.3) | 82.9 ms (67.5 to 118.2) | not applicable |
| The landed field alone (`ComputeLandedField`, 100 goods, 15 repeats) | 36.8 ms | 25.3 ms | 26.7 ms | |
| Allocation of a period, `direct` / `landed` | 6.1 MB / 15.6 MB | the same | the same | 6.1 MB |
| Unchanged control: state digest, `direct` runs / `landed` runs | 46.2 / 64.6 ms | 35.3 / 44.5 ms | 33.3 / 43.2 ms | 21.0 to 22.8 ms |
| Save write / open, `landed` | 159 / 342 ms | 100 / 158 ms | 64 / 134 ms | 35 to 38 / 56 to 71 ms |

**Read this honestly.** The machine was loaded by other work while this was measured: the state digest, whose code this pass did not touch, took 1.5 to 3 times its recorded time (21 to 23 ms). So the absolute numbers are inflated and noisy, and I cannot separate "format 2 `direct` costs more than format 1" (it adds one sort of at most 100 candidates per route) from the load. What the runs do show: `landed` costs about **1.7 to 1.9 times** `direct` in the same process (about 35 to 40 ms more per period here; if the control's slowdown applies, roughly 17 to 25 ms on a quiet machine), and allocates about 9.7 MB more per period. The review estimated the field at 1 to 3 ms; that was **wrong by roughly an order of magnitude**. I did not profile it. The likely cost is the graph build: `ENGINE_GRAPH.md` section 5 measured a 200,000-edge build at 60 to 83 ms and a Dijkstra over the same at 10.5 ms, and the field builds 100 graphs of 600 edges (60,000 edges) a period, which is about 18 to 25 ms of build against 3 ms of search. An immutable-graph library that cannot reweight an existing topology forces that sort once per good; adding a reweighting entry point to `EngineCommon.Graph` is the obvious next step (not done: it would change a shared library and its tests). At the budget the document set (a period every 24 steps, a day), a period is still far under the 300 ms budget in `ScaleTests`, which passes in both modes; a game that closes a period every frame at this scale does not fit either mode (as 10.4 already said). The first-period figures are not comparable between modes (the second case runs in a process the first has already warmed).

### 11.7 Mutation checking

Mutants are single-expression changes applied by a script **outside the tree**: it works on a copy of `src` in the scratchpad, builds the test project, runs the tests, and restores the file and compares its SHA-256 after every mutant; the unmutated copy built and passed (460 tests) before the first. **104 mutants** over `EconomyRuntime.Landed.cs` (36), `.Plan.cs` (32), `.Routes.cs` (6), `EconomyRuntime.cs` (3), `EconomySet.cs` (3), the reader, loader, save and image (8), `EconomyExplain.cs` (7), `EconomyCli.cs` (3) and the AI bridge (6). Each ran first against the seven test classes that cover routing (`LandedTests`, `CapacityTests`, `RoutingTests`, `RouteTests`, `RefusalMatrixTests`, `SaveTests`, `CliTests`), and, if it survived those, against the whole project (the AI bridge's against `AiIntegrationTests`).

- **Round 1:** 91 killed (90 by the seven classes, 1 only by the full project: the margin comparison), **13 survived**, none failed to compile, none timed out. (A caution: the first attempt started two runners by mistake; the second run corrupted the first's results, one mutant was left in the real tree, and I found it by checking every mutant's text against the tree, repaired it, and moved the runner to the copy. Nothing from that attempt is counted.)
- **The 13 survivors, one by one.** Eleven showed missing tests and each got one: the order between equal gains (ratio second: `WhenTheGainIsTheSameTheBetterRatioGoes...`); a shock on the route seen by the plan and by `CapacityNow` (`AShockOnTheRoute...`, two mutants); a route index out of range at either end (`AnUnknownRouteIndex...`, two); a rank 1 good blocked by the shipment limit listed as waiting, not skipped (`AGoodThatWasGivenCapacityButBlocked...`); a tie where the way on is worth exactly the price at home after truncating across a currency boundary (`ARoundingTieAtTheCurrencyBoundary...`, which kills three: the route-into-a rule `>=`, and the two guards in `Reach`); a direct route that nets exactly what selling at home fetches (same test); the percept for a good a market does not trade (`AGoodTheMarketDoesNotTrade...`). **Re-run: all 11 killed.**
- **Two survive, both judged equivalent.** `surplus <= 0 ? 0 : ...` as `< 0` (a surplus of zero gives a quantity of zero either way). The field's initial route `-1` as `0` for a good that no route carries: `Reach` guards it (`route < 0 || value <= price` returns the home price when the "way" is worth no more than it) and nothing else reads a good without routes.
- **Guard check:** two source mutants injected into the new files, `a ?? throw ...` and a `switch` with `default: throw new ArgumentOutOfRangeException`: both caught by the shared Roslyn guards in `SourceGuardTests`.
- **Not covered by any mutant:** the wording of the explanation text beyond what the tests assert, the test helpers, `ScaleTests` (the cost budgets), and the unchanged parts of the engine.

### 11.8 Suites

`dotnet build GameFrameWork.slnx --no-incremental`: 0 warnings, 0 errors (an earlier build showed one MSB3061 warning because another process held `GameRuntime.Relationship.Tests.exe`; the rebuild was clean). EconomyEngine 465 (412 before this pass), GameRuntime.Economy 120 (118), EngineCommon 72, EngineCommon.Graph 65, DiplomacyEngine 505, GameRuntime.Diplomacy 160, TutorialEngine 170, GameRuntime.Tutorial 127, WorldEventsEngine 200 and GameRuntime.WorldEvents 111 (pass 42 is changing them in the same tree; both passed in the run), RelationshipEngine 269 and GameRuntime.Relationship 116 (pass 43, the same), CalendarEngine 206, GameRuntime.Calendar 86, AIEngine 209, CombatEngine 139, GameContent 356, ScenarioEngine 90: all passing (`dotnet run --project <tests> --no-build`). **Not clean on the first try:** DialogueEngine (1,200) and GameRuntime (559) each had one wall-clock test fail in the first full run while the machine was loaded (`CostTests.AHundredThousandLineSteps...UnderASecond` took 9 s; `CombatRuntimeHardeningTests.ABoundedRealTimeRunPlaysThreeGroupsOnTheWallClock`); both pass when their class is run alone, and neither was rerun in full. The first Debug run of `ScaleTests` in landed mode failed its 300 ms budget (405 ms) under load; the landed mode now has its own budget (600 ms, `Budgets.LandedPeriodMs`) and the test passes alone (a Debug landed period took 203 ms, direct 131 ms, still on a loaded machine). `tests/WorldCreator.Tests` (a console project, not xunit) stops with "Block subdivides into four purpose-defined frontages" in a file this pass did not touch and whose project references no economy project; I did not run it before my change, so "not caused by this pass" rests on that reading, not on a baseline. Several suites could not be started at one point because another process was rebuilding or running their binaries; they were rerun and passed. The final `dotnet build GameFrameWork.slnx --no-incremental` ended with 0 errors but with MSB3061 warnings for files other processes held open (test executables of other agents); the one before it was 0 warnings, 0 errors.

### 11.9 Not done, and why

- **Production chains** (review item 7): done in pass 46, section 13 (the owner decision is there: recipes are authored once in the catalog).
- **`maxHops`** of the review: see 11.2 (2). **Min-cost flow** across routes and goods: not built; the greedy-by-gain rule is optimal per route for fixed quantities (11.4), not across routes that share an origin's stock.
- **Faster field**: done in pass 44 (section 12); the cause 11.6 guessed (the graph build) was only part of it.
- **The world-events demo's share of its hash change** is not isolated (11.5).
- **Delay is not in the field** (a far market that pays more in ten periods looks as good as one that pays in two), and merchants can still chase a price that moves while goods travel.
- **Warehouse limits are not in the field**: goods sent to a market whose warehouse is full are lost on arrival, as in format 1; the loading rule does not look at the room there.
- **Other locales, other operating systems, a Godot adapter**: none; Windows only.

## 12. Pass 44: the landed field profiled and made cheaper (2026-10-29)

**Finding to explain.** 11.6 measured the landed field at 25 to 37 ms a period (200 markets x 100 goods, 400 routes) against the review's 1 to 3 ms, and guessed the cause was building a graph per good. Profiling (Release, stopwatch around each phase, 10 fields per sample, 5 samples, the same process, the unchanged code) said otherwise:

| Phase (per field of 100 goods) | ms | Share |
|---|---|---|
| Finding each market's price and each route's cost for the good (the per-route arithmetic: four `Int128` divisions at about 16 ns each, tariff lookups, list indexing) | 11.0 (of which the dearest-price loop 0.8) | about 60% |
| `GraphBuilder.Add` x 100 goods x 600 edges | 2.0 | 11% |
| `GraphBuilder.Build` (the sort and four arrays) | 2.4 to 2.8 | 14% |
| `Dijkstra.Run` | 1.7 to 1.8 | 10% |
| Reading the answer back | 0.55 | 3% |
| Total | 17.4 to 19.1 | |

Allocation was 9.7 MB a field, of which only 1.75 MB was the graphs' own arrays: the rest was `TariffRate`'s `foreach` over an `IReadOnlyList<int>`, which allocates an enumerator (80 bytes a route and good, 40,000 of them a field). So the build was about a quarter of the time, not the cause; the per-route cost arithmetic was the larger part, and the allocation was mostly something else. The 11.6 estimate (18 to 25 ms of build) was wrong; the 25 to 37 ms there was the same code on a loaded machine.

**Change.** (1) `EngineCommon.Graph`: `CsrGraph.Reweight` (see `docs/ENGINE_GRAPH.md` section 9). (2) `EconomyRuntime.Landed.cs`: goods that trade in the same set of markets share one graph (the sink's edges and the routes between trading markets are fixed by that set); the field replaces its weights instead of building another. At most 8 topologies are kept (a good with a ninth trading set replaces the oldest, which costs a build as before); nothing in a result depends on whether a topology was reused. (3) `TariffRate` loops by index (no enumerator), and each market's export and import rate is found once per good, not once per route (`RouteCost` has an overload that takes the two rates; the old one calls it). Results are identical: the Economy demo hash is `67BA46D3...0D65B` and the short run `1A2A0CA3...6E2E`, both as before (this is a pure performance change; if a hash had moved I would have stopped).

**Measured** (`docs/measurements/economy-landed-reweight-2026-10-29.csv`; Release; one process per run, 3 runs; direct, landed-before, landed-after and the two fields measured in turn within each of the 15 repeats; "before" is the previous per-good graph code kept as a temporary copy, which already includes the enumerator fix in `TariffRate`, so the true pre-pass allocation was higher still). The machine was quieter than in 11.6: direct period 33 to 36 ms here against 45 to 51 there.

| | run 1 | run 2 | run 3 |
|---|---|---|---|
| direct period (median) | 33.3 | 33.8 | 35.7 |
| landed period, fresh graph per good | 49.3 | 48.1 | 51.5 |
| landed period, reweight | 43.5 | 42.3 | 47.1 |
| field alone, before / after | 13.9 / 9.2 | 15.6 / 9.0 | 16.8 / 11.4 |
| allocation of a landed period, before / after (KB; direct 3,123) | 9,965 / 3,145 | same | same |

So: the field fell from 14 to 17 ms to 9 to 11 ms (about 32% to 42%) and from 6.8 MB to 19 KB a field; a landed period costs about 8 to 11 ms more than a direct one (it was 14 to 16), and allocates 22 KB more than a direct period (it was 6.8 MB more). The profile before the change (loaded machine, original code) put the field at 17.4 to 19.1 ms, so against the code as it stood the gain is about 8 to 9 ms. **What is still slow:** the field is about 9 ms, of which 6.5 to 7.4 ms is the per-route cost arithmetic (re-measured after the change: costs 7.3, reweight 0.7, Dijkstra 1.6). The review's 1 to 3 ms estimate is still out by three to nine times. Two more steps would go further and were not taken: cache the `Int128` conversions per (market pair, good) between periods, or keep a route's cost across periods and recompute only where a price or policy moved; both add invalidation state and the first is a different change from the one asked for.

**Tests.** `LandedTests`: goods that trade in different sets of markets (3 sets over 12 goods, and 12 sets over 14 goods, so topologies are both shared and replaced) on 25 random worlds each, two price passes on the same runtime, every field equal to enumerating every simple path on only that good's markets and routes (the same oracle as the single-good test, 1,000+ pairs compared per case); the field allocates under 256 KB. EconomyEngine 465 to 468. `docs/ENGINE_GRAPH.md` section 9 has the library's tests.


## 13. Pass 46: production chains, document format 3 (2026-10-29)

Work item 7 of `docs/ENGINE_ALGORITHM_REVIEW.md` (section E2: Leontief chains by a topological pass, not a matrix inverse). Written after the code; results in 13.6 to 13.9.

### 13.1 The owner decision (made before coding, by reading `docs/RESOURCE_RECIPE_CATALOG.md` and `docs/ALCHEMY_ENGINE.md` in full)

Recipes are authored **once**, in the catalog (`GameContent.Catalog`). AlchemyEngine cannot carry production: its recipe has kinds only, no amounts, rates, time or stations (catalog matrix: "Amounts, ratios: reduced"). SimEngine already runs a recipe per entity with exact amounts. The Economy runs it per market. So the Economy has **no recipe format of its own to author**: its `recipes` array is **generated** by a third converter (`GameContent.Catalog.Economy`, `docs/RESOURCE_RECIPE_CATALOG.md` section 11) with the same capability levels as the Alchemy and Sim converters, and a document may also be written by hand in the same shape. AlchemyEngine's role is discovery only. One recipe runs in one layer (SimEngine or Economy): `RecipeOwnership` declares which, the converters obey it, and `EconomyRecipeLayers.Check` refuses a game that composes both for one recipe (`ECON_RECIPE_DOUBLE_OWNER`).

### 13.2 The document (format 3)

Strict JSON as before. Format 2 is refused **by its own code**, `ECON_FORMAT_2` ("format 2 is no longer read"), before anything else is read; format 1 and any other format keep `ECON_FORMAT`; a save of format 2 is refused with `ECON_SAVE_FORMAT`. There is no migration; the two samples that were format 2 (`town.economy.json`, `isles.economy.json`) were edited by hand. `ECON_RECIPES` is **retired** (its meaning, "the array must be empty", is gone); recipe refusals are `ECON_RECIPE` (a good named twice on one side of a recipe), `ECON_RECIPE_CYCLE`, and the ordinary `ECON_RANGE`, `ECON_DUPLICATE`, `ECON_REFERENCE`, `ECON_MISSING_FIELD`, `ECON_UNKNOWN_FIELD` for the rest; `ECON_LINE` (a market's line for a recipe whose goods the market does not trade). Calls add `ECON_UNKNOWN_RECIPE`, `ECON_RECIPE_OPEN` (unlocking a recipe the document never locked) and `ECON_NO_LINE` (explaining a line a market does not have). The engine declared 43 codes when this was written (pass 46; later passes added more, and `EconomyCodes` declares 49 in pass 57, counting its constants); the refusal matrix still requires every one to be reached.

- `recipes[]`: `id`, `key`, `inputs[]` and `outputs[]` (1 to 8 each, `{good, amount}`, amount 1 to 1,000,000, a good once per side), `duration` (0 to 64 periods; 0 means the outputs join the stock at once), `locked` (true: the recipe does not run until it is unlocked by a request).
- `markets[].lines[]`: `{recipe, capacity}`: the market runs the recipe at most `capacity` times a period (1 to 1,000,000). This is the **per-market capacity / throughput**: the same recipe can be a large line in one market and a small one in another, and a market with no line does not produce it. The market must trade every good of the recipe (`ECON_LINE`); one line per recipe per market.
- A recipe that consumes nothing is not a recipe (a market makes goods from nothing with its pair's `production`: a second owner would be a duplicate); a recipe that consumes a good it also makes is a cycle of one.

### 13.3 The model

**Order.** At load, the recipes become a graph (recipe A before B when A makes a good B consumes) and `EngineCommon.Graph` gives the topological order (`TopologicalSort`, new, `docs/ENGINE_GRAPH.md` section 10; lowest node index first, nodes numbered by the ordinal rank of the recipe id, so a reordered document changes nothing). A cycle is refused with `ECON_RECIPE_CYCLE`, naming each link ("f makes q for g, g makes p for f"). Cycles are refused even where a one-period delay would make them well-defined: the owner chose an acyclic graph, which keeps each period an exact, bounded pass.

**One period.** In `ClosePeriod`, after shipments arrive and before prices move: (1) **Deliver**: batches whose period has come put their outputs into their market's stock (what does not fit the warehouse is lost, as for a shipment) and count as supply; (2) **Produce**, market by market (production never crosses markets), each market's lines in the order of their recipes. A line's runs are `min(capacity (cut by the arrears penalty while the market owes wages), for each input stock / amount, for each output free room / amount)` where free room is the warehouse less the stock **less what batches on their way will bring** (`PairState.Incoming`, derived from the batches, not saved), so a batch that is started always has its room kept. The input or output that allows the fewest runs names the limit (an input before an output on a tie, and the capacity before both). The inputs leave the stock now; the outputs join it now (duration 0) or become a batch due `duration` periods on. Prices then respond through the **existing** rule: the units a run consumed count as spot demand for the period and the units it made as spot supply, in the pair's `SpotDemand` and `SpotSupply` (saved state that already existed for traders), so a chain's scarcity and glut move the prices of its goods without a second price rule. All integer; no float; no random.

**Production state** (saved, covered by the digest): the unlocked flag of each recipe; per line the runs started last period, the limit (locked, capacity, input, room) and the good that set it; each batch on its way (market, recipe, runs, completion period). Save format 3, digest text `economy-state-3`. Ranges are checked before the digest as before (a line's runs within its capacity, a limit that fits its recipe, a batch inside its line's capacity and its recipe's duration, one batch per line and completion period, a locked recipe never run); `Incoming` is rebuilt from the batches on open (tested equal).

**Unlocking.** The document says which recipes are `locked`; whether one has been **unlocked is state**, changed by a request (`Runtime.UnlockRecipe`, and in the game layer `EconomyRecipeUnlockRequested`, answered in `Update` with `EconomyApplied` and `EconomyRecipeUnlocked`; journaled as `unlock`, so a replay reproduces it). Chosen over "an unlocked set in the document" because a document is immutable and identified by its digest: discoveries happen during play and must be saved, replayed and checkpointed, which a changed document would defeat. An unlock of an already unlocked recipe is applied and changes nothing; of an unknown recipe or one the document never locked, refused. **Alchemy's role is discovery only**: `EconomyRecipeUnlocks` (bridge) turns an alchemy `DiscoveryMade` of a recipe the economy has and locked into that request. Nothing else of alchemy enters the economy.

**Explain.** `EconomyExplain.Production(runtime, market, recipe)` is the trace: whether the recipe may run, the line's order among its market's lines, its capacity (and any arrears cut), for each input how many runs its stock allows **at this line's turn** (after the lines before it took theirs), for each output how many its room allows, the runs and what limited them ("short of coal", "no room for steel", "the line's capacity", "the recipe is locked"), what the line did last period and its batches. It uses the same `PlanLine` the period uses and changes nothing. CLI: `explain <economy> <state> production <market> <recipe>`; `check` lists the recipes in the order they run and the lines.

**Events** (game layer): `EconomyProductionLimited` (a line's limit changed, once per change, not per period), `EconomyRecipeUnlocked`; both in the transcript; request `EconomyRecipeUnlockRequested`.

### 13.4 The sample and the demo

`town.economy.json` is now format 3 with six new goods (flour, bread, ore, coal, steel, tools), five recipes and five lines: Riverton mills grain to flour (3 to 2, one period, 260 runs) and bakes bread (2 flour to 3 bread, 150 runs); Hillfort smelts steel (2 ore + 1 coal to 1 steel, 150 runs) and smiths tools (2 steel to 3 tools, two periods, 60 runs); and a **locked** `alloy-steel` (3 coal + 1 ore to 2 steel, 40 runs) that the story unlocks on day 35 by a request. Hillfort's coal production is 150 a period against up to 270 the lines can use (150 for the smelter, 3 x 40 for the alloy), so the smelter runs short of coal and says so; the drought of day 20 leaves Riverton's mill short of grain.

| Run | Before (pass 44, format 2) | Now (format 3 + production) |
|---|---|---|
| `--days 120` (default) | `67BA46D30317B3E5F3A3344940E16AAC5F3522A52752DAB82D15C0335B80D65B`, 3,349 events | `609B0FFD78F26829C1E4E30688FA7BF4293B174426EF7A118429301EC1D7000E`, 6,068 events |
| `--days 20 --checkpoint-day 7` | `1A2A0CA38A21F5E8B4574C1DA977EB11A5C4D3B6286C445925FB6E1BD0AA6E2E` | `E7DAFAE9059DA2FC28B8ED07C53725203DDE7999A63EDB60B1F98540FAAE3F95` |

Both hashes legitimately change, for three reasons stacked: the document is a different text (the state digest includes the document's digest); the economy has ten goods, five recipes and the story's unlock; and the demo prints more (the day rows now show ten goods by their first two letters, the end shows each line, events for limits and the discovery). Each was run twice with the same hash; the replay from the checkpoint (ticks 57,631 to 172,800 of the default run) ends in the same state and raises the same events (3,719 events). The Diplomacy demo (`19D3184D3B71968A0CD61ABAA4EA242AA40407C6EF74F1FDAB8C9B1E473B025D`) is **unchanged** (run twice after its sample gained format 3 and `lines: []`; replay same). The world-events demo, which runs the town economy beside the world, now prints `BCC24E3492F049719DCD7699B237C421554C52EA4BECBD5A306C1B081CE22A2C` (twice the same, replay same, 441 events), against `CC2DBBD7...2193A` in section 11.5: the town economy it runs changed; the world-events side was not touched by this pass.

### 13.5 Bridges

Calendar: unchanged (periods and dates as before; tests pass). AI: `EconomyAiBridge` tells about the goods the game names (`goods` argument; null still means every good): **a percept kind holds at most 16 attributes and each good takes three, so an economy of more than five goods cannot be told about whole**; this was true before (the sample had four goods) and the ten-good sample exposed it; the AI tests name the four original goods. A production-margin percept was **not** built (see 13.9). Catalog: the converter and `EconomyRecipeUnlocks` / `EconomyRecipeLayers` above. Ledger and dialogue: unchanged (the ledger vocabulary now lists the ten goods).

### 13.6 Tests (independent oracles)

`EconomyEngine.Tests` 468 to 524 (+56): `ProductionTests` (44), `ProductionScaleTests` (2), one CLI test, and the rest refusal-matrix rows and format tests.

- **Hand-worked chain**: ore 100, coal 30; smelt (ore 2 + coal 1 to steel 1, one period, 40 runs) and smith (steel 2 to tools 3, at once, 10 runs); the stocks of every good after periods 1, 2 and 3 (40/0/0/0, 40/0/10/30, 40/0/0/45), the limit of each line, the batch, and the limit-change events, declared in the opposite order to the one they run in.
- **Brute-force oracle** (`ProductionOracle`, shares nothing with the engine: orders recipes by a quadratic "lowest ready id" scan, finds a line's runs by trying them from the capacity down): 120 fixed-seed random acyclic worlds (7 goods, 3 to 7 recipes with 1 to 3 inputs and 1 to 2 outputs, durations 0 to 3, 2 to 3 markets, every third world with 150-unit warehouses) over 14 periods: stocks, batches (market, recipe, runs, completion) and every line's runs equal the oracle's each period (1,680 period comparisons; more than 200 line-periods held below capacity are asserted so partial runs are exercised).
- **Conservation**, every period of those worlds: for each good, the change of (stock + outputs on their way) equals the sum over lines of runs started x (made - taken); money conserved in every currency.
- **Cycles**: of two, three and one recipe, one behind an acyclic prefix, and an acyclic diamond accepted; the message names the links. **Order**: 24 shuffles of the document give the same order, ties by id.
- **Shortage and limits**: partial run (5 ore, 2 a run: 2 runs), empty run, the ties (capacity, then input, then room), a full warehouse, room kept for a batch on its way and used by it, an arrears cut (40 runs to 28), a delivery into room a trader filled meanwhile; the explanation's text for each.
- **Price response**: a chain that takes grain raises the grain price and lowers the flour price against a same-process control without the line.
- **Save and replay**: opened after every period of 12 random worlds ends where the run that never stopped ends; a batch in flight and the unlocked set survive and `Incoming` is rebuilt; the digest changes with a batch, a line and the unlocked set, and with a batch's completion period (a mutation found it missing); 19 forgeries with recomputed digests are refused as state and the range checks fire before the digest; component level: unlock requests, refusals by code, limit events once per change, journal and replay with an unlock, a checkpoint with batches in flight.
- **Catalog**: `GameContent.Tests` 356 to 404 (+48): the matrix, the chain composed and run, ownership, every shipped pack converts and loads, the Cli. **Bridge**: a discovered alchemy recipe unlocks the locked catalog recipe and the economy then runs it (30 runs, coal-limited, 45 tools); an undeclared or open discovery is ignored; a game with both layers for one recipe is refused. `EngineCommon.Graph.Tests` 78 (6 new, `TopologicalSortTests`). `GameRuntime.Economy.Tests` 120 to 128.

### 13.7 Cost (Release, this machine, 200 markets x 100 goods, 400 routes; 30 recipes in three layers of ten run by 50 markets = 1,500 lines; same-process, interleaved with the same economy without lines; 3 runs of 15 repeats; `docs/measurements/economy-production-2026-10-29.csv`)

| | run 1 | run 2 | run 3 |
|---|---|---|---|
| direct period without / with production (median, ms) | 35.3 / 38.2 | 48.3 / 52.0 | 53.3 / 56.8 |
| difference | **+3.0 ms** | +3.8 ms | +3.5 ms |
| landed period without / with (median, ms) | 49.9 / 56.4 | 59.6 / 70.8 | 75.7 / 86.4 |
| difference | +6.5 ms | +11.2 ms | +10.7 ms |
| allocation of a period without / with (KB) | 3,122 / 3,310 (direct) | 3,122 / 3,309 | 3,124 / 3,308 |

Production adds **about 3 to 4 ms to a direct period (7 to 8 percent) and 6 to 11 ms to a landed one**; the landed difference is larger because the chains' outputs are new goods flowing along the routes, which the landed field and the loading rule then handle; it is not production's own cost. Runs 2 and 3 were on a more loaded machine (the control is slower too), which is why the same-process difference is the figure to read, not the absolute periods. About 188 KB more allocation a period. Planning every line of every market for the explain path costs 0.14 to 0.68 ms (median). A save with about 2,000 batches in flight is 6.0 to 6.4 MB; write took 96 to 632 ms and open 130 to 702 ms (one sample per run, on a machine that got busier from run to run; the same save without production was 35 to 159 ms for write in sections 10.4 and 11.6, and the existing `ScaleTests` budgets of 500 and 1,000 ms still pass, but a loaded machine can pass 500 ms on a 6 MB save). The review estimated under 2 ms; the measurement is 3 to 4 ms, about twice the estimate.

### 13.8 Mutation checking

32 mutants of only the new logic (the production pass `EconomyRuntime.Production.cs` 13, the recipe reader 4, the state rules 3, the image, restore and explain 3, `TopologicalSort` 2, the Economy converter 7), by a runner outside the tree working on a copy of `src`, restoring each file and comparing its SHA-256 after every mutant, with a 300 s timeout per test run; the unmutated copy built and passed first (EconomyEngine 253 of the focused classes, Graph 8, GameContent 42). Only the focused classes were run for each mutant. **Round 1: 24 killed, 8 survived** (none failed to compile, none timed out). The survivors: `fit >= runs` as `>` in the output-room test (a tie between the room and the capacity or an input was untested), the Deliver clamp, the batch completion range's upper end (`+ duration + 1`), `Duration < 0` for `< 1` in the state rules, the digest text without a batch's completion period (a real hole: only the run count was covered), the batch search `k < 1000` (a recipe needing exactly 1,000 catalog runs), `periods >= 64` (a duration of exactly 64), and Energy not a good. New tests were written for six of them (`WhenCapacityInputAndRoomAllowTheSameRuns...`, the one-beyond forgery and `ABatchesCompletionPeriodIsPartOfTheDigest...`, the `t.milli` recipe, `ADurationOfExactlySixtyFourPeriods...`, `EnergyFluidsAndGases...`), and the six re-run: **all killed**. **Two remain, judged and not killed:** (a) `recipe.Duration < 0` for `< 1`: equivalent, because a batch of a no-time recipe has an empty completion range (`period + 1` to `period`), so the next check refuses it anyway; (b) removing the warehouse clamp in `Deliver`: the price rule clamps the stock again in the same period (`Math.Min(capacity, ...)` in `UpdateMarket`), so the stock is the same either way; the clamp's only effect is that lost units are not counted as supply in that period's price, which no test observes (a test for the stock exists and passes with or without the mutant). Final: 30 of 32 killed, 1 equivalent, 1 unobserved. Not mutated: the tests, the CLI wording, the component's new handlers, `EconomyRecipeUnlocks`, `EconomyRecipeLayers`, and the explanation wording beyond what the tests assert. **Guard check:** the shared Roslyn guards caught a real `?? throw` I wrote in `EconomyRecipeUnlocks` (fixed); they cover the new Core files with no finding.

### 13.9 Not done, and why

- **A production-margin AI percept** ("if cheap"): not cheap. **Built in pass 49 (15.5)** with several percept kinds; this paragraph is the reasoning that was superseded: Each percept attribute is declared per good by the AI document, the percept kind is capped at 16 attributes, and a margin per recipe would need another declared attribute per recipe in every AI document that already exists. The current percept (`ratio_*`, `short_*`, `reach_*`) already shows a merchant the price effect of a chain.
- **No margin throttle** (**built in pass 49, 15.3**): a line runs at its capacity whatever the output is worth, and stops only for inputs or room. A market therefore keeps producing a good whose price has fallen to its floor (flour in the demo piles to 18,000). Throttling by margin would be a new rule with its own state; not asked for.
- **Production does not read shocks or the supply modifiers** (**changed in pass 49, 15.4: it does now, when the outputs arrive**): a drought cuts a pair's own `production`, not a recipe's output; chains feel it through their inputs. Only the arrears penalty cuts a line. Said here so it is not mistaken for a supply shock.
- **Workers, wages and energy are not inputs of a run** (**workers and energy are ordinary goods a recipe may list, shown in `workshop.economy.json`, 15.6; tools and catalysts became `requires`**); a recipe's catalysts and tools are not modelled (the converter says so). Station levels, skills and quality are refused by the converter.
- **Cycles are refused, not solved** (**still so; the reason is in 15.6**): a feedback chain (a recycling loop) needs a different model.
- **Batches are not reduced by a blockade or a shock** once started.
- **The Godot adapter, other locales, other operating systems**: none; Windows only.
- **Not run (by the focused-tests rule):** the full solution build; DialogueEngine, GameRuntime (except `RelationshipCharacterTests`), Relationship, Calendar, AI, Combat and ScenarioEngine suites; Release builds of every test project (the Release scale tests were run for the Economy only).

## 14. Pass 48 (2026-10-30): the multi-seed sweep in the Economy tool

`EconomyEngine.Cli simulate <economy.json> <scenario.json> [<periods>] --seeds <n> [--from-seed <s>] [--days <d>] [--metrics a,b,...] [--rows]`. The shared reducer, the report format, the refusal codes, the research (with its reached, snippet and unreachable statuses) and the offline tail tool are in `docs/WORLD_EVENTS_ENGINE.md` section 12; this section holds what is the Economy's. Nothing in `EconomyEngine.Core`, the format, the saves or the sample changed; the single-run `simulate` output is byte-identical (`Simulate` and the sweep share one private `Live` loop), and the headless demo hash `609B0FFD78F26829C1E4E30688FA7BF4293B174426EF7A118429301EC1D7000E` is equal after the change.

**The horizon** is `<periods>` or `--days D`; `--days` is converted with the document's `stepsPerDay` and `periodSteps` and refused (`BAL_DAYS_RANGE`) when it is not a whole number of periods (a test uses a copy of the town with `periodSteps` 48: 3 days refused, 4 days is 2 periods).

**Metrics** (sampled at the end of every period; ratios are per mille of the reference price; names use the market and good ids):

| Name | Meaning |
|---|---|
| `ratio.min.<market>.<good>`, `ratio.max.<market>.<good>`, `ratio.end.<market>.<good>` | lowest, highest and final price ratio |
| `band.<band>.periods.<market>.<good>` | periods that ended with the pair in that price band (the "band breach" count for the bands outside `fair`) |
| `short.periods.<market>.<good>` | periods that ended short of the good |
| `arrears.periods.<market>` | periods that ended with the market in arrears |
| `treasury.end.<market>`, `money.end.<currency>`, `rate.end.<currency>` | final treasury, money and rate |
| `changes.arrivals`, `.bands`, `.blocks`, `.launches`, `.loans`, `.rates`, `.settlements`, `.shocks`, `.shortages` | counts of the engine's change records |
| `scenario.refused` | scripted calls the engine refused |

### 14.1 Findings (50 seeds from 1, 365 periods of the town and the isles economy; `docs/measurements/balance-sweep-economy-town-isles-2026-10-30.csv`, summary in `balance-sweep-summary-2026-10-30.csv`)

- **The shipped documents are seed-insensitive: this is the main finding, and it is a fact about the documents, not noise.** Both set `price.noise` to 0, and the Economy draws a random number only for that noise (`EconomyRuntime.Market.cs`), so in the town (196 metrics) and the isles economy (184) **not one metric differs between seeds**: every minimum equals its maximum. A multi-seed sweep of an Economy document with `noise` 0 measures nothing about the seed; the sweep says so by printing equal minimum and maximum, and a test pins it for the town (8 seeds, every metric).
- **What the sweep does show, for the same reason, is structure.** In the town with its sample scenario, a year spends 714 of 8,030 pair-periods (8.9%) in the `fair` band; 20 of the 22 pairs are `fair` for under half the year, and 6 pairs sit at the 4,000 per-mille ratio at some point (hillfort ale, bread and cloth; riverton cloth; seaport bread and grain). Riverton ends 296 of 365 periods and seaport 240 in arrears; hillfort never. Two caveats: this is one scenario (a drought on day 20, a devaluation on day 45, a decree on day 60), and the year includes the settling from the document's initial stocks, which I did not separate from the long run. The demo is a demonstration, not a balanced game economy; whether the shipped numbers should be rebalanced is a design decision this pass does not take.
- **With noise, the seed does matter, and the spread is real but small against the structure.** On a scratch copy of the town with `price.noise` 60 (the document in the repository was not changed): 71 of 196 metrics vary. Bands: periods in `scarce` summed over all pairs 2,096 to 2,176 across seeds (median 2,135), in `dear` 1,828 to 1,936 (median 1,878). Seaport iron has the widest spread: highest ratio 2,576 to 3,824 (median 3,035, median interval 2,896..3,240), final ratio 2,022 to 2,385. Shortage starts 116 to 137 (median 126), band changes 137 to 157. So at noise 60 the year-to-year breach count varies by about 2 to 4% while the demo's imbalance is 90% of pair-periods. On a scratch copy of the isles economy with an empty scenario and noise 60, `fair` periods rise from 412 to 1,747 of 7,300: noise dithers prices across a band edge, so noise changes the **average** occupancy of the bands as well as the spread; a document author should not assume a noisy economy is the quiet one plus jitter.
- **Cost** (Release, `town.economy.json`, 365 periods): 200 seeds in 10.5 s (about 52 ms a seed).

### 14.2 Tests, mutation, not done

`EconomyEngine.Tests.BalanceCliTests` 13: one seed at the document's seed equals the single run's printed treasury, final price ratios (22 pairs), rates and money; same arguments, same bytes (rows included); `--days` equals the positional horizon; days that are not whole periods refused and whole ones converted (heading `horizon 2 periods`); noise 0 gives no spread; noise 60 does; refusals with exit 2; a bad scenario still exit 2. The shared mutation round is in `WORLD_EVENTS_ENGINE.md` section 12.6; the Economy's own `LiveOneSeed` and the days conversion were mutated there (M29 to M31).

Not done: separating the settling transient from the long run in the town findings; rebalancing the sample; a metric per route or per loan; the `GameRuntime.Economy` bridge is unchanged.

## 15. Pass 49 (2026-10-30): noise, margin throttle, shocks on recipe output, AI percepts, chain gaps, save cost; document and save format 4

The follow-ups left by 13.9 and 14.2, in the order of the task. Everything here changes meaning, so **the document format is 4 and the save format is 4**; format 3 is refused by name (`ECON_FORMAT_3` for a document, `ECON_SAVE_FORMAT_3` for a save), exactly as format 2 was by `ECON_FORMAT_2`. There is no migration. What format 4 changes: (a) a supply modifier or shock now acts on what a recipe makes (15.4), so a format 3 document with shocks would silently behave differently; (b) the optional recipe field `requires` and the optional line field `throttle` (15.3, 15.6); (c) `LineLimit` gains `Held` and `Margin` (saved, so the save range grows); (d) a save groups batches by line and writes a pair as ten numbers (15.8); (e) the digest text is `economy-state-4`.

### 15.1 Research (searched 2026-10-30; reached, snippet and unreached are stated per source)

| Source | Status | What it was used for |
|---|---|---|
| OpenStax, *Principles of Microeconomics* 3e, 8.2 "How perfectly competitive firms make output decisions" (openstax.org/books/principles-microeconomics-3e/pages/8-2-how-perfectly-competitive-firms-make-output-decisions) | **reached** (fetched) | A competitive firm shuts down when the price is below its minimum average variable cost, and its marginal cost curve above that point is its supply curve: output rises with the margin of price over marginal cost. Adapted, not copied: the Economy has no cost curves, so a line's **margin** (output value over input cost) takes the place of price over marginal cost, and a bounded linear response between two margins takes the place of the curve. |
| Course notes on producer surplus and the shutdown rule (Reed College Econ 201 lecture notes and study-guide pages, a search result) | snippet only (not opened) | Producer surplus = revenue less variable cost; the same rule as above. Consistent with the OpenStax page; nothing was taken from them alone. |
| Paradox forum, Victoria 3 dev diary 5 (production methods) and Steam community threads on unprofitable buildings | snippet only (not opened) | Players describe an unprofitable building as a visible state to be fixed by changing production methods or demand. Adopted as a design principle only: profitability must be a number the player can read (the `margin` term of the trace). No formula is claimed for Victoria 3. |
| Dwarf Fortress wiki, Reaction pages (v0.34, 40d, Reaction examples) and bug 9106 | snippet only; a direct fetch of the wiki page failed with HTTP 429 | A reagent flagged `PRESERVE_REAGENT` is needed and kept (the examples use containers), and a bug report says stack handling of a preserved reagent was disputed. Used only as evidence that a "needed but not consumed" input is an established simulation mechanic; the stronger reason to build it is the catalog itself, which uses catalysts and tools in 17 of 28 packs (23 recipes with catalysts, 85 with tools) (15.6). |
| Anno, Factorio | **not searched** | Said so rather than citing them. |
| Input-output model (the Leontief inverse and the Hawkins-Simon condition), already in 13.1 and review source 6 | reached in pass 46 | Why cycles stay refused (15.6). |

### 15.2 Sample noise (item 1)

`price.noise` was 0 in both shipped documents, so a `simulate --seeds` sweep measured nothing (14.1). Chosen from measurement, not by taste: 50 seeds, `town.economy.json` for 120 periods and `isles.economy.json` for 365, noise 0, 20, 40, 60 and 100 (`docs/measurements/economy-noise-pass49-2026-10-30.csv`, produced by `simulate --seeds` of pass 48).

| noise | town: metrics that vary of 196 | town: pair-periods in `fair` (sum of means, of 2,640) | town: mean spread of final price ratio (per mille) | isles: metrics that vary of 184 | isles: pair-periods in `fair` (of 8,030) |
|---|---|---|---|---|---|
| 0 (before) | 0 | 423.0 | 0 | 0 | 412.0 |
| **20 (shipped)** | **80** | 431.3 | 241 | **41** | 711.0 |
| 40 | 79 | 435.5 | 224 | 41 | 1,728.5 |
| 60 | 81 | 439.6 | 264 | 44 | 1,734.4 |
| 100 | 86 | 439.2 | 305 | 49 | 1,460.6 |

Twenty already gives the whole spread the town can show (80 of 196 metrics; more noise adds only a few) and moves the town's `fair` occupancy by 2 percent. In the isles economy noise shifts the occupancy a great deal at any level (412 to 711 at 20; 1,728 at 40: it dithers prices across a band edge, the effect 14.1 warned about), so the smallest level that makes the seeds differ is the right one. **Both documents now have `price.noise` 20.** The Diplomacy demo hash changes with it (15.9). The old `BalanceCliTests` that pinned "the town has no spread" now run on a copy with noise 0, and a new test pins the shipped town's spread.

### 15.3 The margin throttle (item 2)

A line should not run at capacity when its outputs are worth less than its inputs cost. The rule, deterministic and integer, declared per line in the document (`"throttle": {"stop": 900, "full": 1200}`; absent means the line never looks at a price, which is how every format 3 line behaved):

- **Margin** `r` = (sum over outputs of amount x price x supply factor) x 1,000 / (sum over inputs of amount x price), per mille, at the prices the market has when the period starts; 1,000 is break-even. The supply factor is the one that acts on the output (15.4), so a shock that halves what the line makes halves its margin and the throttle follows.
- **Fraction** `f` = 0 at or under `stop`; 1,000 at or over `full`; (r - stop) x 1,000 / (full - stop) between (truncated). `stop` 0 to 99,999, `full` 1 to 100,000, `stop` under `full`.
- **Runs wanted** = capacity (less the arrears cut) x `f` / 1,000, truncated; then cut by inputs and room as before. While `f` is under 1,000 the limit is **`Margin`** (a new `LineLimit`); an input or room limit that bites harder still names itself, and on a tie the margin is named, as the capacity is.
- **No state**: the margin is computed from today's prices, so nothing is saved for it beyond the line's limit; the prices already are state.
- **Trace**: `explain ... production` prints `margin <recipe>: <r> (per mille: one run's outputs are worth X against inputs that cost Y ...; the line stops at S, runs in full from F, so it runs f per mille of its capacity)`.
- **Not in the margin**: wages (they come from the treasury's budget), sales tax and subsidies (a pair's own supply response counts a subsidy; a line's margin does not). Said so that it is not mistaken for a full profit. **Pass 51 (section 16.2) adds the document-declared profit margin** (a wage and an overhead per run, `costs`) and records why sales tax, subsidies and tariffs are still not terms of a line.

**Why this rule** (15.1): producer surplus and the shutdown rule say output follows price over marginal cost and stops below the cost of variable inputs; a line has no cost curve, only a capacity and one recipe, so the margin over its variable inputs (its only variable cost) is the exact analogue, and a linear band between a stop below break-even (900: a line tolerates a small loss, as a firm covers some costs) and a full-speed margin (1,200) gives the smooth response of a supply curve rather than a cliff. The response is self-correcting: a stopped mill leaves grain unsold and its price falls, and flour is no longer made so its price rises, so the margin returns and the line restarts.

**Measured** (`docs/measurements/economy-throttle-pass49-2026-10-30.csv`; the town, day 120, the document's own seed). Before (format 4 without a throttle, noise 0): riverton's flour stock **12,370** at its floor price 750 (pass 46 recorded about 18,000 before shocks acted on recipe output), mill 182 runs and bakery 105 in the last period. With the shipped throttle (900 to 1,200) on all five lines and noise 20: flour **1** at price 3,269, bread 341, mill and bakery 36 runs each. A looser band (800 to 1,100) gives flour 0 and mill 22 runs: the equilibrium depends on the band, which is what a document author tunes. The demo is still a demonstration, not a balanced economy; hillfort's ore still piles (13,383) because ore is a pair's own `production`, not a recipe output.

### 15.4 Shocks and supply modifiers on recipe output (item 3)

A pair's own supply is `production x price response x modifier x shock x arrears cut`. A recipe's output now takes the same two middle factors: **units made = ((runs x amount) x modifier / 1,000) x shock / 1,000, each step truncated, in that order** (`EconomyRuntime.Yield`), where the modifier and the shock are the supply multipliers of that output's good in that market and period (the same `Shocked` and `Modifiers` the pair uses, selected by good id or tag). Decisions:

- The inputs are consumed in full (a drought or a strike does not return the grain); the shock cuts what comes out. The throttle sees the reduced output (15.3).
- **When**: the factors of the period in which the outputs join the stock. A run with no duration joins at once, in its own period; a run with a duration arrives in the period it completes and meets the shocks of that period, as a shipment meets the blockade of its arrival period. A batch therefore needs no saved yield, and a shock that ends before a batch arrives does not touch it. (The alternative, fixing the yield at the start, would need a saved factor per batch.)
- **Boosts**: a factor over 1,000 (a harvest modifier) can make more than the room reserved when the run started; the stock is cut to the warehouse and what does not fit is lost, as a shipment's surplus is. The plan sees the same cut, so a later line of the same period sees the same stock.
- **The arrears penalty is different on purpose**: it cuts a line's **runs** (inputs and outputs both), as before; for a pair it cuts supply. Said here so the two are not mistaken for one rule.
- **Trace**: `yield <good>: <thousandths of a unit a run makes> (... supply modifiers M per mille, shocks S per mille, applied when the outputs arrive)`; `explain` and `PlanLines` take optional host facts (the runtime keeps none), and without them the trace says "modifiers not applied: no host facts were given".

### 15.5 AI percepts (item 4)

An AI percept kind holds at most 16 attributes (`AiReader`, `attrs` 0 to 16) and a good takes three, so the old bridge could tell about five goods. Solved by **several kinds, never by cutting**: `EconomyAiBridge` takes goods percept groups (`EconomyGoodsPercept(kind, goods)`, at most 5 goods) and margin percept groups (`EconomyMarginPercept(kind, recipes)`, at most 16 recipes, one attribute `margin_<recipe>` each, per mille of the line's margin in the perceiver's market, 0 where the market has no line); `PlanGoods(set, prefix)` and `PlanMargins(set, prefix)` split a whole document into kinds named `prefix`, `prefix-2`, ... in document order. The constructor checks **each kind against the real AI document**: the kind exists, is not owned, declares exactly the attributes of its group with the right types and ranges; a group over the limit, a good or recipe named twice, two groups with one kind and an unknown id are refused with `ECONAI_PERCEPT_KIND`. Each agent is told each kind when its content changes (the told state is per agent and kind). The margin the percept carries is the prices' margin with the shocks that are on; host-fact modifiers are not applied because the bridge is not given the facts. Tests load real AI documents with `AiLoader`, show that 17 attributes do not load and 16 do, tell all ten goods and five margins of the town in three kinds, and let a merchant act on a margin.

### 15.6 Chain gaps (item 5)

- **Cycles stay refused.** A recycling loop needs a model of its own: a closed input-output system has a solution only when the Hawkins-Simon condition holds, and a per-period pass over a cycle would need either an arbitrary break (a one-period delay chosen by the engine) or a fixed-point iteration whose bound is a design decision the owner has not made (13.3). The refusal is also what keeps each period an exact, bounded pass. `ECON_RECIPE_CYCLE` names every link; it now also covers a cycle through a **requirement** (a tool made by the chain that needs it): "a makes hammer for b, b makes tools for a".
- **Workers and energy are ordinary goods.** No new mechanism: labour and fuel are goods (tags `labour` and `energy`) that a recipe lists among its inputs, produced by a pair's own `production` and consumed by lines, priced by the same rule, hit by the same shocks. `samples/workshop.economy.json` shows it: `forge-steel` takes ore, fuel and labour and requires a hammer; `make-nails` takes steel and labour; a `strike` shock cuts labour supply by 60 percent. A test runs it: both lines run at capacity in period 1; after the strike a line stops "short of labour"; with the hammer removed the forge stops with `Held` and names the hammer.
- **Catalysts and tools: a non-consumed required stock is warranted, and built.** Reasons: the catalog uses them (catalysts or tools appear in 17 of 28 packs (23 recipes with catalysts, 85 with tools), for example `core.hammer`, `alchemy.mortar`, `city.house-small`), and the converter would otherwise have to keep dropping them with a note; a tool is a standard simulation mechanic (15.1); and it needs no new loop, only a gate. Model: a recipe lists `requires` (up to 8 goods, whole amounts, a good once, never also an input or an output); a line runs only while the market's **stock holds at least that amount of each**, at its turn in the order (so what an earlier line of the same period made at once counts, what is on its way does not); nothing is used up; the first missing good is named (`LineLimit.Held`). It is a dependency like an input for ordering and cycle detection. Not modelled: tool wear (the catalog's `uses`), a tool shared by several lines at once (each line only checks the stock), and a tool that costs money to keep.
- **The catalog converter** (`GameContent.Catalog.Economy`, `docs/RESOURCE_RECIPE_CATALOG.md` section 11) now reports, per feature: **supported** (a catalyst or tool that is a priced tradable good becomes a requirement; no note), **reduced** (`CATALOG_ECONOMY_TOOL_WEAR_NOT_MODELLED`; `..._REQUIREMENT_NOT_A_GOOD` for one that is not a priced good, so the recipe runs without it; `..._REQUIREMENT_ROUNDED` for a fractional amount, rounded up; `..._REQUIREMENT_ON_OWN_GOOD` for one the recipe also makes; `..._REQUIREMENT_CYCLE` when it would close a cycle, so the requirement, not the recipe, is given up) and **refused** (`CATALOG_ECONOMY_TOO_MANY_GOODS` when catalysts and tools together pass 8). `CATALOG_ECONOMY_CATALYSTS_IGNORED` and `CATALOG_ECONOMY_TOOLS_IGNORED` are retired; the reduction they named is replaced by the codes above.

### 15.7 Document format 4 and the loader

`format: 4`. New optional fields: recipe `requires` (array of `{good, amount}`, absent means none) and line `throttle` (`{stop, full}`, absent means none). A document that has neither differs from format 3 only in its number, but shocks now act on its recipes' outputs, which is why format 3 is not read. `ECON_FORMAT_3` (documents) and `ECON_SAVE_FORMAT_3` (saves) name the old format and what changed; the save check reads only the `Format` property with a forward-only reader before the rest, so a format 3 save of another shape is still refused by name. Samples migrated: `town.economy.json` (throttles on all five lines, noise 20), `isles.economy.json` (noise 20), new `workshop.economy.json`. The CLI `check` prints `requires in stock ...` and `throttle stop S full F`; `explain production` gained the `requires`, `margin` and `yield` terms.

### 15.8 Save cost with thousands of batches in flight (item 6)

The brief assumed batches dominate the save. **They do not**: in the 200 markets x 100 goods world of `ProductionScaleTests` with 2,000 batches in flight on 1,500 lines (Release, one process, `SaveCostTests`, `docs/measurements/economy-save-cost-*.csv`), the batches are **86 KB of a 6.4 MB save (1.3 percent)**; the pairs were 2.6 MB (41 percent) and the price history 1.9 MB (30 percent). So two changes were made and measured separately:

1. **Batches grouped by line** (one entry per line with its completion periods and runs in ascending order; canonical and digest-covered; the order a runtime holds batches in is not state, because delivery adds the same units to the same stock whatever order it visits them, so the image sorts them): 102,228 bytes to 85,658 (**-16 percent of the batch section, -0.3 percent of the save**), the digest text 36,227 to 33,157 bytes. Serialising the nested form is slightly slower (0.99 ms against 0.40 ms for the section). **This alone does not move the 500 ms budget**; it removes the dependence of the digest on an arbitrary order and the "one batch per line and completion period" rule is now a structural one (ascending completion periods).
2. **A pair is ten numbers, not an object** (the property names were most of its bytes): pairs 2,609,750 to 649,752 bytes (**-75 percent**). Save size **6,391 KB to 4,477 KB (-30 percent)**.

| Release, medians of 7 (worst of 7) | before: batches grouped, pairs as objects | + pairs as arrays | + the format read by a forward-only reader (final) |
|---|---|---|---|
| save size | 6,391 KB | 4,477 KB | 4,477 KB |
| save write | 194 ms (293) | 189 ms (232) | 229 ms (255) |
| save open | 305 ms (568) | 220 ms (525) | **206 ms (437)** |
| where run | repository, SDK 10.0.402 | repository, SDK 10.0.402 | outside copy, SDK 8.0.427, serial |

The pairs were 2.6 MB of the 6.4 MB and are 0.65 MB now (-75 percent); the save is **30 percent smaller** and the **open is 32 percent faster at the median and 23 percent at the worst** (the pair objects no longer have ten property names each; `PeekFormat`, which looks at the `Format` property to refuse an old save by name, used to build a document model of the whole 4.5 MB and now skips to the property with a reader). The **write did not get faster**: building the image and its digest (about 70 ms) and serialising it (about 34 ms) are less than half of the 190 to 230 ms a write took, and the rest is the machine; the three columns are three runs on a machine other builds were using, so differences of 30 ms in a write are noise, and the last column ran in a different SDK. One run of the same test in the same process as two other timing classes gave write 578 ms and open 528 ms; it was parallel contention, is not a measurement and is not recorded in the table. Breakdown of the final open (median ms): deserialise 90, range rules 8, digest text and hash 55, restore and check against the document 15. The worst open (437 ms) is under the 500 ms figure here and was over it before (568); on a loaded machine it can still pass it, and the existing `ScaleTests` budgets (500 and 1,000 ms for their own saves) passed unchanged in the Release run. The remaining cost is the price history (1.9 MB) and the digest text, which would need a different history encoding or a cheaper digest, neither chosen here. Pass 46's figure (6.0 to 6.4 MB, write 96 to 632 ms, open 130 to 702 ms, one sample a run, 13.7) is in the same range as the first column.

### 15.9 Demo hashes

Each run twice with the same hash; the replay from the checkpoint ended in the same state and raised the same events. They all legitimately change: the town document is a different text (format 4, throttles on five lines, noise 20), the isles document too (format 4, noise 20), the Economy engine now scales recipe output by shocks and applies the throttle.

| Run | Before | Now |
|---|---|---|
| Economy demo, default (`--days 120`) | `609B0FFD78F26829C1E4E30688FA7BF4293B174426EF7A118429301EC1D7000E` (6,068 events; replay 3,719 events; pass 46, 13.4) | `562D70E230C82C2DD98A3B91CB66EA8DF62406B862B4BD4B110A66E5DD2F3FE6` (5,995 events; state `AFE4E0882A1BF168`, events `A6301791BFB55610`; replay of ticks 57,631 to 172,800: state same, events same, 3,734 events) |
| Economy demo, `--days 20 --checkpoint-day 7` | `E7DAFAE9059DA2FC28B8ED07C53725203DDE7999A63EDB60B1F98540FAAE3F95` | `438F8A057B5FF7ECC7106C8A2D933C3FD7E71A02D38C26ACC1ED9BA3EF1A577C` (1,069 events; state `CB5BA4ABCF37AD3C`, events `ADF804A1FDEC62F5`; replay of ticks 10,111 to 28,800, 785 events) |
| World-events demo (embeds the town economy) | `BCC24E3492F049719DCD7699B237C421554C52EA4BECBD5A306C1B081CE22A2C` (13.4) | `828C9303072970C1FE7B459DEA489BB271445A7BE129B12E3546CC66582BD314` (637 events; world state `CFAA39AB28E8DCEE`, events `93911E160FA0234C`; replay 441 events) |
| Diplomacy demo (embeds the isles economy) | `19D3184D3B71968A0CD61ABAA4EA242AA40407C6EF74F1FDAB8C9B1E473B025D` (unchanged through pass 46) | `8266A7D3101EFCC2783C6CF2AEB5685288C8BE7FFBE78E84131069A84A094029` (119 events; state `2FCB5DAB41B76128`, events `5E83E9058DF3BA75`; replay 33 events) |

`GameRuntime.Economy.Tests.HeadlessTests` pins the first two. **Where these were run:** the pinned SDK (10.0.402) stopped being usable on this machine in the middle of the pass (its .NET 10 runtime and host were removed by something outside the repository; `dotnet --version` failed in the repository and gave 8.0.427 outside it), so the demo runs below, the mutation round and the later test runs were made in a source-only copy outside the tree with SDK 8.0.427 (all projects target net8.0). The default Economy demo's hash prefix (`562D70E230C82C2DD98A3B91CB66EA8DF624`) was also seen earlier in the repository under SDK 10.0.402, in the one failed run of `HeadlessTests`; the full values are from the copy.

### 15.10 Tests, mutation, not done

**Tests** (focused, `scripts/focused-tests.ps1 -Changed src/EconomyEngine -List` selected `EconomyEngine.Tests`, `GameContent.Tests`, `GameRuntime.Economy.Tests`, `GameRuntime.WorldEvents.Tests`, `GameRuntime.Diplomacy.Tests`, `GameRuntime.Tutorial.Tests`; all six were run; no full suite, no solution build, no DialogueEngine `CostTests`, soldiers budget or Relationship `ScaleTests`).

- `EconomyEngine.Tests` 524 to **584**. New `MarginAndRequireTests` (every expected number worked by hand beside it): the throttle at nine margins around its stop and full (899, 900, 910, 1,050, 1,199, 1,200, 1,333) with the runs and the limit each gives, the margin as output value over input cost (2 ore at 4,000 and 1 coal at 2,000 against 12,000 is 1,200), a line without a throttle ignoring prices, the throttle shrinking only what is wanted (an input limit still names itself; on a tie the margin is named), the limit-change events raised exactly when the limit or good changed, a throttled chain not piling up a good worth less than its inputs against the same chain without the throttle, the explanation text, a shock and a modifier lowering the margin (1,200 to 720, 600, 360; a scale-400 shock gives 960 and 8 of 40 runs), every wrong throttle and requirement refused with its code, `requires` as a gate not a cost, a requirement made by an earlier line counted the same period and one on its way not, shocks and modifiers acting on a recipe's output and on a pair's own supply by the same half, composing with each factor truncating on its own (21 tools x 600 / 1,000 x 800 / 1,000 is 9, not 10), outputs under the shocks of their arrival period, a boost cut to the warehouse and the later line of the period seeing the cut stock, the yield and facts in the explanation, the workshop's workers, energy, strike and missing hammer, batches grouped by line with an order-independent digest, nine forged batch groups and the new limits refused as state, the pair as ten numbers with nine malformed forms refused as a save, and `ECON_SAVE_FORMAT_3`. Also `SaveCostTests` (the measurement), the CLI test for the workshop, the shipped town's spread, and the refusal matrix rows for `ECON_FORMAT_3` and `ECON_SAVE_FORMAT_3`.
- `GameContent.Tests` 404 to **408** (+4 in `EconomyConverterTests`: the tool as a held, never used good, a catalyst that is not a good or is the recipe's own, the cycle through a tool broken at the requirement, more than eight catalysts and tools together refused).
- `GameRuntime.Economy.Tests` 128 to 131 (+3 `AiIntegrationTests`: ten goods and five margins in three kinds with no attribute dropped, the margin percept and a merchant acting on it, the limit refused by the AI engine itself and the bridge's refusals); the two demo hashes in `HeadlessTests` updated; one `ProductionComponentTests` assertion now allows the margin step on the way to a shortage.
- `GameRuntime.WorldEvents.Tests` 111, `GameRuntime.Diplomacy.Tests` 160 and `GameRuntime.Tutorial.Tests` 127, all passing. **One test of another engine changed because the behaviour it pinned changed on purpose:** `EconomyIntegrationTests.ADroughtTheWorldMakesReachesTheEconomyAsAShockOnTheRegionsMarketAndRaisesThePrice` compared the grain price of hillfort twelve periods after a drought and found it dearer than the control's; now that the drought cuts what the riverton mill makes, the mill's margin (838 per mille against a stop of 900) stops it, riverton no longer buys grain, and hillfort's grain ends cheaper (2,089 against 2,593 per mille of its reference). The test now asserts the immediate effect (hillfort dearer while the shock is new: 1,350 against 1,259), the mechanism (the drought rig's mill held by `Margin` with no runs, the control's running) and the consequence (hillfort's grain cheaper). The old assertion would have passed again only by pinning an accident.

**Mutation check** of the new logic (round 1 and round 2; a runner outside the tree on a source-only copy, SHA-256 of each file compared after every mutant, a 900 s timeout per mutant, the unmutated copy passed first for each of the three test projects; the copy was built with SDK 8.0.427, see 15.9). 31 mutants of: the throttle and margin (6), the held limit and the throttled-run logic (4), the yield in the plan, delivery and start (4), the reader (5), the state rules (4), the image and pair converter (3), the save check (1), the catalog converter (4 after two that did not compile were replaced), the AI bridge (2). **Round 1: 22 killed, 7 survived, 2 did not compile.** Survivors: two **equivalent** (`ratio >= full` as `>` and `ratio <= stop` as `<`: the linear formula gives exactly 1,000 and 0 at the two ends, so the response is continuous and the boundary cannot be seen), one **equivalent** on reachable states (`OrderBy(completes)` of a line's batches: they are appended in completion order and restored in it, so the sort is a guarantee, not a change), and four **real holes** that I closed with new tests and killed in round 2: the shock factor missing from the margin (`AShockOrAModifierOnWhatALineMakesLowersItsMarginAndTheThrottleFollows`), the warehouse cut missing in the plan and in the start of an at-once boosted run (`ALaterLineOfThePeriodSeesTheStockAnEarlierBoostedLine...`), and the "requires its own output" refusal that a later cycle check also catches with the same code (the message is now asserted). The two that did not compile (`if (false)` is an unreachable-code error here) were replaced by `if (whole != amount && whole < 0)` and an inverted test; both killed. **Round 2 result: 28 killed, 3 equivalent, none survived.** One of my new tests had a bug found by the unmutated baseline (a `Period` helper that gives no host facts, in a world with a modifier); fixed before the round was counted.

**Not done, and why**
- A rebalanced town (hillfort's ore still piles; ore is a pair's production, not a recipe output) and a full-profit margin (wages, sales tax, subsidies): a design decision, not asked for.
- Tool wear, a tool contended by several lines, and a requirement that is bought rather than held: the catalog's `uses` is reduced with a note, the rest has no consumer.
- Cycles through a one-period delay or a fixed-point solve: needs the owner's choice of bound (15.6).
- A cheaper digest or a compact price history, so the worst open stays under 500 ms on a loaded machine: the 1.9 MB of history and the digest text are the remaining cost; not measured to be needed beyond the median.
- The margin percept does not apply host-fact modifiers (the bridge is not given the facts); `explain` and `PlanLines` can, with the facts passed in.
- Release builds of the other test projects, the full solution build, and the whole of DialogueEngine, Relationship, Calendar, AI, Combat, ScenarioEngine and WorldCreator suites (focused-tests rule).
- **Verification environment:** the in-repository SDK (10.0.402) worked until the middle of the pass and then its .NET 10 runtime disappeared from the machine (not by anything this pass ran). Everything after that point (the Economy test project's last two runs, the demos, the mutation rounds, the GameRuntime project runs) ran in a source-only copy under SDK 8.0.427; the test and engine code are identical to the tree, the copy was synchronised file by file, but those runs are not runs of the pinned SDK. The Release measurement of 15.8 and the first full runs of `EconomyEngine.Tests` (580) and `GameContent.Tests` (408) were made in the repository under SDK 10.0.402.


## 16. Pass 51 (2026-10-30): the profit margin, tool wear, a rebalanced town; document and save format 5

*Pass 57 (section 17) raised both formats to 6; "format 5" and the hashes of 16.5 below describe pass 51 and are historical.*

Three follow-ups left by 15.3, 15.6 and 15.10. Each changes what a document or a save means or holds, so **the document format is 5 and the save format is 5**; format 4 is refused by name (`ECON_FORMAT_4` for a document, `ECON_SAVE_FORMAT_4` for a save), as formats 3 and 2 were before it. No migration. What format 5 adds: (a) the optional `costs` of a line's `throttle` (16.2); (b) the optional `wear` of a recipe's `requires` entry (16.3); (c) a line's saved state grows by the runs it has done towards the next loss of each required good, `LineLimit` gains `Worn` (the saved limit range is 0 to 6), and the digest text is `economy-state-5`. A format 4 document that uses none of this reads the same way in format 5; the number is raised anyway, so that a document that declares wear or costs can never be opened by a reader that does not know them, and so that an old save is told by name that it is old.

### 16.1 Research (searched 2026-10-30; reached, snippet and unreached are stated per source)

| Source | Status | What it was used for |
|---|---|---|
| OpenStax, *Principles of Microeconomics* 3e, 8.2 "How perfectly competitive firms make output decisions" | **reached** (fetched again in this pass) | The shutdown rule: a firm shuts down when the price is under its minimum average **variable** cost; **fixed costs do not affect the decision** (they are paid whether or not the firm produces, so shutting down saves only the variable ones), and a firm that covers its variable costs keeps producing even at a loss. Adopted: a line's margin counts only costs that **fall with a run**: inputs, the wage of a run, the other variable cost of a run. A fixed cost (rent paid whether the line runs or not) is not a term; the treasury's budget lines already carry the market's. |
| Wikipedia, *Tax incidence* | **reached** (fetched) | "It does not matter whether the tax is levied on consumers or producers": the burden depends on the relative elasticities of demand and supply, not on who is named by the law. Used for the sales-tax decision (16.2). |
| Paradox forum, Victoria 3 developer diaries 5 (production methods) and 11 (employment and qualifications) | snippet only (search result, pages not opened) | A building sets its own wages from local conditions; a building that loses money lowers wages until it is profitable again; profit per worker falls as it fills; the interface predicts a building's balance from production, consumption and wages. Adopted as a principle only: a wage is a term of a line's profit and the trace shows each term. **Not** adopted: wages that adjust themselves (a second price rule). No formula is claimed for the game. |
| Anno 1800 wiki and community threads on building maintenance | snippet only | Players describe maintenance as a cost every building has, also while idle, and say that pausing a building avoids it. Consistent with the economics source: the idle-time cost is a fixed cost and out of the run decision. Nothing else was taken; no figure is claimed. |
| Dwarf Fortress reaction pages, Factorio | not re-read in this pass (see 15.1 for the earlier snippet on a needed-but-kept reagent) | A tool kept by a recipe is an established mechanic (15.1). The wear of a tool by use is the catalog's own `uses`, which is the stronger reason (16.3). |

### 16.2 The profit margin (item 1)

**Which terms count, from what the engine already has** (checked in the code, not assumed):

| Concept in the engine | Where | Does it reach a line's revenue or cost? | Decision |
|---|---|---|---|
| Inputs at market prices | `PlanLine` | yes, they are the cost of a run | **term** (as before) |
| Labour and energy as goods | 15.6 | yes, they are ordinary inputs | already in the inputs |
| Wages and upkeep (`budget`) | `SettleSchedule` | no: a treasury pays them on a schedule from a head count; no line is charged | not a term; the new per-run `wage` is a different thing (below) |
| Sales tax | `UpdateMarket` | **buyers** pay it on top of the price (`consumerRatio`), so it lowers what they ask for; the producer receives the price | not a cost of the line (tax incidence, 16.1): the price the line sees already carries the tax. The trace shows the rate for each output |
| Subsidy | `UpdateMarket` | paid on a **pair's own production**, not on what a recipe makes | not a term: counting it would claim money the engine never pays a line |
| Import and export tariffs | `Allocate` | costs of shipping | not a term of production |

So the engine has no tax, subsidy or tariff that falls on a line, and none is invented to fill the list. What the engine did not have is the cost of the line's own labour and overhead, and that is what the document can now declare.

**The rule.** The optional `costs` of a throttle: `"throttle": {"stop": 900, "full": 1200, "costs": {"wage": 1500, "overhead": 500, "indexed": true}}`. `wage` and `overhead` are per run, in thousandths of a minor unit of the market's currency (the unit of a price; 0 to 10 trillion, whole numbers); `indexed` (required when `costs` is present, no hidden default) makes both follow the currency's price level like an indexed budget line. The margin becomes

> margin = (outputs' value after modifiers and shocks) x 1,000 / (inputs at market prices + wage + overhead), per mille; 1,000 is break-even.

The stop and full ramp, the fraction, the limit named `Margin`, the AI margin percept and the saved state are unchanged: only the cost in the divisor grows. **Without `costs` the margin is exactly the old one** (outputs over inputs): reachable by writing nothing, not by a default. Inside a period the indexed cost uses the price level the prices are still expressed in (the level before the period's growth is applied; prices are rescaled to the new level later in the period), so the explanation, the plan and the period agree; outside a period it uses the present level. The cost is formed in 128 bits and capped well inside a long.

**Trace.** `explain ... production` prints, for a line with costs, `margin <recipe>: <r> (per mille: one run's outputs are worth V against a cost of C (inputs I + wage W + overhead O) ...)`, a `wage` term (declared amount, indexed or nominal), an `overhead` term (and says that costs paid whether or not the line runs are not counted), and one `sales-tax` term per output with its rate and why it is not a cost. `check` prints `costs wage W overhead O indexed|nominal` on the line.

### 16.3 Tool wear (item 2)

**Declaration.** A `requires` entry may carry `"wear": {"units": u, "runs": n}` (units 1 to 1,000,000, runs 1 to 2,000,000,000): `u` units of the good are lost for every `n` runs of the line that holds it ("1 hammer in 200 runs"; "3 bricks in 10 runs"). Only a requirement can wear: an input is used up already and an output does not wear, so `wear` on either is `ECON_UNKNOWN_FIELD`. Whole numbers, no chance.

**State.** A line keeps, per required good, the runs it has done towards the next loss (`LineState.Wear`, 0 to `n - 1`; 0 for a good that does not wear). Saved, covered by the digest, range-checked as state (one count per requirement, inside `0 .. n - 1`, 0 where nothing wears; `LineLimit.Worn` only where the recipe has a tool that wears and only naming such a tool). Wearing in small steps loses the same units as in one step (a test checks this against a run-by-run oracle on 600 random cases).

**One period.** After the margin and before the inputs, a worn tool caps the runs: a run starts only if the tool is whole at its start, so the line may run until the loss that takes the stock under the required amount has happened: `((stock - required) / units + 1) x n - progress`, at least 1 whenever the stock holds the requirement (`RunsBeforeWornOut`). The cap names `LineLimit.Worn` and the tool (a tie with the capacity or the margin keeps their name; a tool before an input before an output). The runs then wear the stock: `(progress + runs) / n x u` units are lost, **never below nothing** (a loss larger than the stock leaves 0), the loss counts as **demand** for the good in that period's price (as an input's use does), so the market re-buys a worn tool through the ordinary rule (its price rises, merchants ship, its pair's own production refills it); a test shows the price rising against a twin without wear. The plan applies the same loss to its working copy, so a later line of the same period (a line that shares the tool, or uses it up as an input) sees what the earlier one left, and the plan equals the period.

**When the tool is gone.** The stock is then under the required amount and the line is `Held`, naming the tool, as for a tool that was never there (15.6); the trace says `the tool is worn out until the market holds N again`. `explain` shows a `wear` term (runs done, the loss declared, and how many more runs the stock lasts) and `Worn` in the limit text; the headless demo's limit sentences now name `held`, `margin` and `worn` (they used to read "runs at capacity" for any limit they did not list).

**The catalog converter** (`GameContent.Catalog.Economy`, `docs/RESOURCE_RECIPE_CATALOG.md` section 11). The catalog **does** carry wear: a recipe's tool requirement is `{resource, uses}` ("each run wears 1/uses of one item", schema: `uses` 1 to 2,000,000,000), which is exactly the Economy's wear. No field was invented and none is missing. A tool that is a priced tradable good now becomes `{"good", "amount": 1, "wear": {"units": batch, "runs": uses}}` (the batch is the catalog runs one Economy run stands for, 15.6: a batch of 2 catalog runs loses 2 items' worth in the `uses` of each); **`CATALOG_ECONOMY_TOOL_WEAR_NOT_MODELLED` is retired** and no note is made, because nothing is lost. A catalyst never wears (the catalog keeps it). A tool that is not a priced good, that the recipe also makes or that would close a cycle keeps its reduced code (15.6). A resource's own `durability.uses` (the item's total life, a different field) is still not modelled: the note said so under `CATALOG_ECONOMY_RESOURCE_FIELDS_NOT_MODELLED` (pass 54 gave it its own code, 16.7). Tests: a tool wears out in the running economy (`uses` 25, 10 runs a period: 10, 10, 5, then held), the batch scales the units, the longest legal life converts and loads, a catalyst has no wear.

### 16.4 Rebalancing the town (item 3)

**Intended behaviour of the demo** (fixed before measuring): (a) the fair band holds most pair-periods, well over the 8.9 percent of 14.1 (14.5 percent with the noise of 15.2); (b) no market is in arrears for most of the year; (c) no pile that grows without bound (hillfort ore reached 13,383 in 15.3, 40,578 in the pass-48 single run and 85,128 in the mean of the 30-seed measurement below); (d) no chain permanently starved or permanently throttled; (e) the story still happens (the drought throttles the mill, the locked recipe is discovered, the devaluation hits the seaport). **Only authored numbers changed**: no engine rule. Changed: production, consumption and stock-defining numbers of pairs, line capacities, route capacities, the flour base price, riverton and seaport staff and wage per head, and the new declarations (`costs` on all five throttles, `wear` on the mill's and the bakery's tools). Throttle bands (900 to 1,200), prices of the other nine goods, taxes, subsidies, policies and the document's rules were kept.

**What was learned about the document, by measurement** (`docs/measurements/economy-rebalance-pass51-2026-10-30.csv`; a harness outside the tree built on the public `EconomyRuntime` API ran 8 to 30 seeds x 365 periods, with and without a cycle of seasons, with the alloy unlock of day 35):

- **An intermediate good with no consumption of its own sinks to its floor.** The stock-gap term wants `cover x consumption`, which is 0, so a good only the lines use is always "too plentiful" and its price decays; that was hillfort's ore and coal. Giving them a small local consumption (20 ore, 30 coal, later tuned) gives them a stock to hold and a price to keep.
- **A line's input and its pair's production must meet.** Hillfort's coal (150) could feed only 85 of the smelter's 150 runs, so its ore (500) piled to 40,578; the smith made 180 tools a period for a demand of 75. The numbers now match each good's total use.
- **Wages were nearly twice the town's tax income.** In one seed's year riverton's wage bill was 250,000 against 138,000 of tax (and a subsidy and upkeep on top), and seaport's 748,000 against 433,000: they were in arrears for 77 and 65 percent of the year. Staff and wages per head of those two were cut to what the taxes bear; nothing in the tax rates changed.
- **The mill could never earn.** At base prices 3 grain (6,000) became 2 flour (6,000): a margin of exactly 1,000, a third of its capacity at best. Flour is now worth 4,500, and the mill and the bakery have a wage and an overhead per run to pay out of a margin of about 1,200 to 1,700.
- **Route capacity was shared in order of value per unit**, so grain (cheap per unit) never got a place on a route that carried tools, cloth and iron; the six routes' capacities were raised.
- **Search.** After the structural changes above, a coordinate search (random single-number moves, accepted when an objective of fair-band share, arrears, pinned prices, shortages, piles and starved lines improved; about 400 evaluations of 8 seeds in two modes) tuned the remaining numbers; the result was then rounded (to the nearest 5 or 10) and re-measured, because round numbers are what a reader of the sample can follow. The search is a way of finding numbers, not a claim that they are optimal, and they were checked on seeds the search did not use (the sweep below runs seeds 0 to 49).

**Before and after** (the official tool: `simulate --seeds 50 --days 365`, the sample's own scenario; the harness figures in the CSV add seasons and the alloy unlock):

| | before (same engine, old numbers) | after |
|---|---|---|
| pair-periods in the fair band | **14.5 percent** | **64.2 percent** (harness with seasons and the unlock: 14.5 to 53.8; without seasons: 17.6 to 63.0) |
| periods in arrears of 365, riverton / hillfort / seaport | 282 / 0 / 239 | **0 / 0 / 0** |
| pairs that reach the 4,000 ceiling / the 250 floor at some point | 7 / 7 | 1 / 2 |
| pairs short for over a quarter of the year | 4 | 0 |
| largest mean stock at the end (harness, 30 seeds) | hillfort ore 85,128 | riverton grain 1,780 to 2,004 (a warehouse holds 100,000) |
| mill / bakery runs a period (mean), harness | 45 / 46 to 52 (off 38 and 34 percent of periods with seasons) | 140 to 158 / 146 to 153 (off 3 to 5 percent of periods) |
| metrics that vary between seeds (of 196) | 79 | 123 |

**What authoring could not fix, said plainly:**

- **Two exporters stay at their floor for the whole year**: hillfort's iron and seaport's cloth. A market's price answers its own balance only; exports leave the surplus after the local market has settled (the loading rule of section 11), so a producer whose output is far over its local use never sees the foreign demand. The numbers that keep them off the floor either starve the importers or lose the fair band elsewhere; the sample accepts the two as "export towns with a glut at home". It is an engine property, not an authoring one.
- **The smelter runs about 10 of its 40 runs**: steel has three users (the smith, a small local use and the alloy), so the smelter is demand-limited, not starved. It was starved before (coal, 95 percent of periods); now it is under-used, which is honest for the numbers the goods carry.
- **64 percent, not 100.** The bands are narrow (800 to 1,250), noise 20 moves prices across the edges, the scenario hits the economy with a drought, a devaluation and a decree, and the year includes the settling from the initial stocks (not separated, as in 14.1). Whether 64 percent is the right balance for the demo is a design judgement; it is far above 9 and the story still happens.
- **The treasuries are balanced, not rich.** Riverton ends the year near 41,000 of the 50,000 it started with (the farm subsidy and the wages take most of its tax), seaport and hillfort end higher; no arrears in any of the 50 seeds of the sweep, but the margin is thin by design (staff and wages were cut only until the search stopped finding arrears).
- **The isles economy was not rebalanced** in pass 51; pass 57 did (section 17.3).

### 16.5 Demo hashes

Each run twice with the same hash; the replay from the checkpoint ended in the same state and raised the same events. Run in the repository, Release, SDK 10.0.403 (the pinned 10.0.402 with `rollForward: latestPatch`).

| Run | Before (15.9) | Now |
|---|---|---|
| Economy demo, default (`--days 120`) | `562D70E230C82C2DD98A3B91CB66EA8DF62406B862B4BD4B110A66E5DD2F3FE6` (5,995 events) | `BC3E1F6FFABE055BB6BD987B06F72FD2F9FBEAF171D728CB731FDA0DEAB9B37C` (5,865 events; state `C35C6FA670466F67`, events `D751E7B96E4356FC`; replay of ticks 57,631 to 172,800: state same, events same, 3,865 events) |
| Economy demo, `--days 20 --checkpoint-day 7` | `438F8A057B5FF7ECC7106C8A2D933C3FD7E71A02D38C26ACC1ED9BA3EF1A577C` (1,069 events) | `19BC196B41F23565A9E4274EA1206E40A48892478B8A6C36AD1615CFFB8276F5` (920 events; state `40D446C5207432F1`, events `5CC22ECF9C0B936A`; replay of ticks 10,111 to 28,800, 622 events) |
| World-events demo (embeds the town economy) | `828C9303072970C1FE7B459DEA489BB271445A7BE129B12E3546CC66582BD314` | `D45BF0031AD618DFEBE6DDD03D793F227282089D1EB59E351F3EA060366D1F73` (637 events; world state `CFAA39AB28E8DCEE` and events `93911E160FA0234C` **unchanged**: the economy it prints moved, the world did not; replay same, 441 events) |
| Diplomacy demo (embeds the isles economy) | `8266A7D3101EFCC2783C6CF2AEB5685288C8BE7FFBE78E84131069A84A094029` | **unchanged** (the isles document changed only its format number; state `2FCB5DAB41B76128`, events `5E83E9058DF3BA75`, replay 33 events) |

`GameRuntime.Economy.Tests.HeadlessTests` pins the first two (updated). The world-events and diplomacy test projects compare runs and replays and pin no hash.

### 16.6 Tests, mutation, not done

**Tests** (focused; `scripts/focused-tests.ps1 -Changed src/EconomyEngine,src/GameContent/GameContent.Catalog.Economy,src/DiplomacyEngine/samples -List` selected the same six projects as in 15.10, all six were run, no full suite or solution build). `EconomyEngine.Tests` 584 to **608** (`WearAndProfitTests` 22: the old margin by writing no costs; wage and overhead lowering the margin to 1,000, a fraction of 333 and 13 runs of 40; indexed and nominal costs and their rounding; the level inside a period (13 runs, not 11); the trace of every term; a sales tax not being a cost; every wrong `costs` and `wear` refused by code; a tool lasting 10, 30 and 20 runs by hand; progress kept over periods (4, 8, then a loss at 12); the floor; two lines sharing a tool and the plan equal to the period; wear as demand; the arithmetic against a run-by-run oracle on 600 cases; the worn limit saved and refused where it cannot be; format 4 refused by name for a document and for a save; plus the refusal-matrix rows and the pins that follow the town's new numbers), `GameContent.Tests` 408 to **410**, `GameRuntime.Economy.Tests` 131, `GameRuntime.WorldEvents.Tests` 111, `GameRuntime.Diplomacy.Tests` 160, `GameRuntime.Tutorial.Tests` 127, all passing. The Economy project including its scale and save-cost classes also passed in one Debug run (608); those are timing tests and are not a gate. **Tests of other engines that changed because the sample's numbers changed**: `EconomyIntegrationTests.ADroughtTheWorld...` (the drought now throttles the mill instead of stopping it, and hillfort's grain is dearer than the control's again after a dozen periods, as the test's name says), and `ProductionComponentTests` (the smelter's first shortage is of ore, not coal). The tests that name the sample's staff, route capacities and limiting good follow the new numbers.

**Mutation check.** 33 mutants of only the new logic (the margin cost 8, wear arithmetic and plan 13, state rules and image 7, formats 3, converter 3), by a runner outside the tree on a copy of `src`, each applied to one file, SHA-256 of the file compared with the original after it was restored, a 900 s timeout per test run; the unmutated copy passed first (327 Economy tests in the focused classes, 48 converter tests). **Round 1: 28 killed, 5 survived** (none failed to compile, none timed out). The survivors were real holes and each got a test: the indexed cost not rounding (`3 x 1.5` is 5, not 4), a loss larger than the stock leaving a negative `Met` (now: the state reopens, `Met` is 0), a later line seeing a negative stock in the plan (a line using the tool up as an input is now checked), the saved limit range ending before `Worn` (a worn state is now saved and reopened), and a `Worn` limit naming a tool that does not wear (forged saves refused). **Round 2: all 33 killed**, no equivalent mutant left. The tree itself was never mutated.

**Not done, and why**
- **Fixed costs and wage adjustment.** A cost a line pays whether it runs or not is deliberately not in a run decision (16.1); a wage that rises and falls with the line's profit (Victoria 3's behaviour) would be a second price rule and was not asked for.
- **A tax, subsidy or tariff on a line's output.** The engine has none that reaches a recipe's revenue (16.2). Making the farm subsidy pay for recipe outputs, or a producer-side output levy, would be new engine rules; they would then enter the margin.
- **A tool contended by several lines at once** was checked line by line in order with no reservation (done in pass 57, section 17: the first line that runs holds the tool for the period). A tool is not bought by the line itself; the market re-buys it by demand.
- **The catalog resource's own `durability.uses`**: decided in 16.7 (pass 54): not read, with its own note code.
- **Rebalancing the isles economy** (done in pass 57, 17.3), a cheaper digest or history encoding (15.10), cycles (15.6): unchanged.
- **Not run:** the full solution build; DialogueEngine, Relationship, Calendar, AI, Combat, ScenarioEngine and WorldCreator suites; Release builds of the other test projects (focused-tests rule). The Godot adapter and other operating systems are out of scope (Windows only).

### 16.7 Pass 54 (2026-10-31): the catalog's `durability.uses`, wages and fixed costs, the recipe-output levy

**Catalog `durability.uses` (B1): not read, with a code of its own; no format change.** The schema gives a resource `durability: {uses: 1 to 2,000,000,000}` (only a tool, equipment or unique item may carry it, `CATALOG_DURABILITY`) and a recipe tool `{resource, uses}` with the same range, documented in the catalog as "each run wears 1/uses of one item". Pass 51 converts the second exactly. The first cannot be turned into a wear on the same footing: the Economy's wear is a property of a `requires` entry (a recipe holding a good), and the recipe's `uses` already states that fraction completely; the item's total life is counted in the item's own use, and nothing in the catalog says how many of those a run of a given recipe costs, so a conversion would have to invent the ratio (or silently disagree with the recipe when the two differ, for example life 300, recipe uses 100). The comparable case in games: Minecraft's durability is an item's total number of uses, and an action costs a per-action amount (most blocks 1, a sword on a block 2, a mob 1 or 2, armour by damage), so total life and cost per action are separate quantities, and wear per action is their ratio; here the catalog gives the ratio's result (the recipe `uses`) and the Economy takes it. Initial stock cannot carry the item's life either: stock counts items, and a market's stock of a tool is the market's, not the pack's. Nothing is invented: no catalog field, no Economy rule. What changed: the generic note `CATALOG_ECONOMY_RESOURCE_FIELDS_NOT_MODELLED` no longer names durability; a good with `durability.uses` gets `CATALOG_ECONOMY_DURABILITY_NOT_MAPPED`, whose text gives the life that was not read and either "no kept recipe holds this good as a worn tool, so it does not wear in the Economy" or the distinct recipe `uses` that do wear it. Document and save formats are unchanged (format 5 stays; no `ECON_FORMAT_5` refusal is needed), and so are all demo hashes. Tests (`EconomyConverterTests`): a tool of life 300 held by recipes of uses 100, 50 and 100 converts to wear `1 in 100` and the note names `(50, 100)` once each, in order; a catalyst with a life says it does not wear; stack-only fields keep the old code and durability-only fields no longer produce it.

Sources, honestly: Minecraft Wiki "Durability" (https://minecraft.wiki/w/Durability): **reached**, first 100,000 of 102,768 characters read through a summarising fetch (so the per-item numbers are the fetch's summary, not a verbatim reading). Factorio wiki durability page (https://wiki.factorio.com/Durability): **unreachable** (HTTP 404), unchanged from earlier passes where it was not reached. The catalog schema and validator were read in the repository. The reading "total life and cost per action are different quantities, so the catalog's recipe `uses` is the ratio" is a design inference, not a statement of either source.

**Self-adjusting wages and fixed costs (B5): stay out.** Fixed costs (a cost paid whether or not the line runs) are excluded from a run decision by the shutdown rule (OpenStax Principles of Microeconomics 8.2, reached in pass 51, 16.1): a firm that already pays them minimises loss by running while price covers variable cost, so a fixed cost does not change what a line does. Wages that rise and fall with a line's profit (Victoria 3's behaviour, not re-read here) would be a second price rule beside the market's own price response and are not needed by any consumer. The wage and overhead per run of 16.2 remain authored numbers. **Reopen condition:** a labour-market feature that names a consumer (for example a Diplomacy or World-events feature that needs workers to move between markets or wages to answer scarcity); then wages become a priced good or a rule of that feature, designed once, not a self-adjusting field on a line.

**Recipe-output levy (B4): deferred, nothing built.** A tax or subsidy on a recipe's output is a new engine rule. Sales tax already reaches a line through demand and the farm subsidy is paid on a pair's own production (16.2). **Reopen trigger:** a Diplomacy treaty or policy feature that names the recipe-output levy as its consumer; it would then enter the margin like the costs of 16.2.

## 17. Pass 57 (2026-10-31): a tool shared across lines (document and save format 6), and the isles economy rebalanced

Two packages of `docs/ENGINE_REMAINING_WORK_PLAN.md`: B3 (a required good contended by several lines) and, after it, B2 (the isles economy).

### 17.1 Research (searched 2026-10-31; reached, snippet and unreached stated per source)

| Source | Status | What it was used for |
|---|---|---|
| Factorio wiki, *Inserters* (https://wiki.factorio.com/Inserters) | **reached** (first 100,000 of 115,812 characters, through a summarising fetch, so the wording is the fetch's summary) | When several inserters want the same belt items there is **no fixed priority**: "the inserter that can grab the items the fastest will grab them first". **Avoid** for an engine whose runs must be reproducible: contention is settled by timing. **Adapt**: the same shortage, settled by a fixed order the document already has. The page says nothing about tools or catalysts as such. |
| Victoria 3 wiki (building input shortage sharing) | **unreached** (`victoria3.paradoxwikis.com` did not resolve; `vic3.paradoxwikis.com/Production` gave HTTP 404) | Nothing was taken from it. Pass 51 recorded Victoria 3's developer diaries as snippet only; that is unchanged. |
| Anno 1800 (which building takes a shared input first) | **snippet only** (one web search; the results did not state a ranking and no page was opened) | Nothing was taken from it. The search found that supply buildings pull from nearby producers or from warehouses, not how competing buildings are ranked. |

Decision (a design choice, not a statement of any of those sources): the engine already runs the lines of a market in a fixed order (a recipe after those that make its inputs, then declaration order) and a later line already sees what an earlier one left. A tool follows the same order, so the answer needs no new concept: **the first line in the order that runs takes the tool for the period and the next sees what is left.**

### 17.2 The rule (B3)

Before pass 57 each line only asked "is the tool in stock?", so two lines needing one hammer could both count the same hammer. Now, inside one period:

1. **Holding.** A line that starts at least one run **holds** the amount it requires of each required good for the rest of the period. A line that starts no run (capacity 0, the margin throttle at 0, no inputs, no room, a locked recipe) holds nothing, and so leaves the tool to the next line.
2. **Seeing.** A later line sees a required good as **free** stock: stock less what the lines before it hold, never below nothing. It is `Held` (naming the good) when the free stock is under its amount. The same free stock bounds a worn tool's runs (`RunsBeforeWornOut`) and, because the units a line holds are in use, an **input** of a later line (a line that uses the tool up) cannot take them either.
3. **Wear falls on the line that used it.** The loss of a line's runs comes out of the stock, never below nothing, and falls **first on the units the line holds**: it keeps holding `amount - lost` (nothing once its own tool is worn out), so with two hammers and two lines the hammer that wears out is the wearing line's, and the other line keeps its own.
4. **A tool that wears out mid-period.** The line may run until the loss that takes the stock under its requirement has happened (unchanged); the stock is then lower, the line holds nothing of it, and a later line in the same period finds only what is left of the stock (possibly none, then `Held`). No negative stock exists at any step (a test checks the floor). Next period the stock is what it is: a line is held until the market holds the amount again, and the market buys it again by the ordinary demand (the loss counts as demand, 16.3).
5. **Only inside the period.** The holding is working state of the period's plan; it is not saved, not in the digest, and not carried over. A recipe with a duration does not keep its tool across periods (unchanged: a batch in progress needs no tool). So the save has the same shape; the format is raised because **the meaning of `requires` changed**, not because of new state.
6. **One line, no change.** With one user of a tool the rule changes nothing (a test pairs a shared document with a twin whose second line needs another tool: the same runs, wear and stock for four periods; the pass 51 wear oracle still passes unchanged). The shipped samples: the **town** has one shared requirement (riverton's mill and bakery both need `tools`); its stock is enough for both, and its demo events hash is unchanged (17.5); the workshop and isles have none.

**Trace and `check`.** `explain ... production` now says for each requirement `needs N free at this line's turn, never used up: stock S, held by earlier lines this period H; held | missing | missing: the earlier lines hold it`, and the `Held` limit text says `requires free (in stock and not held by a line earlier in the order this period)`; the input term shows the free stock too. `check` prints one `shared requirement <market> <good>: held by one line at a time, in line order r1 a1, r2 a2; the market needs <sum> for all of them to run in one period` line for every required good that more than one line of a market needs.

**Formats.** Document 5 to **6** and save 5 to **6**; a format 5 document is refused with `ECON_FORMAT_5` and a format 5 save with `ECON_SAVE_FORMAT_5`, each naming what changed; the digest text is `economy-state-6`. No migration. Samples (town, workshop, isles) and the test builders raised to 6.

**The catalog converter** (`GameContent.Catalog.Economy`, `docs/RESOURCE_RECIPE_CATALOG.md` section 11). The converter code needs no change: it already emits a catalyst or tool as a `requires` entry. Only the meaning of that level changed, and the capability matrix rows for catalysts and tools now say it: two recipes that keep one item need the sum of their amounts to run in the same period, because the catalog does not say a kept item can be in two reactions at once. No note code is added (nothing the catalog states is lost); this is a stated reading, not a converter test.

**Tests** (`SharedToolTests`, 11, plus the changed ones). Hand-worked: one hammer serves the first line and holds the next (plan equals period, the held line's inputs untouched); the holding is the sum of the amounts (stock 3: needs 2 and 2 holds, 2 and 1 runs, stock 4: 2 and 2 runs); a line with no ore holds nothing; a hammer that wears out mid-period leaves a stock of 0 and the later line held; the wear falls on the wearing line (two hammers, 1 in 10 and 1 in 5 runs: stocks 1, then 0); an input cannot use up what an earlier line holds; the single-user twin; the explanation text, which does not change the state; `check`; the two refusals and a save round trip. **The 600-case oracle**: a run-by-run oracle (one run at a time, the stock less the holding of earlier lines, a loss at the wear count, never below nothing) over 600 random markets of 1 to 4 lines (amounts 1 to 3, stock 0 to 8, wear 1 to 3 units in 1 to 7 runs or none, capacity 1 to 12), compared on the runs of every line, the wear counts and the stock for two periods. `TwoLinesSharingATool_...` (pass 51) now uses two hammers, because with one the second line is held; that is the changed meaning, not a regression.

### 17.3 Rebalancing the isles economy (B2)

**Intended behaviour** (fixed before measuring, as for the town): (a) the fair band holds well over the 10 percent it held; (b) no market is in arrears most of the year; (c) no pile that grows without bound; (d) no good starved for lack of any producer; (e) the diplomacy story and the document's rules unchanged. **Only authored numbers changed**: each pair's `production`, `consumption` and starting `stock`, and the capacity of the ten routes. Kept: goods, bands, prices, tax and subsidy policies, budgets, staff, treasuries, currencies, shocks, modifiers, `price` (noise 20), routing mode and the route lists. Markets stayed the five of the diplomacy story.

**What the measurement showed** (`docs/measurements/economy-rebalance-isles-pass57-2026-10-31.csv`; a harness outside the tree on the public `EconomyRuntime` API, 30 seeds x 365 periods, with static facts and with a cycle of the four seasons):

- **Cloth and iron had no producer in any market** (`production` 0 in all five, consumption 150 and 80 in all): after the initial stock was used up they sat at the 4,000 ceiling with the pair short for 99 percent of the year, in all five markets (47.5 percent of all pair-periods at the ceiling, 49.6 percent short). That was the whole of the starvation; no market was ever in arrears (any seed, before or after) and the grain surplus (production 1,000 against 600) stayed near 1,740 units.
- **The search.** After a structural start (every market makes its grain and ale for itself; two markets make cloth and two make iron), a coordinate search (random single-number moves of a pair's production or consumption, of the starting stock or of the route capacity, accepted when an objective of fair-band share, ceiling, floor, shortage, arrears and pile size improved; about 600 evaluations of 6 seeds in the two modes in three rounds) tuned the numbers; the result was rounded to the nearest 10 (starting extra stocks to 100) and re-measured, because round numbers are what a reader of the sample can follow. The search finds numbers; it is not a claim that they are optimal, and the figures below are on 30 seeds.

**Before and after** (30 seeds, 365 periods; static facts / four seasons):

| | before (same engine, old numbers) | after |
|---|---|---|
| pair-periods in the fair band | **10.0 / 11.2 percent** | **76.5 / 69.0 percent** |
| pair-periods at the price ceiling | 47.5 / 47.5 percent | **0.0 / 0.0** |
| pair-periods short | 49.6 / 50.0 percent | **0.7 / 0.7** |
| pair-periods at the price floor | 0.0 / 0.0 | 9.6 / 9.6 (two pairs, below) |
| periods in arrears (any market, any seed) | 0 | 0 |
| largest stock in any seed | grain 3,885 | cloth 6,297 at brass (a warehouse holds 100,000); mean end stocks 80 to 2,100 |
| goods with a producer | grain, ale | all four |
| final states that differ between seeds | 30 of 30 | 30 of 30 |

**New numbers (production / consumption a period):** aurel grain 450 / 600, cloth 560 / 180, iron 50 / 60, ale 200 / 250; brass 410 / 480, 20 / 140, 130 / 100, 160 / 250; cinder 410 / 540, 20 / 110, 20 / 80, 220 / 250; dune 420 / 600, 70 / 140, 340 / 80, 250 / 300; ember 490 / 600, 890 / 140, 10 / 80, 160 / 200. Starting stock is three periods of consumption plus 700 iron at aurel, 200 cloth at brass, 200 iron at dune and 500 cloth at ember. Every route has capacity 1,200 (was 400). Aurel, ember (cloth) and dune (iron) are the exporters; grain and ale are made in every market.

**What authoring could not fix, said plainly:**
- **Two exporters sit at their floor for the whole year** (dune's iron and ember's cloth, about 96 percent of periods in every seed), the same engine property as the town's hillfort iron and seaport cloth (16.4): a market's price answers its own balance, and exports leave the surplus after the local market has settled, so a producer whose output is far over its local use never feels the foreign demand. Numbers that lifted them off the floor starved the importers or lost fair-band share elsewhere in the search. Aurel's cloth is the same in weaker form (cheap, ratio about 360, a fair band of 1 percent). Ember's iron is dear (7 percent fair).
- **Not 100 percent.** The fair band is 69 to 77 percent; noise 20, the seasons and the settling from the starting stocks (not separated) keep the rest outside it.
- **The five markets are no longer identical** (they were). A design judgement; the diplomacy documents were not changed.
- **The ten routes share one capacity**; per-route capacities were not searched.
- **No recipes in the isles**, so "chains not starved" has nothing to test there; the criterion became "every good has a producer".
- The measurement is without the diplomacy scenario's shocks; the demo's own run is in 17.5.

**Guard.** `GameRuntime.Diplomacy.Tests.IslesEconomyBalanceTests` runs a year (seed 1 static, seeds 2 and 3 with seasons) of the shipped document and asserts a fair band of at least 55 percent, no pair at the ceiling, no market in arrears and no stock over 10,000. It fails on the old numbers (3 of 3 failed with the old file swapped in, then the new file was restored and its SHA-256 compared) and passes on the new.

### 17.4 Tests

Focused projects (pinned SDK 10.0.403): `EconomyEngine.Tests` 618 passed with the Scale and SaveCost timing classes excluded (11 of them are `SharedToolTests`), `GameContent.Tests` 412, `GameRuntime.Economy.Tests` 131, `GameRuntime.Diplomacy.Tests` 160 to 163, `DiplomacyEngine.Tests` 505, `GameRuntime.WorldEvents.Tests` 111, `GameRuntime.Tutorial.Tests` 127. Mutation and the not-done list are in the tracker entry of `docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md` (pass 57).

### 17.5 Demo hashes (Release, SDK 10.0.403; each run twice with the same hash; the replay from the checkpoint ended in the same state and raised the same events)

| Run | Before | Now |
|---|---|---|
| Economy demo, default (`--days 120`) | `BC3E1F6FFABE055BB6BD987B06F72FD2F9FBEAF171D728CB731FDA0DEAB9B37C` | `10CCBD044E511BA5C85523C233E46BD22502522C2F6782618470B7C83E8B3A83` (economy state `6FA0C3FCB8736856` changed, **events `D751E7B96E4356FC` unchanged, 3,865 replayed events**: only the digest text and format changed, the town behaves the same) |
| Economy demo, `--days 20 --checkpoint-day 7` | `19BC196B41F23565A9E4274EA1206E40A48892478B8A6C36AD1615CFFB8276F5` | `A65672F88F44A05BD4464054B795AE319E1DA964739FB82CB3DF55BD7647C5C8` (state `3BA83504A65C6DC0`, events `5CC22ECF9C0B936A` unchanged, 622 replayed) |
| World-events demo (embeds the town economy) | `D45BF0031AD618DFEBE6DDD03D793F227282089D1EB59E351F3EA060366D1F73` | **unchanged** (the town's behaviour and what the demo prints did not change; the economy digest is not printed) |
| Diplomacy demo (embeds the isles economy) | `8266A7D3101EFCC2783C6CF2AEB5685288C8BE7FFBE78E84131069A84A094029` | `AE67CE0FD27A2CCBC6AAFF5BB131A19F72AED087091FF9CBBFF2D40089898466` (diplomacy state `2FCB5DAB41B76128` and events `5E83E9058DF3BA75` unchanged; the economy figures it prints moved; replay same, 33 events) |

The town's 50-seed sweep (`simulate --seeds 50 --days 365`) is unchanged at 64.2 percent in the fair band and no market in arrears. `GameRuntime.Economy.Tests.HeadlessTests` pins the first two (updated); the Diplomacy and World-events test projects pin no hash.
