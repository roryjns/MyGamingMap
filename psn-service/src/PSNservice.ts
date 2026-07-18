import fs from "fs";
import path from "path";
import psn from "psn-api";
import "dotenv/config";

const AUTH_CACHE_PATH = path.resolve("psnAuthCache.json");

if (process.argv[2]) {
  const username = process.argv[2];

  try {
    const playerGames = await getPlayerGames(username);
    console.log(JSON.stringify(playerGames))
    process.exit(0);
  } catch (error) {
    console.error(error);
    process.exit(1);
  }
}

export async function getPlayerGames(username: string) {
  const auth = await authorization();
  const accountId = await getUser(auth.accessToken, username);
  const playData = await getUserPlayData(auth.accessToken, accountId);
  const trophyData = await getUserTrophyData(auth.accessToken, accountId);
  const playerGames = await mergePlayAndTrophyData(auth.accessToken, accountId, playData, trophyData);
  return playerGames;
}

async function saveAuthorization(auth: any) {
  auth.expiresAt = Date.now() + auth.expiresIn * 1000;
  await fs.promises.writeFile(AUTH_CACHE_PATH, JSON.stringify(auth, null, 2), "utf-8");
}

async function authorization() {
  // Attempt to find cached authorisation
  if (fs.existsSync(AUTH_CACHE_PATH)) {
    const data = fs.readFileSync(AUTH_CACHE_PATH, "utf-8");
    const cachedAuthorization = JSON.parse(data);
    const REFRESH_BUFFER = 10 * 60 * 1000; // 10 minutes

    // Access token is still valid for the next 10 minutes
    if (Date.now() < cachedAuthorization.expiresAt - REFRESH_BUFFER) return cachedAuthorization;

    // 2. Access token expired - try refreshing
    try {
      const refreshedAuthorization = await psn.exchangeRefreshTokenForAuthTokens(cachedAuthorization.refreshToken);
      await saveAuthorization(refreshedAuthorization);
      return refreshedAuthorization;
    } catch {
      console.log("Refresh token expired, falling back to NPSSO");
    }
  }

  else {
    // No cache or refresh failed - authenticate with NPSSO
    const myNpsso = process.env.PSN_NPSSO!;
    if (!myNpsso) throw new Error("Missing PSN_NPSSO environment variable");
    const accessCode = await psn.exchangeNpssoForAccessCode(myNpsso);
    const authorization = await psn.exchangeAccessCodeForAuthTokens(accessCode);
    await saveAuthorization(authorization);
    return authorization;
  }
}

async function getUser(accessToken: string, username: string) {
  const { profile } = await psn.getProfileFromUserName({ accessToken }, username);
  return profile.accountId;
}

// Get all games that the user has played, but seems to exclude physical and game shared games for now...
async function getUserPlayData(accessToken: string, accountId: string) {
  let playData: any[] = [];
  let offset = 0;

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
    "EA Play Hub"
  ]);

  while (true) { // 1 API call per 200 games
    const response = await psn.getUserPlayedGames(
      { accessToken },
      accountId,
      {
        limit: 200,
        offset: offset
      }
    );

    const games = [];

    for (const game of response.titles) {
      if (excludedTitles.has(game.concept.name)) continue;

      const playHours = durationToHours(game.playDuration);
      if (playHours < 0.1) continue;

      const playCount = game.playCount > 0 ? game.playCount : (playHours > 0 ? 1 : 0);

      games.push({
        titleId: game.titleId,
        name: normaliseName(game.concept.name),
        platform: titleIdToPlatform(game.titleId),
        conceptId: game.concept.id,
        service: game.service,
        imageUrl: game.imageUrl,
        playHours,
        playCount,
        averageSessionMinutes: (playHours * 60) / playCount,
        firstPlayed: extractDate(game.firstPlayedDateTime),
        lastPlayed: extractDate(game.lastPlayedDateTime)
      });
    }

    // Add current page to all games
    playData.push(...games);

    if (response.nextOffset == null) break;

    offset = response.nextOffset;
  }

  playData.sort((a, b) => a.name.localeCompare(b.name));
  //console.log(`Play data found: ${playData.length}`);
  //fs.promises.writeFile("./output/userPlayedGames.json", JSON.stringify(playData, null, 2));
  return playData;
}

// Get all games where the user has earned at least one trophy
async function getUserTrophyData(accessToken: string, accountId: string) {
  let trophyData: any[] = [];
  let offset = 0;

  while (true) { // 1 API call per 200 games
    const response = await psn.getUserTitles(
      { accessToken },
      accountId,
      {
        limit: 200,
        offset
      }
    );

    for (const game of response.trophyTitles) {
      trophyData.push({
        name: normaliseName(game.trophyTitleName).replace(/\s+Trophies$/i, ""),
        npCommunicationId: game.npCommunicationId,
        trophyTitleIconUrl: game.trophyTitleIconUrl,
        platform: game.trophyTitlePlatform,
        progress: game.progress,
        earnedTrophies: game.earnedTrophies
      });
    }

    if (response.nextOffset == null) break;

    offset = response.nextOffset;
  }

  trophyData.sort((a, b) => a.name.localeCompare(b.name));
  //console.log(`Trophy data found: ${trophyData.length}`);
  //fs.promises.writeFile("./output/userTrophyGames.json", JSON.stringify(trophyData, null, 2));
  return trophyData;
}

