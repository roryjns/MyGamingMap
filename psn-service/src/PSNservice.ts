import fs from "fs";
import path from "path";
import psn from "psn-api";
import "dotenv/config";

const AUTH_CACHE_PATH = path.resolve("psnAuthCache.json");

if (process.argv[2]) {
  const username = process.argv[2];

  try {
    const playerGames = await getPlayerGames(username);
    if (playerGames == null) console.log(JSON.stringify([]));
    else console.log(JSON.stringify(playerGames))
    process.exit(0);
  } catch (error) {
    console.error(error);
    process.exit(1);
  }
}

export async function getPlayerGames(username: string) {
  const auth = await authorization();

  try {
    const user = await getUser(auth.accessToken, username);
    const accountId = user.accountId;
    const isOwnProfile = user.username.toLowerCase() === "me" || user.username.toLowerCase() === "eternitype_";

    let start = performance.now();
    const playData = await getUserPlayData(auth.accessToken, accountId);
    console.error(`getUserPlayData: ${(performance.now() - start).toFixed(0)} ms`);

    start = performance.now();
    const trophyData = await getUserTrophyData(auth.accessToken, accountId);
    console.error(`getUserTrophyData: ${(performance.now() - start).toFixed(0)} ms`);

    start = performance.now();
    const playerGames = await mergePlayAndTrophyData(auth.accessToken, accountId, playData, trophyData);
    console.error(`mergePlayAndTrophyData: ${(performance.now() - start).toFixed(0)} ms`);

    return playerGames;
  }
  catch (error: any) {
    if (error?.message?.includes("User not found")) {
      console.error("User cannot be found. Skipping profile.");
      return [];
    }

    if (error?.message?.includes("hidden his or her games")) {
      console.error("User has hidden their games. Skipping profile.");
      return [];
    }

    throw error;
  }
}

async function saveAuthorization(auth: any) {
  auth.expiresAt = Date.now() + auth.expiresIn * 1000;
  await fs.promises.writeFile(AUTH_CACHE_PATH, JSON.stringify(auth, null, 2), "utf-8");
}

async function authorization() {
  console.error("\nStarting PSN authorisation...");

  if (fs.existsSync(AUTH_CACHE_PATH)) {
    console.error("Found cached authorisation file");
    const data = fs.readFileSync(AUTH_CACHE_PATH, "utf-8");
    let cachedAuthorization = JSON.parse(data);
    const REFRESH_BUFFER = 6 * 60 * 1000; // 8 minutes
    const expiresIn = cachedAuthorization.expiresAt - Date.now();

    if (expiresIn > REFRESH_BUFFER) {
      console.error(`Using cached authorisation (expires in ${Math.round(expiresIn / 60000)} minutes)`);
      return cachedAuthorization;
    }

    console.error(`Cached authorisation expires soon (${Math.round(expiresIn / 60000)} minutes), refreshing token...`);

    try {
      const refreshedAuthorization = await psn.exchangeRefreshTokenForAuthTokens(cachedAuthorization.refreshToken);
      await saveAuthorization(refreshedAuthorization);
      console.error("Successfully refreshed PSN authorisation");
      return refreshedAuthorization;
    } catch (error) {
      console.error("Token refresh failed, falling back to NPSSO authentication");
    }
  } else console.error("No cached authorisation found");

  console.error("Authenticating using NPSSO token...");
  const myNpsso = process.env.PSN_NPSSO;

  if (!myNpsso) {
    console.error("Missing PSN_NPSSO environment variable");
    throw new Error("Missing PSN_NPSSO environment variable");
  }

  const accessCode = await psn.exchangeNpssoForAccessCode(myNpsso);
  console.error("Successfully exchanged NPSSO for access code");
  const authorization = await psn.exchangeAccessCodeForAuthTokens(accessCode);
  await saveAuthorization(authorization);
  console.error("Successfully authenticated and saved new authorisation");
  return authorization;
}

