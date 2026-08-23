import json
import matplotlib.pyplot as plt
from datetime import timedelta
from pathlib import Path

import numpy as np

def r2_score(y_true, y_pred):
    ss_res = np.sum((y_true - y_pred) ** 2)
    ss_tot = np.sum((y_true - np.mean(y_true)) ** 2)

    return 1 - ss_res / ss_tot if ss_tot != 0 else 0.0

def remove_outliers(x, y):
    y = np.array(y)

    q1 = np.percentile(y, 25)
    q3 = np.percentile(y, 75)

    iqr = q3 - q1

    lower = q1 - 1.5 * iqr
    upper = q3 + 1.5 * iqr

    mask = (y >= lower) & (y <= upper)

    return np.array(x)[mask], y[mask]

# --------------------------------------------------
# Load results
# --------------------------------------------------

results = []

file_path = Path(__file__).parent / "benchmark_results.jsonl"

with open(file_path) as f:
    for line in f:
        results.append(json.loads(line))

# --------------------------------------------------
# Extract profile size and processing time
# --------------------------------------------------

profile_games = np.array([
    r["Result"]["ProfileGames"]
    for r in results
])

processing_ms = np.array([
    timedelta(
        hours=int(r["Result"]["ProcessingTime"][0:2]),
        minutes=int(r["Result"]["ProcessingTime"][3:5]),
        seconds=float(r["Result"]["ProcessingTime"][6:])
    ).total_seconds() * 1000
    for r in results
])

# --------------------------------------------------
# Processing time model
# --------------------------------------------------

x, y = remove_outliers(profile_games, processing_ms)

# --------------------------------------------------
# Linear regression
# --------------------------------------------------

m, b = np.polyfit(x, y, 1)

y_pred = m * x + b

r2 = r2_score(y, y_pred)

residuals = y - y_pred

mae = np.mean(np.abs(residuals))
rmse = np.sqrt(np.mean(residuals ** 2))

# --------------------------------------------------
# Print model statistics
# --------------------------------------------------

print(
    f"Processing time model: "
    f"T(g) = {m:.4f}g + {b:.2f} ms"
)

print(f"R²: {r2:.4f}")
print(f"MAE: {mae:.2f} ms")
print(f"RMSE: {rmse:.2f} ms")

# --------------------------------------------------
# Scatter graph
# --------------------------------------------------

fig, ax = plt.subplots(figsize=(8, 6))

ax.scatter(x,y,s=2,alpha=0.5)

# --------------------------------------------------
# Regression line
# --------------------------------------------------

line_x = np.array([min(x),max(x)])

ax.plot(
    line_x,
    m * line_x + b,
    color="red",
    linewidth=2,
    label=(
        f"T(g) = {m:.4f}g + {b:.2f}\n"
        f"R² = {r2:.4f}"
    )
)

# --------------------------------------------------
# Labels
# --------------------------------------------------

ax.set_xlabel("Games in profile")
ax.set_ylabel("Processing time (ms)")
ax.set_title("Processing time vs profile size")
ax.grid()
ax.legend()
fig.suptitle("Processing time benchmark",fontsize=14)
fig.tight_layout()
plt.show()