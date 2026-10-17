import nodemailer from "nodemailer";
import path from "path";
import fs from "fs";
import psn from "psn-api";
import "dotenv/config";

const AUTH_CACHE_PATH = path.resolve("psnAuthCache.json");
const NPSSO_LIFETIME_DAYS = 60;
const NPSSO_WARNING_DAYS = 5;   // start warning when 5 days remain 
const EXPECTED_PSN_USERNAME = "Eternitype_"
const WARNING_FLAG_PATH = path.resolve(".npsso-warning-date");

async function saveAuthorisation(auth: any) {
    auth.expiresAt = Date.now() + auth.expiresIn * 1000;
    await fs.promises.writeFile(AUTH_CACHE_PATH, JSON.stringify(auth, null, 2), "utf-8");
}

export async function authorisation() {
    console.error("\nStarting PSN authorisation...");

    const issuedAtRaw = process.env.ISSUED_AT;
    let npssoAgeDays: number | null = null;

    if (issuedAtRaw) {
        const issuedAt = new Date(issuedAtRaw);
        if (isNaN(issuedAt.getTime())) {
            console.error(`ISSUED_AT="${issuedAtRaw}" is not a valid date.`);
        } else {
            npssoAgeDays = Math.floor((Date.now() - issuedAt.getTime()) / 86400000);
        }
    } else console.error("ISSUED_AT is missing from .env.");

    if (npssoAgeDays !== null) await maybeSendExpiryWarning(npssoAgeDays);

    const npssoExpiringSoon = npssoAgeDays !== null && npssoAgeDays >= NPSSO_LIFETIME_DAYS - NPSSO_WARNING_DAYS;

    if (fs.existsSync(AUTH_CACHE_PATH)) {
        const data = fs.readFileSync(AUTH_CACHE_PATH, "utf-8");
        let cachedAuthorization = JSON.parse(data);
        const REFRESH_BUFFER = 6 * 60 * 1000; // 6 minutes in ms
        const expiresIn = cachedAuthorization.expiresAt - Date.now();

        if (expiresIn > REFRESH_BUFFER && !npssoExpiringSoon) {
            console.error(`Using cached authorisation (expires in ${Math.round(expiresIn / 60000)} minutes)`);
            return cachedAuthorization;
        }

        if (!npssoExpiringSoon) {
            console.error(`Cached authorisation expires soon (${Math.round(expiresIn / 60000)} minutes), refreshing token...`);

            try {
                const refreshedAuthorization = await psn.exchangeRefreshTokenForAuthTokens(cachedAuthorization.refreshToken);
                await saveAuthorisation(refreshedAuthorization);
                console.error("Successfully refreshed PSN authorisation");
                return refreshedAuthorization;
            } catch (error) {
                console.error("Token refresh failed, falling back to NPSSO authentication");
            }
        }
        else console.error(`Skipping token refresh because NPSSO is stale (${npssoAgeDays!.toFixed(0)} days old).`);
    }
    else console.error("No cached authorisation found");

    console.error("Authenticating using NPSSO token...");
    const myNpsso = process.env.PSN_NPSSO;

    if (!myNpsso) {
        console.error("Missing PSN_NPSSO environment variable");
        throw new Error("Missing PSN_NPSSO environment variable");
    }

    const accessCode = await psn.exchangeNpssoForAccessCode(myNpsso);
    console.error("Successfully exchanged NPSSO for access code");
    const authorization = await psn.exchangeAccessCodeForAuthTokens(accessCode);
    await saveAuthorisation(authorization);
    console.error("Successfully authenticated and saved new authorisation");
    return authorization;
}

async function maybeSendExpiryWarning(npssoAgeDays: number): Promise<void> {
    const daysRemaining = NPSSO_LIFETIME_DAYS - npssoAgeDays;
    if (daysRemaining > NPSSO_WARNING_DAYS) return;

    const today = new Date().toISOString().slice(0, 10);
    const lastSent = fs.existsSync(WARNING_FLAG_PATH)
        ? fs.readFileSync(WARNING_FLAG_PATH, "utf-8").trim()
        : "";

    if (lastSent === today) return;

    try {
        await sendNpssoWarningEmail(daysRemaining);
        fs.writeFileSync(WARNING_FLAG_PATH, today);
    } catch (err) {
        console.error("Failed to send NPSSO expiry email:", err);
        // Don't write the flag — retry on the next request
    }
}