async function getUser(accessToken: string, username: string) {
  try {
    const { profile } = await psn.getProfileFromUserName({ accessToken }, username);
    if (!profile?.accountId) throw new Error(`No account returned for ${username}`);
    console.error(`Successfully retrieved PSN profile for ${profile.onlineId}`);

    return {
      accountId: profile.accountId,
      username: profile.onlineId,
      avatarUrls: profile.avatarUrls[0]?.avatarUrl
    };
  }
  catch (error: any) {
    console.error(`Failed to retrieve PSN profile for ${username}`);

    if (error?.response) console.error("PSN API response error:", { status: error.response.status, data: error.response.data });
    else if (error?.message) console.error("Error:", error.message);
    else console.error("Unknown error:", error);

    throw error;
  }
}

// Get all games that the user has played (currently only for PS4 and PS5 games)
async function getUserPlayData(accessToken: string, accountId: string) {
  let playData: any[] = [];
  let offset = 0;
  let totalTitlesFound = 0;

  const excludedTitles = new Set([
    "SHAREfactory™",
    "Share Factory Studio",
    "Apple TV",
    "BBC iPlayer",
    "Channel 4",
    "Crunchyroll",
    "Disney+",
    "Netflix",
    "NOW",
    "Prime Video",
    "Spotify",
    "YouTube",
    "Media-Player",
    "ITVX",
    "Apple Music",
    "IGN",
    "Media Player",
    "Rad TV",
    "Sky Go",
    "SONY PICTURES CORE",
    "Twitch",
    "Tubi: Free Movies & TV",
    "EA Play Hub",
    "Demand 5",
    "PlayStationHome",
    "DAZN",
    "Amazon Freevee",
    "Animax",
    "HBO Max",
    "PlayStationVR Demo Disc",
    "PlayStation Home"
  ]);

  console.error("Fetching play data...")

  while (true) { // 1 API call per 200 games
    const response = await retry(() =>
      psn.getUserPlayedGames(
        { accessToken },
        accountId,
        {
          limit: 200,
          offset
        }
      )
    );

    totalTitlesFound += response.titles.length;

    for (const game of response.titles) {
      if (excludedTitles.has(game.concept.name)) continue;

      const playHours = durationToHours(game.playDuration);
      const playCount = game.playCount > 0 ? game.playCount : (playHours > 0 ? 1 : 0);

      playData.push({
        titleId: game.titleId,
        name: normaliseName(game.concept.name),
        platform: titleIdToPlatform(game.titleId),
        conceptId: game.concept.id,
        imageUrl: game.imageUrl,
        playHours,
        playCount,
        firstPlayed: game.firstPlayedDateTime,
        lastPlayed: game.lastPlayedDateTime
      });
    }

    if (response.nextOffset == null) break;

    offset = response.nextOffset;
  }

  playData.sort((a, b) => a.name.localeCompare(b.name));
  console.error(`Relevant play data found: ${playData.length}/${totalTitlesFound}`);
  //fs.promises.writeFile("./debug/userPlayedGames.json", JSON.stringify(playData, null, 2));
  return playData;
}

// Get all games where the user has earned at least one trophy
async function getUserTrophyData(accessToken: string, accountId: string) {
  let trophyData: any[] = [];
  let offset = 0;
  let totalTrophyTitlesFound = 0;

  console.error("Fetching trophy data...")

  while (true) { // 1 API call per 800 games
    const response = await retry(() =>
      psn.getUserTitles(
        { accessToken },
        accountId,
        {
          limit: 800,
          offset
        }
      )
    );

    const trophyTitles = response.trophyTitles ?? [];

    totalTrophyTitlesFound += trophyTitles.length;

    for (const game of trophyTitles) {
      // Ignore PS3 games with no earned trophies
      // PS3/PS Vita trophyGames never have a playedGame, so there's little to no player info to analyse without trophies
      if ((game.trophyTitlePlatform === "PS3" || game.trophyTitlePlatform === "PSVITA") && game.progress == 0) continue;

      trophyData.push({
        name: normaliseName(game.trophyTitleName)
          .trim()
          .replace(/^\((.*)\)$/, "$1")
          .replace(/\s*(Trophy Set|Trophy Pack|Trophies|trophies.|Trophy|Set [12])\s*/gi, " ")
          .replace(/\s*-\s*$/, "")
          .replace(/\s{2,}/g, " ")
          .trim(),
        npCommunicationId: game.npCommunicationId.replace(/_00$/i, ""),
        trophyTitleIconUrl: game.trophyTitleIconUrl,
        platform: game.trophyTitlePlatform,
        progress: game.progress,
        earnedTrophies: game.earnedTrophies,
        definedTrophies: game.definedTrophies,
        lastTrophyEarned: game.lastUpdatedDateTime,
      });
    }

    if (response.nextOffset == null) break;

    offset = response.nextOffset;
  }

  trophyData.sort((a, b) => a.name.localeCompare(b.name));
  console.error(`Relevant trophy data found: ${trophyData.length}/${totalTrophyTitlesFound}`);
  //fs.promises.writeFile("./debug/userTrophyGames.json", JSON.stringify(trophyData, null, 2));
  return trophyData;
}

