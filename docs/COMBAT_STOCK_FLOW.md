# Combat stock flow: people and rounds as inventory

Pass 24, 2026-10-27, built it; pass 25 added depots, resupply, reinforcement and routed groups (section 7). Status: built and tested; the limits are in section 6.

## 1. What it is

Every squad of the field game has a storage on its entity. Its people and the rounds its weapons carry are inventory resources
(`stock.people`, and one `stock.ammo.<ammunition id>` per ammunition type), and everything that happens to them is recorded as the engine's own
lifecycle flow, so the stock has a history in the same facts (`ResourceFlowFact`, forwarded as `sim.event.stock-changed`) that every other
inventory movement produces.

| What combat reports | Flow recorded | Engine kind |
|---|---|---|
| squads raised | people and rounds issued | `Initialized` |
| `AmmoSpent` (rounds fired) | rounds consumed | `Consumed` |
| `Casualty` (members lost) | people destroyed | `Destroyed` |
| `Standing` becomes destroyed | everything the group still held, taken last | `Lost` |

`Lost` also exists as a plain stated quantity (`LostFlow`) for a caller that has one. Resupply as a `StockTransfer` between a depot and a squad is not built.

## 2. Layers and owners

- `GameRuntime.CombatStock` (new project) sits above the inventory module and the characters module and below the game. It knows combat events,
  the director effect interface and a roster interface. It does not know the field game.
- One class per kind of flow (`IssuedFlow`, `ConsumedFlow`, `DestroyedFlow`, `LostFlow`, `AbandonedFlow`) behind `IStockFlow`; `StockFlows` is the one list.
  Shared code (`StockFlowEffect`, `StockPayload`, `CombatStockBridge`) names no kind and no engine flow kind; a test fails if it does.
- `StockFlowEffect` (director effect `stock.flows`) is the only writer of the inventory. It checks every line of a request against a running balance and
  the inventory's own admission before it applies any, so a request applies completely or changes nothing.
- `CombatStockBridge` is a game component that listens to events only and holds no balance; a loaded game needs nothing restored for it.
  If the world refuses a request the ledger and combat disagree, and the bridge faults the session instead of carrying on.
- `StockContent` generates the inventory vocabulary (units, the squad store, the people resource, the ammunition resources) from the combat groups.
- The field game owns which squad stands for which group (`FieldCommander`/`FieldSquadRoster`); the commander tells the stock layer when the squads exist
  (`SquadsRaised`) and does not end the fight until the stock layer has recorded everything combat reported.

Fact owners, stated plainly because this is a mirror and not a single store: combat owns the rounds loaded in each battery, the squad owns who is alive,
and the inventory ledger records both as flows. The bridge is the ledger only writer. `FieldLedger` reads the ledger back at the end of a run and
compares it with the other two owners (people with the squad roster, rounds with the combat groups, a destroyed group holding none); the field game
prints the result as `ledger a=<people>/<rounds> b=<people>/<rounds> reconciled=<True|False>`.

## 3. Cost (10,000 soldiers, 100 squads, one fight; this machine, default JIT unless stated)

| Run | average tick | allocation per tick | slowest frames |
|---|---|---|---|
| squads with storage, bridge off | 8.4 to 9.1 ms | 421 KB | t2 76 to 89 ms (raising the squads), t16 39 to 53 ms (a round) |
| bridge on, 40 lines a tick | 15.0 to 16.0 ms | 823 KB | t2 117 to 148 ms, t3 62 to 66 ms (issuing stock), t16 56 to 70 ms |

With tiered JIT off the averages were 5.4 to 10.4 ms (bridge off, eight runs) and 9.0 to 14.6 ms (on, nine runs, plus one outlier at 24 ms); the machine varied between runs, so read the ratio (about 1.5 to 2
times the average tick) rather than the milliseconds. A checkpoint is 87 KB against 78 KB without storage and takes 35 to 47 ms against about 32.
The 33 ms frame was already exceeded by the raising frame and the round frames before this layer existed; this layer adds to the round frames
(about 15 ms) and adds one frame of its own at the start (issuing).

What was tried: capping the lines recorded per tick (`--stock-lines`, default 40) cut the issuing frame from 79 to 94 ms to 62 to 66 ms and left the
round frames where they were (they are mostly combat and casualty effects), so the cap is kept for the issuing frame and as a bound. Skipping the
inventory pre-check for removals gave no measurable change and was reverted. A read-only change to the engine's lot codec (below) cut allocation by
17 percent with no measurable wall-clock change.

Engine change made for this layer: `ResourceInventoryState.SyncLots` copied the whole lot list on every save and hash of every storage; it now reads the
list in place (`ResourceLotBuffer.LotCount` / `LotAt`). Allocation per tick fell from 986 KB to 806 to 817 KB; the saved bytes and hashes are unchanged.

## 4. Tests (GameRuntime.Tests, `CombatStockTests`, 21)

