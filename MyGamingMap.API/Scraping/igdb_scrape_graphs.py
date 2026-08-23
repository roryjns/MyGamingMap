import json
import matplotlib.pyplot as plt
from datetime import timedelta
from pathlib import Path

import numpy as np


def plot_rolling(ax, x, y, window=50):
    """
    Plot individual values with a rolling average.
    """

    x = np.array(x)
    y = np.array(y)

    # Individual observations
    ax.scatter(
        x,
        y,
        s=2,
        alpha=0.15
    )

    # Rolling average
    rolling_average = np.convolve(
        y,
        np.ones(window) / window,
        mode="valid"
    )

    rolling_x = np.arange(
        window,
        len(y) + 1
    )

    ax.plot(
        rolling_x,
        rolling_average,
        linewidth=2
    )


# --------------------------------------------------
# Load results
# --------------------------------------------------

results = []

file_path = Path(__file__).parent / "igdb_scrape_results.jsonl"

with open(file_path) as f:
    next(f)  # Ignore first user

    for line in f:
        results.append(json.loads(line))


# --------------------------------------------------
# Profile numbers
# --------------------------------------------------

profile_numbers = np.arange(
    1,
    len(results) + 1
)


# --------------------------------------------------
# Extract database statistics
# --------------------------------------------------

profile_games = np.array([
    r["Result"]["ProfileGames"]
    for r in results
])

database_hit_rate = np.array([
    r["Result"]["DatabaseHits"] /
    r["Result"]["ProfileGames"] * 100
    for r in results
])

name_lookup_rate = np.array([
    r["Result"]["NameLookups"] /
    r["Result"]["ProfileGames"] * 100
    for r in results
])

unmatched_rate = np.array([
    r["Result"]["UnmatchedGames"] /
    r["Result"]["ProfileGames"] * 100
    for r in results
])


# --------------------------------------------------
# Processing time
# --------------------------------------------------

processing_ms = np.array([
    timedelta(
        hours=int(r["Result"]["ProcessingTime"][0:2]),
        minutes=int(r["Result"]["ProcessingTime"][3:5]),
        seconds=float(r["Result"]["ProcessingTime"][6:])
    ).total_seconds() * 1000
    for r in results
])


# ==================================================
#
# FIGURE 2
# Effect of scraping progression
#
# ==================================================

fig, axes = plt.subplots(
    2,
    2,
    figsize=(10, 7)
)


# --------------------------------------------------
# Database hit rate
# --------------------------------------------------

plot_rolling(
    axes[0, 0],
    profile_numbers,
    database_hit_rate,
    window=50
)

axes[0, 0].set_xlabel("Profiles scraped")
axes[0, 0].set_ylabel("Database hit rate (%)")
axes[0, 0].set_title("Database hit rate")
axes[0, 0].grid()


# --------------------------------------------------
# Lookup rate
# --------------------------------------------------

plot_rolling(
    axes[0, 1],
    profile_numbers,
    name_lookup_rate,
    window=50
)

axes[0, 1].set_xlabel("Profiles scraped")
axes[0, 1].set_ylabel("Lookup rate (%)")
axes[0, 1].set_title("IGDB lookup rate")
axes[0, 1].grid()


# --------------------------------------------------
# Unmatched rate
# --------------------------------------------------

plot_rolling(
    axes[1, 0],
    profile_numbers,
    unmatched_rate,
    window=50
)

axes[1, 0].set_xlabel("Profiles scraped")
axes[1, 0].set_ylabel("Unmatched rate (%)")
axes[1, 0].set_title("Unmatched rate")
axes[1, 0].grid()


# --------------------------------------------------
# Processing time
# --------------------------------------------------

plot_rolling(
    axes[1, 1],
    profile_numbers,
    processing_ms,
    window=50
)

axes[1, 1].set_xlabel("Profiles scraped")
axes[1, 1].set_ylabel("Processing time (ms)")
axes[1, 1].set_title("Processing time")
axes[1, 1].grid()


# --------------------------------------------------
# Figure formatting
# --------------------------------------------------

fig.suptitle(
    "Database population over scrape duration",
    fontsize=14
)

fig.tight_layout()

plt.show()