// 1 API call per 5 games
async function getUserTrophiesForSpecificTitle(accessToken: string, accountId: string, titleIds: string) {

  const response = await retry(() =>
    psn.getUserTrophiesForSpecificTitle(
      { accessToken },
      accountId,
      {
        npTitleIds: titleIds,
        includeNotEarnedTrophyIds: false,
      }
    )
  );

  const games = (response.titles ?? []).flatMap((title: any) =>
    (title.trophyTitles ?? []).map((game: any) => ({
      name: normaliseName(game.trophyTitleName)
        .trim()
        .replace(/^\((.*)\)$/, "$1")
        .replace(/\s*(Trophy Set|Trophy Pack|Trophies|trophies.|Trophy|Set [12])\s*/gi, " ")
        .replace(/\s*-\s*$/, "")
        .replace(/\s{2,}/g, " ")
        .trim(),
      sourceTitleId: title.npTitleId,
      npCommunicationId: game.npCommunicationId?.replace(/_00$/i, ""),
      trophyTitleIconUrl: game.trophyTitleIconUrl ?? null,
      platform: titleIdToPlatform(title.npTitleId),
      progress: game.progress ?? null,
      earnedTrophies: game.earnedTrophies ?? null,
      definedTrophies: game.definedTrophies ?? null,
      lastTrophyEarned: game.lastUpdatedDateTime,
    }))
  );

  return games;
}

