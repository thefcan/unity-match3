using System;

namespace Match3.Core
{
    /// <summary>
    /// The "Candy Town" decor meta, Royal-Match-lite: one scene per chapter that
    /// restores itself AUTOMATICALLY as the chapter's stars accumulate — no choices,
    /// no currency, purely cosmetic (the RM lesson: a meta with zero decisions).
    /// Stateless: the stage is derived from the chapter's star total every time.
    /// </summary>
    public static class TownRules
    {
        /// <summary>Build stages per chapter scene (ground → shops → lights → rooftops → fireworks).</summary>
        public const int StageCount = 5;

        /// <summary>Stars (of the chapter's 60) that unlock each successive stage.</summary>
        public const int StarsPerStage = 12;

        /// <summary>0 = empty lot, 5 = fully lit town with fireworks.</summary>
        public static int StageFor(int chapterStars) =>
            Math.Clamp(chapterStars / StarsPerStage, 0, StageCount);

        /// <summary>Stars still missing for the next stage; 0 when the scene is complete.</summary>
        public static int StarsToNextStage(int chapterStars)
        {
            int stage = StageFor(chapterStars);
            if (stage >= StageCount)
                return 0;
            return (stage + 1) * StarsPerStage - chapterStars;
        }

        /// <summary>A global stage index across chapters (for "new stage" celebration tracking).</summary>
        public static int GlobalStage(int chapter, int chapterStars) =>
            chapter * StageCount + StageFor(chapterStars);
    }
}