// 1 API call per 5 games
async function getUserTrophiesForSpecificTitle(accessToken: string, accountId: string, titleIds: string) {
  const response = await psn.getUserTrophiesForSpecificTitle(
    { accessToken },
    accountId,
    {
      npTitleIds: titleIds,
      includeNotEarnedTrophyIds: false,
    }
  );

  const games = response.titles.flatMap((title: any) =>
    (title.trophyTitles ?? []).map((game: any) => ({
      name: game.trophyTitleName
        ?.replace(/\s+Trophies$/i, "")
        .replace(/[™®©]/g, "")
        .trim() ?? null,

      titleId: title.npTitleId,
      npCommunicationId: game.npCommunicationId,
      trophyTitleIconUrl: game.trophyTitleIconUrl,
      platform: titleIdToPlatform(title.npTitleId),
      progress: game.progress ?? null,
      earnedTrophies: game.earnedTrophies ?? null,
    }))
  );

  return games;
}

async function mergePlayAndTrophyData(accessToken: string, accountId: string, playedGames: any[], userTitles: any[]) {
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

      for (const trophy of matches) {
        matchedTrophyGames.add(trophy.npCommunicationId);
      }
    } else {
      unmatchedGames.push(game);
    }
  }

  //console.log(`Play data matched with trophy data by name + platform: ${mergedGames.length}`);

  // Second pass: lookup unmatched played games in batches of 5
  const batches = [];
  const batchSize = 5;
  let trophyLookupMatches = 0;

  for (let i = 0; i < unmatchedGames.length; i += batchSize) {
    batches.push(unmatchedGames.slice(i, i + batchSize));
  }

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
    const key = trophy.titleId.toLowerCase();
    if (!trophyMap.has(key)) trophyMap.set(key, []);
    trophyMap.get(key)!.push(trophy);
  }

  for (const game of unmatchedGames) {
    const trophyData = trophyMap.get(game.titleId.toLowerCase()) ?? [];

    if (trophyData.length > 0) {
      trophyLookupMatches++;

      for (const trophy of trophyData) {
        matchedTrophyGames.add(trophy.npCommunicationId);
      }
    }

    // Skip low-playtime games with no trophy data - they are likely to be irrelevant
    if (trophyData.length === 0 && game.playHours <= 0.5) continue;

    const normalisedTrophies = normaliseTrophyData(trophyData);

    mergedGames.push({
      ...game,
      name: normalisedTrophies.length === 1
        ? normalisedTrophies[0].name
        : game.name,
      trophyData: normalisedTrophies
    });
  }

  //console.log(`Play data matched with trophy data by lookup: ${trophyLookupMatches}`);

  const unmatchedAfterLookup = mergedGames.filter(game => !game.trophyData || game.trophyData.length === 0);
  /*
  console.log(`Play data without trophy data even after lookup: ${unmatchedAfterLookup.length}`);

  console.table(
    unmatchedAfterLookup
      .map(result => ({
        "Name": result.name,
        "ID": result.titleId,
        "Platform": result.platform,
        "Playtime": result.playHours
      }))
  );
  */

  mergedGames = mergeGamesWithSameTrophies(mergedGames);

  const trophyOnlyGames = userTitles.filter(trophy => !matchedTrophyGames.has(trophy.npCommunicationId));
  //console.log(`Trophy data without play data: ${trophyOnlyGames.length}`);

  // Add play data to trophy data, inferring what we can..
  for (const trophy of trophyOnlyGames) {
    mergedGames.push({
      titleId: null,
      name: normaliseName(trophy.name),
      platform: trophy.platform,
      conceptId: null,
      service: null,
      imageUrl: trophy.trophyTitleIconUrl,

      playHours: null,
      playCount: null,
      averageSessionMinutes: null,

      firstPlayed: null,
      lastPlayed: null,

      trophyData: [
        trophy
      ]
    });
  }

  mergedGames.sort((a, b) => a.name.localeCompare(b.name));
  await fs.promises.writeFile("./debug/playerGames.json", JSON.stringify(mergedGames, null, 2));
  //console.log(`Final merged game data count: ${mergedGames.length}`);
  return mergedGames;
}

function normaliseTrophyData(trophies: any[]) {
  return trophies.map(trophy => ({
    ...trophy,
    name: normaliseName(trophy.name)
  }));
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
    if (game.firstPlayed && (!existing.firstPlayed || game.firstPlayed < existing.firstPlayed)) {
      existing.firstPlayed = game.firstPlayed;
    }

    // Latest last play
    if (game.lastPlayed && (!existing.lastPlayed || game.lastPlayed > existing.lastPlayed)) {
      existing.lastPlayed = game.lastPlayed;
    }
  }

  // Recalculate averages
  for (const game of merged.values()) {
    if (game.playCount > 0) {
      game.averageSessionMinutes = (game.playHours * 60) / game.playCount;
    }
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

function extractDate(dateTime: string): string {
  return dateTime.split("T")[0] ?? "";
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
    .replace(/\s*\(?\s*(PS4|PS5|PS4\s*&\s*PS5|PlayStation\s*4|PlayStation\s*5)\s*\)?\s*$/gi, "")
    .replace(/\s*:?\s*(Game of the Year Edition|Ultimate Edition|Deluxe Edition|Definitive Edition|Console Edition)\s*$/gi, "")
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '"')
    .trim()
}

function getTrophyKey(game: any) {
  if (!game.trophyData || game.trophyData.length === 0) return null;

  return game.trophyData
    .map((trophy: any) => trophy.npCommunicationId)
    .sort()
    .join("|");
}