async function mergePlayAndTrophyData(accessToken: string, accountId: string, playedGames: any[], userTitles: any[]) {
  if (playedGames.length == 0 && userTitles.length == 0) {
    console.error("Could not find any game data for this user. Their games and trophy data are likely set to private.")
    return;
  }

  let mergedGames: any[] = [];
  const unmatchedGames: any[] = [];
  const titleMap = new Map<string, any[]>();
  const matchedTrophyGames = new Set<string>();

  // Create title map
  for (const title of userTitles) {
    const key = `${title.name.toLowerCase()}|${title.platform}`;
    if (!titleMap.has(key)) titleMap.set(key, []);
    titleMap.get(key)!.push(title);
  }

  // First pass: exact name + platform matching
  for (const game of playedGames) {
    const matches = titleMap.get(`${game.name.toLowerCase()}|${game.platform}`) ?? [];

    if (matches.length) {
      const normalisedTrophies = normaliseTrophyData(matches);

      mergedGames.push({
        ...game,
        name: matches.length === 1
          ? normaliseName(matches[0].name)
          : game.name,
        trophyData: normalisedTrophies
      });

      for (const trophy of matches) matchedTrophyGames.add(trophy.npCommunicationId);
    }
    else unmatchedGames.push(game);
  }

  console.error(`First pass (exact name + platform): ${mergedGames.length}/${playedGames.length} games matched`);

  let lowPlaytimeRemoved = 0;
  let lowProgressRemoved = 0;

  // Second pass: lookup unmatched played games in batches of 5
  const batches = [];
  const batchSize = 5;

  for (let i = 0; i < unmatchedGames.length; i += batchSize) batches.push(unmatchedGames.slice(i, i + batchSize));

  const trophyTitles = (
    await Promise.all(
      batches.map(batch =>
        getUserTrophiesForSpecificTitle(
          accessToken,
          accountId,
          batch.map(game => game.titleId).join(",")
        )
      )
    )
  ).flat();

  const trophyMap = new Map<string, any[]>();

  // Create trophy title map
  for (const trophy of trophyTitles) {
    const key = trophy.sourceTitleId.toLowerCase();
    if (!trophyMap.has(key)) trophyMap.set(key, []);
    trophyMap.get(key)!.push(trophy);
  }

  for (const game of unmatchedGames) {
    const trophyData = trophyMap.get(game.titleId.toLowerCase()) ?? [];

    if (trophyData.length > 0) {
      for (const trophy of trophyData) matchedTrophyGames.add(trophy.npCommunicationId);
    }

    // Skip low playtime games with no trophy data - they are likely to be irrelevant
    if (trophyData.length === 0 && game.playHours <= 1) {
      lowPlaytimeRemoved++;
      continue;
    }

    const normalisedTrophies = normaliseTrophyData(trophyData);
    const gameName = normalisedTrophies.length === 1 ? normalisedTrophies[0]?.name ?? game.name : game.name;

    mergedGames.push({
      ...game,
      name: gameName,
      trophyData: normalisedTrophies
    });
  }

  const gamesWithTrophies = mergedGames.filter(game => game.trophyData && game.trophyData.length > 0);
  console.error(`Second pass (PSN trophy lookup): ${gamesWithTrophies.length}/${playedGames.length} games matched`);
  const unmatchedTrophyGames = userTitles.filter(trophy => !matchedTrophyGames.has(trophy.npCommunicationId));

  // Remove trophy only games with low progress - likely irrelevant
  const trophyOnlyGames = unmatchedTrophyGames.filter(
    trophy => {
      if (trophy.progress <= 5) {
        lowProgressRemoved++;
        return false;
      }

      return true;
    }
  );

  console.error(
    `Filtered irrelevant data: ${lowPlaytimeRemoved} play data only removed (<=1 hour), ${lowProgressRemoved} trophy data only removed (<=5% progress)`
  );

  const gamesBeforeMerge = mergedGames.length;
  mergedGames = mergeGamesWithSameTrophies(mergedGames);
  console.error(`Merged ${gamesBeforeMerge - mergedGames.length} games with identical trophy lists (regional versions, demos, soundtrack apps etc.)`);

  // Add play data to trophy data, inferring what we can..
  for (const trophy of trophyOnlyGames) {
    mergedGames.push({
      titleId: null,
      name: normaliseName(trophy.name),
      platform: trophy.platform,
      conceptId: null,
      imageUrl: trophy.trophyTitleIconUrl,
      playHours: null,
      playCount: null,
      firstPlayed: null,
      lastPlayed: trophy.lastTrophyEarned,
      trophyData: [
        trophy
      ]
    });
  }

  mergedGames.sort((a, b) => a.name.replace(/^The\s+/i, "").localeCompare(b.name.replace(/^The\s+/i, "")));
  const playDataOnly = mergedGames.filter(game => (!game.trophyData || game.trophyData.length === 0) && game.playHours !== null);
  const trophyDataOnly = mergedGames.filter(game => game.playHours === null && game.trophyData && game.trophyData.length > 0);
  const mergedWithTrophies = mergedGames.filter(game => game.trophyData && game.trophyData.length > 0 && game.playHours !== null);
  console.error(`Merge complete: ${mergedGames.length} games (${mergedWithTrophies.length} merged + ${playDataOnly.length} play data only + ${trophyDataOnly.length} trophy data only)`);
  await fs.promises.writeFile("./debug/playerGames.json", JSON.stringify(mergedGames, null, 2));
  return mergedGames;
}

