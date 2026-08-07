import json
import matplotlib.pyplot as plt
from datetime import timedelta
from pathlib import Path
import numpy as np

results = []

file_path = Path(__file__).parent / "igdb_scrape_results.jsonl"

def remove_outliers(x, y):
    y = np.array(y)

    q1 = np.percentile(y, 25)
    q3 = np.percentile(y, 75)

    iqr = q3 - q1

    lower = q1 - 1.5 * iqr
    upper = q3 + 1.5 * iqr

    mask = (y >= lower) & (y <= upper)

    return np.array(x)[mask], y[mask]

with open(file_path) as f:
    next(f)  # ignore first user
    for line in f:
        results.append(json.loads(line))

profile_numbers = range(1, len(results) + 1)

profile_games = [
    r["Result"]["ProfileGames"]
    for r in results
]

profile_games_after_1000 = profile_games[1000:] # Used for processing time graph

database_hit_rate = [
    r["Result"]["DatabaseHits"] / r["Result"]["ProfileGames"] * 100
    for r in results
]

name_lookup_rate = [
    r["Result"]["NameLookups"] / r["Result"]["ProfileGames"] * 100
    for r in results
]

unmatched_rate = [
    r["Result"]["UnmatchedGames"] / r["Result"]["ProfileGames"] * 100
    for r in results
]

processing_seconds = [
    timedelta(
        hours=int(r["Result"]["ProcessingTime"][0:2]),
        minutes=int(r["Result"]["ProcessingTime"][3:5]),
        seconds=float(r["Result"]["ProcessingTime"][6:])
    ).total_seconds()
    for r in results[1000:]
]

fig, axes = plt.subplots(2, 2, figsize=(9, 6))

x, y = remove_outliers(profile_games, database_hit_rate)
axes[0,0].scatter(x, y, s=2)
m, b = np.polyfit(x, y, 1)
line_x = np.array([min(x), max(x)])
axes[0,0].plot(line_x, m * line_x + b, color="red")
axes[0,0].set_xlabel("Games in profile")
axes[0,0].set_ylabel("Database hit rate (%)")
axes[0,0].grid()

axes[0,1].scatter(profile_games, name_lookup_rate, s=2)
m, b = np.polyfit(profile_games, name_lookup_rate, 1)
line_x = np.array([min(profile_games), max(profile_games)])
axes[0,1].plot(line_x, m * line_x + b, color="red")
axes[0,1].set_xlabel("Games in profile")
axes[0,1].set_ylabel("Games requiring name lookup (%)")
axes[0,1].grid()

x, y = remove_outliers(profile_games, unmatched_rate)
axes[1,0].scatter(x, y, s=2)
m, b = np.polyfit(x, y, 1)
line_x = np.array([min(x), max(x)])
axes[1,0].plot(line_x, m * line_x + b, color="red")
axes[1,0].set_xlabel("Games in profile")
axes[1,0].set_ylabel("Unmatched games (%)")
axes[1,0].grid()

x, y = remove_outliers(profile_games_after_1000, processing_seconds)
axes[1,1].scatter(x, y, s=2)
m, b = np.polyfit(x, y, 1)
line_x = np.array([min(x), max(x)])
axes[1,1].plot(line_x, m * line_x + b, color="red")
axes[1,1].set_xlabel("Games in profile")
axes[1,1].set_ylabel("Processing time (seconds)")
plt.grid()

plt.tight_layout
plt.show()