Effect level: each flow is recorded as its own engine kind; a request with one line too large is refused whole and changes nothing (entities hash
unchanged); lines share a running balance; ten malformed payloads are refused as invalid argument; an unknown squad or resource is refused as not found;
abandoning nothing records nothing.
Field level: after a fight the ledger agrees with the squads and with combat; a group wiped out in one round still spends the rounds it fired and then loses
what is left; a save in the middle of a fight continues the ledger exactly as an uninterrupted run; a journal of the whole fight replays tick for tick with the
ledger in the world hash; with two lines a tick the fight ends later but the ledger still agrees.
Layer rule: shared stock code names no flow kind and the bridge never touches the world.

Mutation checks (change made, tests that failed): rounds fired not recorded (2), casualties not recorded (2), nothing abandoned (4), ammunition not issued (4),
no running balance (1), pre-check off (3), destroyed recorded as lost (1), abandon not applied last (1), the commander not waiting for the ledger (1), the
per-tick cap ignored (1). One real defect was found by running the system rather than by a test first: a group destroyed in a round still fires in that
round, so its abandon line arrived before its own rounds-fired line and the world refused the request (`stock.short`) and faulted the session. Abandon
lines are now applied after the rest of a request, and a test covers it.

## 5. Research, with its limits (2026-10-27)

Searches for how Hearts of Iron IV and Command: Modern Operations record equipment and ammunition losses returned only player forum posts and patch
notes, no developer documentation. What they support, as player reports: equipment is either usable or lost (no damaged state for ground equipment), and
a logistics view shows each equipment type's shortfall against full strength. US Army LOGSTAT Class V reporting treats ammunition as a reported status
per unit. I did not find a primary source for how either game stores the ledger, so the design above (flows recorded as the engine's own lifecycle kinds,
with a reconciliation against the other owners) rests on the engine's documented inventory contract and my own reasoning, not on a copied pattern.

## 6. Not done, and why (updated in pass 25)

- **The mirror.** The ledger repeats two facts the other owners hold. Drift is caught loudly (a refused request faults the run; `reconciled=False` at the end);
  it is not designed away, because combat state and the squad component keep their own formats.
- **Ammunition by weapon.** Decided against, see section 7.
- **Fuel and platform losses in the ledger.** Combat burns fuel (march rule) and loses platforms, but fuel is not a ledger resource, no depot refuels, and platform
  losses are an event nothing records. No field formation raises a fuelled group with a depot yet.
- **Resupply and reinforcement are rules of the field game only.** The stock layer records whatever combat accepted from a depot; a game with another rule
  (a convoy, a delay) would write its own quartermaster. The people who join are always of the first roster entry of the formation.
- **Reinforcement has no travel time and no recruit cost beyond the depot reserve.** A squad slot is added for each person; slots of the dead are not reused,
  so a squad total grows and is bounded by the squad entity `max_members` (refused as `characters.too-many` when full).
- **Measurement limits.** One machine with large run-to-run variance; three to six runs per figure; frame times are from a loop callback, not a profiler.

## 7. Pass 25 (2026-10-27): depots, resupply, reinforcement, routed groups, overhead

Research: I looked for how strategy games model resupply and replacements and found only player discussion, no primary source; the design follows the
combat engine existing contract (the caller takes stock out, the engine says what fitted, the caller removes only that).

What was built:
- **Depots.** Each side has a depot entity with a storage (placed by the world content, first in the world). It is issued `--depot-percent` (default 25) percent
  of the people and rounds the side took onto the field. `--depot-percent 0` is no reserve and so no resupply or reinforcement.
- **The supply rule** (`FieldQuartermaster`, the game owns it). Every 30 ticks, at tick 8 of the cycle, between rounds and once the books are settled: each group still
  in the fight with a battery below half its load is offered rounds; a group below three quarters of its starting strength is offered up to a fifth of it in
  people. Offers are never more than the depot holds (read from the world, which owns the balance) and never more than is missing. Combat answers with what fitted
  (`Resupplied`, `Reinforced`); the stock layer records exactly that. A routed or destroyed group is never offered anything and combat would refuse it.
- **Recording** (`stock.supply`, `stock.reinforce`, one `StockMoves` owner). Rounds that fitted move from depot to squad as `StockTransfer`; people move depot to squad
  and the character factory adds the same number of members to the squad in the same step (`ICharacterFactory.Reinforce`: new slots of one roster entry after the
  existing ones, a fresh casualty order, refusals `unknown-squad`, `bad-count`, `bad-roster-entry`, `too-many`; the squad component schema is 2). A request is
  checked whole against running balances first.
- **Routed groups.** Routed is permanent in the combat rules, so `Standing = routed` withdraws the squad: everything it held (people and rounds) is recorded as `Lost` with cause
  `stock.withdrawn`, applied last in a request so a hit taken in the same round still counts first. A destroyed group still abandons its rounds. The ledger expects zero for
  a routed or destroyed group; its squad and members still exist in the world.
- **Budget.** The per-tick budget is in units of work: a ledger line is 1, a move or a reinforcement 8 (measured: a flow line costs about 0.145 ms to check and apply,
  a reinforcement about 1.2 ms). `--stock-lines` is that budget (default 40: five reinforcements or forty lines a tick). Something is always sent when nothing else was.