function mergeGamesWithSameTrophies(games: any[]) {
  const merged = new Map<string, any>();

  for (const game of games) {
    const key = getTrophyKey(game);

    // Leave unmatched games alone
    if (!key) {
      merged.set(`unmatched:${game.titleId}`, game);
      continue;
    }

    const existing = merged.get(key);

    if (!existing) {
      merged.set(key, { ...game });
      continue;
    }

    // Combine play statistics safely
    existing.playHours = (existing.playHours ?? 0) + (game.playHours ?? 0);
    existing.playCount = (existing.playCount ?? 0) + (game.playCount ?? 0);

    // Earliest first play
    if (game.firstPlayed && (!existing.firstPlayed || game.firstPlayed < existing.firstPlayed))
      existing.firstPlayed = game.firstPlayed;

    // Latest last play
    if (game.lastPlayed && (!existing.lastPlayed || game.lastPlayed > existing.lastPlayed))
      existing.lastPlayed = game.lastPlayed;
  }

  return [...merged.values()];
}

function durationToHours(duration: string): number {
  const match = duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);

  if (!match) return 0;

  const hours = Number(match[1] ?? 0);
  const minutes = Number(match[2] ?? 0);
  const seconds = Number(match[3] ?? 0);

  return Number((hours + minutes / 60 + seconds / 3600).toFixed(2));
}

function titleIdToPlatform(titleId: string): string | null {
  if (titleId.startsWith("CUSA")) return "PS4";
  if (titleId.startsWith("PPSA")) return "PS5";
  if (titleId.startsWith("PCSA")) return "PS Vita";
  return null;
}

function normaliseName(name: string): string {
  return name
    .replace(/[™®©]/g, "")
    // Remove platform-specific editions first
    .replace(/\s*:?\s*(PlayStationVita|PlayStation\s*3|PlayStation\s*4|PlayStation\s*5)\s+Edition/gi, "")
    // Remove platform names
    .replace(/\s*\(?\s*(for\s+PS5|PS4|PS5|PS4\s*&\s*PS5|PlayStationVita|PS Vita|PlayStation\s*3|PlayStation\s*4|PlayStation\s*5)\s*\)?/gi, "")
    // Remove other editions
    .replace(/\s*:?\s*(Game of the Year Edition|Ultimate Edition|Deluxe Edition|Deluxe Editiom|Collector's Edition|Limited Edition|Extended Edition|Console Edition|PlayStationVita Edition|PlayStation3 Edition|PlayStation4 Edition|Playstation5 Edition|Premium Edition|Special Edition)\s*$/gi, "")
    .replace(/\s+\(?\s*(ASIA|EU|JA|NA|JP)\s*\)?$/gi, "")
    .replace(/\s*:\s*Edition\s*$/gi, "")
    .replace(/\s+editon$/gi, " edition")
    .replace(/\s+HD\s*$/gi, "")
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '\'')
    .trim()
}

function normaliseTrophyData(trophies: any[]) {
  return trophies.map(trophy => ({
    name: normaliseName(trophy.name),
    npCommunicationId: trophy.npCommunicationId.replace(/_00$/i, ""),
    trophyTitleIconUrl: trophy.trophyTitleIconUrl,
    platform: trophy.platform,
    progress: trophy.progress,
    earnedTrophies: trophy.earnedTrophies,
    definedTrophies: trophy.definedTrophies,
    lastTrophyEarned: trophy.lastTrophyEarned,
  }));
}

function getTrophyKey(game: any) {
  if (!game.trophyData || game.trophyData.length === 0) return null;

  return game.trophyData.map((trophy: any) => trophy.npCommunicationId).sort().join("|");
}

async function retry<T>(operation: () => Promise<T>): Promise<T> {
  for (let attempt = 1; attempt <= 5; attempt++) {
    try {
      return await operation();
    }
    catch (err: any) {
      const message = err?.message ?? "";

      // Don't retry permanent PSN privacy errors
      if (message.includes("hidden his or her games") || message.includes("hidden his or her trophies")) throw err;

      if (attempt === 5) throw err;

      console.error(`Retry ${attempt}/5`);
      await new Promise(r => setTimeout(r, attempt * 5000));
    }
  }

  throw new Error();
}