using System;
using System.Collections.Generic;

namespace Match3.Core
{
    public enum MissionType
    {
        /// <summary>Clear N tiles of one colour (Param = colour index).</summary>
        ClearColor,
        /// <summary>Create N striped candies.</summary>
        MakeStriped,
        /// <summary>Create N wrapped candies.</summary>
        MakeWrapped,
        /// <summary>Set off N big combo detonations (cross, triple cross, 5x5, colour clear, board clear).</summary>
        MakeCombo,
        /// <summary>Win N moves-mode levels.</summary>
        WinLevels,
        /// <summary>Pop N jelly layers.</summary>
        ClearJelly,
        /// <summary>Crumble N chocolates.</summary>
        ClearChocolate,
        /// <summary>Deliver N ingredients.</summary>
        CollectIngredients,
    }

    /// <summary>One mission: what to do, its parameter (colour) and the target count.</summary>
    public readonly struct MissionDef
    {
        public readonly MissionType Type;
        public readonly int Param;
        public readonly int Target;

        public MissionDef(MissionType type, int param, int target)
        {
            Type = type;
            Param = param;
            Target = target;
        }
    }

    /// <summary>
    /// Deterministic daily/weekly mission generation (xorshift over the day number —
    /// the same trick MusicComposer uses) plus the pure counting rules that map a
    /// <see cref="CascadeStep"/> onto mission progress. No clock, no persistence:
    /// callers pass day numbers and store progress themselves.
    /// </summary>
    public static class MissionCatalog
    {
        public const int DailyCount = 3;

        /// <summary>
        /// The three missions for a given day — types are always distinct, and never
        /// ask for content the player cannot reach yet.
        ///
        /// <paramref name="frontier"/> is the highest level the player could attempt
        /// WHEN THE DAY ROLLED OVER — a snapshot, never the live value. Slot progress
        /// and claims are stored per slot, not per mission, so a set that shifted the
        /// moment a level was won (or the moment a cloud merge jumped the player from
        /// level 1 to 60) would leave stored progress attached to a different mission.
        /// 0 means "unknown" — a save from before the snapshot existed — and filters
        /// nothing, which reproduces the old behaviour exactly.
        ///
        /// Unreachable rolls are skipped rather than remapped: for a player who has
        /// everything unlocked the sequence of draws is untouched, so their missions
        /// are byte-identical to what this always produced.
        /// </summary>
        public static MissionDef[] DailyFor(int dayNumber, int frontier = 0)
        {
            var missions = new MissionDef[DailyCount];
            var usedTypes = new HashSet<MissionType>();
            uint seed = Hash((uint)(dayNumber * 2654435761u + 17u));

            for (int slot = 0; slot < DailyCount; slot++)
            {
                MissionDef candidate;
                do
                {
                    seed = Hash(seed);
                    candidate = FromRoll(seed);
                    // Reachability first, so usedTypes only ever holds what the day
                    // really offered.
                } while (!IsReachable(candidate.Type, frontier) || !usedTypes.Add(candidate.Type));
                missions[slot] = candidate;
            }
            return missions;
        }

        /// <summary>The replacement offered when the player rerolls a slot (one per day).</summary>
        public static MissionDef RerollFor(int dayNumber, int slot, int frontier = 0)
        {
            MissionDef[] daily = DailyFor(dayNumber, frontier);
            var usedTypes = new HashSet<MissionType>();
            foreach (MissionDef def in daily)
                usedTypes.Add(def.Type);

            uint seed = Hash((uint)(dayNumber * 97u + slot * 31u + 421u));
            MissionDef candidate;
            do
            {
                seed = Hash(seed);
                candidate = FromRoll(seed);
                // never a duplicate of the day's set, never something out of reach
            } while (usedTypes.Contains(candidate.Type) || !IsReachable(candidate.Type, frontier));
            return candidate;
        }

        /// <summary>
        /// Can a player whose frontier is <paramref name="frontier"/> make ANY progress
        /// on this mission type? A level-3 player asked to crumble five chocolates
        /// (first seen around level 66) holds a mission that is not hard, just dead —
        /// and it eats one of three daily slots until the single reroll is spent on it.
        ///
        /// The thresholds are read off <see cref="LevelCurve"/> rather than typed in,
        /// so re-authoring where a mechanic debuts moves the missions with it.
        ///
        /// INVARIANT: at every frontier at least <see cref="DailyCount"/> + 1 types must
        /// stay reachable — three distinct ones for the day plus a fourth for the
        /// reroll — or the roll loops above never terminate. Five types have no content
        /// gate at all, and a test pins that margin.
        /// </summary>
        public static bool IsReachable(MissionType type, int frontier)
        {
            if (frontier <= 0)
                return true; // unknown frontier: behave exactly as before the filter

            switch (type)
            {
                case MissionType.ClearJelly: return frontier >= FirstLevelWith(Mechanic.Jelly);
                case MissionType.ClearChocolate: return frontier >= FirstLevelWith(Mechanic.Chocolate);
                case MissionType.CollectIngredients: return frontier >= FirstLevelWith(Mechanic.Ingredient);
                default: return true;
            }
        }

        /// <summary>
        /// Every type the daily roll can produce — read off <see cref="FromRoll"/>'s own
        /// cases, so the reachability invariant above is checked against the real table
        /// rather than a second list that could drift from it.
        /// </summary>
        public static IReadOnlyCollection<MissionType> RollableTypes
        {
            get
            {
                var types = new HashSet<MissionType>();
                for (uint roll = 0; roll < RollCases; roll++)
                    types.Add(FromRoll(roll).Type);
                return types;
            }
        }

        private enum Mechanic { Jelly, Chocolate, Ingredient }

