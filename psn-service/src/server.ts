import express from "express";
import { getPlayerGames } from "./PSNservice.js";

const app = express();

app.use(express.json());

app.post("/player/:username/games", async (req, res) => {
    try {
        const username = req.params.username;
        const games = await getPlayerGames(username);
        res.json(games);
    }
    catch (error) {
        console.error(error);
        res.status(500).json({ error: "Failed to fetch PSN data" });
    }
});

app.listen(3000, () => {
    console.log("PSN service running on port 3000");
});