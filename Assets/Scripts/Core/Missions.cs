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

        /// <summary>The three missions for a given day — types are always distinct.</summary>
        public static MissionDef[] DailyFor(int dayNumber)
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
                } while (!usedTypes.Add(candidate.Type));
                missions[slot] = candidate;
            }
            return missions;
        }

        /// <summary>The replacement offered when the player rerolls a slot (one per day).</summary>
        public static MissionDef RerollFor(int dayNumber, int slot)
        {
            MissionDef[] daily = DailyFor(dayNumber);
            var usedTypes = new HashSet<MissionType>();
            foreach (MissionDef def in daily)
                usedTypes.Add(def.Type);

            uint seed = Hash((uint)(dayNumber * 97u + slot * 31u + 421u));
            MissionDef candidate;
            do
            {
                seed = Hash(seed);
                candidate = FromRoll(seed);
            } while (usedTypes.Contains(candidate.Type)); // never a duplicate of the day's set
            return candidate;
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
                    int count = 0;
                    foreach (ClearedTile cleared in step.Cleared)
                        if (cleared.Tile.ColorIndex == def.Param)
                            count++;
                    return count;
                }
                case MissionType.MakeStriped:
                {
                    int count = 0;
                    foreach (SpecialCreation creation in step.Creations)
                        if (creation.Created.Kind == TileKind.StripedH || creation.Created.Kind == TileKind.StripedV)
                            count++;
                    return count;
                }
                case MissionType.MakeWrapped:
                {
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

        private static MissionDef FromRoll(uint seed)
        {
            switch (seed % 8u)
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