        private static int[] _firstLevelWith;

        /// <summary>
        /// The first campaign level that contains a mechanic; int.MaxValue if none does.
        /// One scan of the curve, cached — the answer cannot change at runtime.
        /// </summary>
        private static int FirstLevelWith(Mechanic mechanic)
        {
            if (_firstLevelWith == null)
            {
                var first = new[] { int.MaxValue, int.MaxValue, int.MaxValue };
                for (int level = 1; level <= LevelCurve.LevelCount; level++)
                {
                    LevelParameters p = LevelCurve.For(level);
                    if (p.JellyRows > 0 && first[(int)Mechanic.Jelly] == int.MaxValue)
                        first[(int)Mechanic.Jelly] = level;
                    // A fountain mints chocolate on its own, so it counts as chocolate.
                    if ((p.ChocolateCells.Count > 0 || p.FountainCells.Count > 0) &&
                        first[(int)Mechanic.Chocolate] == int.MaxValue)
                        first[(int)Mechanic.Chocolate] = level;
                    if (p.IngredientCount > 0 && first[(int)Mechanic.Ingredient] == int.MaxValue)
                        first[(int)Mechanic.Ingredient] = level;
                }
                _firstLevelWith = first;
            }
            return _firstLevelWith[(int)mechanic];
        }

        /// <summary>One bigger weekly mission, cycling three evergreen shapes.</summary>
        public static MissionDef WeeklyFor(int weekNumber)
        {
            switch (((weekNumber % 3) + 3) % 3)
            {
                case 0: return new MissionDef(MissionType.WinLevels, 0, 12);
                case 1: return new MissionDef(MissionType.ClearColor, ((weekNumber / 3) % 5 + 5) % 5, 250);
                default: return new MissionDef(MissionType.MakeStriped, 0, 20);
            }
        }

        /// <summary>How much a resolved wave advances a mission (WinLevels counts elsewhere).</summary>
        public static int CountFor(MissionDef def, CascadeStep step)
        {
            switch (def.Type)
            {
                case MissionType.ClearColor:
                {
                    // Deliberately Cleared-only (no creation-cell credit, unlike
                    // ObjectiveTracker): finale steps flow through here, and their
                    // pure-morph conversions are cleared by later blast waves —
                    // crediting the morph too would double-count.
                    int count = 0;
                    foreach (ClearedTile cleared in step.Cleared)
                        if (cleared.Tile.ColorIndex == def.Param)
                            count++;
                    return count;
                }
                case MissionType.MakeStriped:
                {
                    // The finale mints striped candies FOR the player (4 per 5 leftover
                    // moves) — crediting those would let a comfortable win finish a
                    // "make 20 striped" mission on its own, without a single 4-run.
                    if (step.IsFinale)
                        return 0;
                    int count = 0;
                    foreach (SpecialCreation creation in step.Creations)
                        if (creation.Created.Kind == TileKind.StripedH || creation.Created.Kind == TileKind.StripedV)
                            count++;
                    return count;
                }
                case MissionType.MakeWrapped:
                {
                    if (step.IsFinale) // same payout-is-not-play rule as MakeStriped
                        return 0;
                    int count = 0;
                    foreach (SpecialCreation creation in step.Creations)
                        if (creation.Created.Kind == TileKind.Wrapped)
                            count++;
                    return count;
                }
                case MissionType.MakeCombo:
                {
                    int count = 0;
                    foreach (Detonation detonation in step.Detonations)
                    {
                        switch (detonation.Kind)
                        {
                            case DetonationKind.Cross:
                            case DetonationKind.TripleCross:
                            case DetonationKind.Blast5x5:
                            case DetonationKind.ColorClear:
                            case DetonationKind.BoardClear:
                                count++;
                                break;
                        }
                    }
                    return count;
                }
                case MissionType.ClearJelly:
                    return step.JellyHits.Count;
                case MissionType.ClearChocolate:
                {
                    int count = 0;
                    foreach (ClearedTile cleared in step.Cleared)
                        if (cleared.Tile.Kind == TileKind.Chocolate)
                            count++;
                    return count;
                }
                case MissionType.CollectIngredients:
                    return step.IngredientExits.Count;
                default:
                    return 0; // WinLevels advances on level outcomes, not waves
            }
        }

        /// <summary>Booster payout per daily slot; the weekly pays big and shields the streak.</summary>
        public static ChestReward RewardForSlot(int slot)
        {
            switch (((slot % 3) + 3) % 3)
            {
                case 0: return new ChestReward(1, 0, 0, 0);
                case 1: return new ChestReward(0, 1, 0, 0);
                default: return new ChestReward(0, 0, 1, 0);
            }
        }

        public static ChestReward WeeklyReward() => new ChestReward(2, 2, 2, 1);

        /// <summary>How many cases <see cref="FromRoll"/> switches over.</summary>
        private const uint RollCases = 8u;

        private static MissionDef FromRoll(uint seed)
        {
            switch (seed % RollCases)
            {
                case 0: return new MissionDef(MissionType.ClearColor, (int)(Hash(seed) % 5u), 60);
                case 1: return new MissionDef(MissionType.ClearColor, (int)(Hash(seed) % 5u), 80);
                case 2: return new MissionDef(MissionType.MakeStriped, 0, 6);
                case 3: return new MissionDef(MissionType.MakeWrapped, 0, 4);
                case 4: return new MissionDef(MissionType.MakeCombo, 0, 3);
                case 5: return new MissionDef(MissionType.WinLevels, 0, 3);
                case 6: return new MissionDef(MissionType.ClearJelly, 0, 12);
                default: return new MissionDef(MissionType.ClearChocolate, 0, 5);
            }
        }

        private static uint Hash(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }
}
