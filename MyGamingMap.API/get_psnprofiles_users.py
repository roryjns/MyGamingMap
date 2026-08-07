from playwright.sync_api import sync_playwright
import time
import random
import json

# In a command prompt, run the following command to start Chrome with remote debugging enabled:
# "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="C:\temp\chrome-debug"
# Then run this script to open a new tab to scrape the top PSNProfiles users.
# Don't click and new links new tab while the script is running
def get_top_users(limit):
    users = []

    with sync_playwright() as p:
        browser = p.chromium.connect_over_cdp(
            "http://127.0.0.1:9222"
        )

        context = browser.contexts[0]

        page = context.new_page()

        current_page = 1

        while len(users) < limit:
            url = f"https://psnprofiles.com/leaderboard/all?page={current_page}"

            print(
                f"Scraping page {current_page}... "
                f"({len(users)}/{limit})"
            )

            page.goto(
                url,
                wait_until="domcontentloaded",
                timeout=60000
            )

            # Give redirects/challenges time to finish
            time.sleep(2)

            if "Attention Required" in page.title() or "blocked" in page.title().lower():
                print("Cloudflare/block detected, waiting...")
                time.sleep(30)
                page.reload(wait_until="domcontentloaded")

            if "Just a moment" in page.title():
                            print("Cloudflare challenge detected")

                            for i in range(12):
                                time.sleep(5)

                                if "Just a moment" not in page.title():
                                    print("Challenge passed")
                                    break

                            else:
                                print("Challenge did not clear")
                                continue

            try:
                page.wait_for_selector(
                    "a[href^='/']",
                    timeout=15000
                )

            except:
                print("Failed loading page")
                print("URL:", page.url)
                print("Title:", page.title())

                try:
                    page.screenshot(
                        path=f"failed_page_{current_page}.png",
                        timeout=5000
                    )
                except:
                    pass

                continue

            links = page.locator("a[href^='/']").all()

            before = len(users)

            for link in links:
                href = link.get_attribute("href")

                if not href:
                    continue

                username = href.strip("/")

                # Ignore non-user links
                ignored_prefixes = (
                    "login",
                    "leaderboard",
                    "account",
                    "about",
                    "guides",
                    "trophies",
                    "sessions",
                    "games",
                    "users"
                )

                if (
                    username == ""
                    or "/" in username
                    or username.startswith("?")
                    or username.startswith(ignored_prefixes)
                ):
                    continue

                if username not in users:
                    users.append(username)

                if len(users) >= limit:
                    break

            if len(users) - before == 0:
                print("No users found, stopping")
                break

            current_page += 1

            if len(users) >= limit:
                break

            delay = random.uniform(3, 5)
            print(f"Waiting {delay:.1f}s")
            time.sleep(delay)

    return users[:limit]


if __name__ == "__main__":
    usernames = get_top_users(10000)

    print(f"\nCollected {len(usernames)} usernames")

    with open(
        "psn_top_10000.json",
        "w",
        encoding="utf-8"
    ) as f:
        json.dump(
            usernames,
            f,
            indent=2
        )

    print("Saved!")