async function sendNpssoWarningEmail(daysRemaining: number): Promise<void> {
    const from = process.env.EMAIL_FROM;
    const pass = process.env.EMAIL_APP_PASSWORD;

    if (!from || !pass) {
        console.error("Email credentials missing; skipping NPSSO warning email.");
        return;
    }

    const days = Math.floor(daysRemaining);

    const phrase =
        days > 0 ? `expires in ${days} day${days === 1 ? "" : "s"}` :
            days === 0 ? "expires today" :
                `expired ${Math.abs(days)} day${Math.abs(days) === 1 ? "" : "s"} ago`;

    const transporter = nodemailer.createTransport({
        service: "gmail",
        auth: { user: from, pass },
    });

    await transporter.sendMail({
        from,
        to: "roryjns123@gmail.com",
        subject: `PSN NPSSO ${phrase}`,
        text:
            `Your PSN NPSSO ${phrase}.\n\n` +
            `Refresh it now:\n` +
            `1. Log in to PSN: https://www.playstation.com\n` +
            `2. Copy the NPSSO value at: https://ca.account.sony.com/api/v1/ssocookie\n` +
            `3. And paste it here: https://mygamingmap.com/refresh-npsso\n`
    });

    console.error(`Sent NPSSO expiry warning email (${phrase}).`);
}

export async function refreshNpsso(npsso: string): Promise<void> {
    if (!npsso || npsso.length !== 64) {
        throw new Error(`NPSSO should be 64 characters, got ${npsso?.length ?? 0}.`);
    }

    // If the NPSSO hasn't changed AND the current one isn't approaching expiry,
    // there's nothing to do.
    const currentNpsso = process.env.PSN_NPSSO;
    const issuedAtRaw = process.env.ISSUED_AT;

    if (currentNpsso === npsso && issuedAtRaw) {
        const issuedAt = new Date(issuedAtRaw);
        if (!isNaN(issuedAt.getTime())) {
            const ageDays = Math.floor(
                (Date.now() - issuedAt.getTime()) / 86400000);
            const daysRemaining = NPSSO_LIFETIME_DAYS - ageDays;

            if (daysRemaining > NPSSO_WARNING_DAYS) {
                console.error(`Current NPSSO is still valid. No need to update it.`);
                return;
            }
        }
    }

    // 1. Verify the NPSSO authenticates and belongs to the expected account.
    const accessCode = await psn.exchangeNpssoForAccessCode(npsso);
    const auth = await psn.exchangeAccessCodeForAuthTokens(accessCode);

    const { profile } = await psn.getProfileFromUserName(
        { accessToken: auth.accessToken },
        "me"
    );

    const expected = EXPECTED_PSN_USERNAME;
    if (expected && profile.onlineId.toLowerCase() !== expected.toLowerCase()) {
        throw new Error(
            `NPSSO belongs to a different user.`);
    }

    // 2. Persist the new NPSSO and update ISSUED_AT.
    const issuedAt = new Date().toISOString().slice(0, 10);

    updateEnv(".env", {
        PSN_NPSSO: `"${npsso}"`,
        ISSUED_AT: `"${issuedAt}"`,
    });

    // 3. Clear stale state so the next request re-authenticates cleanly.
    if (fs.existsSync(AUTH_CACHE_PATH)) fs.unlinkSync(AUTH_CACHE_PATH);
    if (fs.existsSync(WARNING_FLAG_PATH)) fs.unlinkSync(WARNING_FLAG_PATH);

    console.error(`NPSSO successfully updated.`);
}

function updateEnv(file: string, updates: Record<string, string>) {
    const lines = fs.existsSync(file)
        ? fs.readFileSync(file, "utf-8").split("\n")
        : [];

    const remaining = new Set(Object.keys(updates));
    const out: string[] = [];

    for (const line of lines) {
        const m = line.match(/^\s*([A-Z_][A-Z0-9_]*)\s*=/i);
        const key = m?.[1];

        if (key && updates[key] !== undefined) {
            out.push(`${key}=${updates[key]}`);
            remaining.delete(key);
        } else {
            out.push(line);
        }
    }

    for (const key of remaining) out.push(`${key}=${updates[key]}`);

    fs.writeFileSync(file, out.join("\n").replace(/\n*$/, "\n"), "utf-8");
}