- **Rounds wait for the books.** A round that falls due while the books are open is not lost: it goes at the first tick they are settled. No round is asked for
  while members that combat already counts have not reached their squads, because the squad could not take losses of members it does not have yet.

Measured: the same 10,000-soldier fight, steady-state tiered JIT off unless stated.
- Allocation per tick fell 1.12 MB to 0.95 MB (-15 percent, six runs each, same session) with a cache of each resource native capacity quantum in the capacity
  policy (`CapacityPolicyDeclaration.Quantum` ran a full capacity check and big-integer arithmetic on every call; it is a function of immutable content). A direct
  microbenchmark of flow lines through the effect: 33 KB to 25 KB per line (-24 percent) and 0.21 to 0.28 ms to 0.20 to 0.25 ms per line (about -8 percent, inside the noise).
- Tried and reverted: replacing the reflection-based `Enum.IsDefined` on capacity statuses with a lookup table (profile showed 2 percent of samples; 6 runs each
  showed no change I could separate from the noise). Trimming the pre-check of a flow request: the check is 0.03 ms of 0.145 ms a line, so at best 20 percent of the
  stock cost; not done.
- The remaining cost is the engine guards around each inventory commit (about 0.1 ms) and each movement (about 0.7 ms plus 0.5 ms to check); lowering those
  means weakening the observation guards, which the notes forbid.
- With `--depot-percent 0` (the same work as pass 24): average tick 6.5 to 9.7 ms (six runs), 776 KB per tick; slowest frames raising the squads (97 to 174 ms with JIT off),
  issuing stock (66 to 103 ms), first round (37 to 55 ms). Pass 24 measured 9.0 to 14.6 ms and 806 KB per tick with JIT off; the machine drifted between sessions (the
  bridge-off baseline alone moved from 5.4 to 10.4 ms), so only the same-session allocation and microbenchmark figures above are claimed as gains.

Ammunition per weapon: decided against. The catalog does carry ammunition per weapon, but of the 22 formations in the packs only `arms-modern.rifle-platoon` has two batteries
on one ammunition type; depots hold ammunition by type, so a per-weapon ledger would have a depot hold "rounds for weapon X", which is not a physical thing; and combat resupply
is already per battery, so the squad per-type total is exact in the sum.

Tests (`CombatStockTests` 33, `CombatSupplyTests` 8, `SquadReinforcementTests` 7, plus the combat and catalog ones in `COMBAT_ENGINE.md`): mutation checks that each failed a test:
rounds moved without the running balance, reinforcement without moving people, reinforcement check skipping the depot, supply move not recorded, reinforcement move not recorded,
routed group not withdrawn, offers ignoring the depot, supply threshold always met, withdrawn flow not taking everything, reinforcement costing one unit, a round due not remembered,
rounds ignoring open books; in the factory: casualties skipping lost slots, cursor not restarted, roster index of waves, waves not loaded, the squad limit.

## 8. Pass 26 (2026-10-27)

- **Fuel is a ledger resource** (`stock.fuel.<catalog fuel>`, thousandths of a unit). Squads are issued their tanks; marching records `FuelBurned` as consumed; fuel lost with platforms is recorded as destroyed; depots keep the reserve percent of it; the quartermaster offers a tank below half what its standing platforms can hold fuel from the depot, combat answers with what fitted (`Refuelled`) and the stock layer moves exactly that from depot to squad. A group wiped out or routed loses its fuel with everything else it held.
- **Platforms are a ledger resource** (`stock.platforms`): issued at the start, destroyed as `PlatformsLost` says. Depots do not replace them. The `ledger` reconciliation also checks platforms and fuel against combat.
- **Reinforcement by the roster.** `ICharacterFactory.ReinforceByRoster` spreads new members over the formation roster entries in proportion (largest remainder, earlier entry wins a tie), so a platoon keeps its mix of ranks; the stock effect no longer names an entry. Squads can now take several waves per reinforcement.
- **Formats.** Field checkpoint format 3 (2 refused, tested). Combat checkpoint 4 (see `COMBAT_ENGINE.md`).
- **Still not done.** The commander does not react to platform losses beyond the ledger; the field game still never marches (only the test sends `MoveRequested`), so fuel is burned only when a game does that; a depot does not replace platforms.
- **Measured.** The 10,000-soldier rifle fight (no platforms or fuel involved): average tick 6.5 to 6.9 ms (three runs, JIT off) against 6.6 to 7.8 before; allocation 902 KB a tick against 811 KB (+11 percent, not traced to a cause: the ledger now keeps more resources per squad and reinforcement adds waves per roster entry). Slowest frames unchanged in kind (raising the squads 93 to 110 ms with JIT off, the supply tick 90 ms with JIT off, issuing stock 58 to 65 ms).
- **Mutations that failed a test:** fuel lost with platforms not recorded, platform losses not recorded, burn not recorded, refuel move not recorded, no refuel offers, offers ignoring the depot, withdrawn groups keeping platforms and fuel, and in the factory the tie rule, the leftover rule and the proportional floor.
