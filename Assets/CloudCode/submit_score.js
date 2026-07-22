/*
 * Cloud Code endpoint: submit_score(score: number, duration: number)
 *
 * The game's "middleware": every time-attack score passes through here, so the
 * plausibility rules run SERVER-SIDE where a modified client can't skip them.
 *
 * KEEP IN SYNC with Match3.Core.ScoreBounds (a unit test pins these numbers):
 *   MaxPlausiblePointsPerSecond = 400
 *   MinRunSeconds               = 5
 *
 * Deploy: Unity Editor > Window > Deployment (or paste into the UGS dashboard's
 * Cloud Code editor as "submit_score"). See docs/UGS-SETUP.md.
 */

const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

const LEADERBOARD_ID = "time-attack-score";
const MAX_POINTS_PER_SECOND = 400;
const MIN_RUN_SECONDS = 5;

module.exports = async ({ params, context, logger }) => {
  const { score, duration } = params;

  if (typeof score !== "number" || typeof duration !== "number" ||
      !Number.isFinite(score) || !Number.isFinite(duration)) {
    throw Error("submit_score: score and duration must be numbers");
  }
  if (duration < MIN_RUN_SECONDS) {
    logger.info(`rejected: run too short (${duration}s)`);
    throw Error("submit_score: run too short");
  }
  if (score < 0 || score > duration * MAX_POINTS_PER_SECOND) {
    logger.info(`rejected: implausible score ${score} for ${duration}s`);
    throw Error("submit_score: implausible score");
  }

  const { projectId, playerId, accessToken } = context;
  const leaderboards = new LeaderboardsApi({ accessToken });
  const result = await leaderboards.addLeaderboardPlayerScore(
    projectId, LEADERBOARD_ID, playerId, { score: Math.floor(score) });

  return JSON.stringify({ rank: result.data.rank, score: result.data.score });
};

module.exports.params = { score: "NUMERIC", duration: "NUMERIC" };
