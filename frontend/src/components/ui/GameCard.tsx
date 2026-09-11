import { Box, Heading, Image, Text } from "@chakra-ui/react"
import ps4Logo from "../../assets/PS4 Logo.png";
import ps5Logo from "../../assets/PS5 Logo.png";

type GameCardProps = {
  game: any

  width?: string
  height?: string
  borderRadius?: string
  borderWidth?: string

  showOverlay?: boolean
  showName?: boolean
  showDevelopers?: boolean
  showPlayHours?: boolean
  showSessions?: boolean
  showDateRange?: boolean
}

const getIgdbImageUrl = (imageId: string) =>
  `https://images.igdb.com/igdb/image/upload/t_cover_med/${imageId}.webp`

export function GameCard({
  game,
  width = "135px",
  height = "180px",
  borderRadius = "1px",
  borderWidth = "1px solid var(--chakra-colors-border)",
  showOverlay = true,
  showName = true,
  showDevelopers = true,
  showPlayHours = true,
  showSessions = true,
  showDateRange = true,
}: GameCardProps) {
  const playerGame = game.playerGame

  return (
    <Box className="game-card" width={width} height={height} borderRadius={borderRadius} >
      <Box className="game-card-inner">
        <Box className="game-card-front" borderRadius={borderRadius} borderWidth={borderWidth}>
          <Image
            src={getIgdbImageUrl(playerGame.imageUrl)}
            alt={playerGame.name}
            width="100%"
            height="auto"
            objectFit="cover"
            loading="lazy"
            decoding="async"
          />
          {showOverlay && (
            <Box className="game-card-overlay">
              <Text
                className="game-card-number"
                fontSize="xs"
                bg="bg.muted"
                color="fg"
                borderRadius="6px"
                px="1"
                py="0"
                display="inline-block"
              >
                #1
              </Text>
              <Box className="platform-logos">
                {playerGame.platform === "PS4" && (
                  <Image src={ps4Logo} alt="PS4" className="platform-logo" />
                )}
                {playerGame.platform === "PS5" && (
                  <Image src={ps5Logo} alt="PS5" className="platform-logo" />
                )}
                <Image src={ps4Logo} alt="PS4" className="platform-logo" />
              </Box>
            </Box>
          )}
        </Box>

        <Box className="game-card-back" borderRadius={borderRadius} borderWidth="1px solid var(--chakra-colors-border)" >
          <Box p="3" height="100%" display="flex" flexDirection="column">
            {showName ?? (
              <Heading size="sm" lineClamp="3">{playerGame.name}</Heading>
            )}

            {showDevelopers && (
              <Text fontSize="xs" lineClamp="2">{game.igdbGame?.developers?.join(", ") ?? ""}</Text>
            )}

            <Box mt="auto">
              {showPlayHours && (
                <Text fontSize="xs">
                  {playerGame.playHours != null
                    ? `${Math.round(playerGame.playHours).toLocaleString()} hrs`
                    : ""}
                </Text>
              )}

              {showSessions && (
                <Text fontSize="xs">
                  {playerGame.playCount != null
                    ? `${playerGame.playCount.toLocaleString()} sessions`
                    : ""}
                </Text>
              )}

              {showDateRange &&
                playerGame.firstPlayed != null &&
                playerGame.lastPlayed != null && (
                  <Text fontSize="xs">
                    {new Date(playerGame.firstPlayed).toLocaleDateString("en-GB", {
                      day: "numeric",
                      month: "numeric",
                      year: "2-digit",
                    })}
                    {" - "}
                    {new Date(playerGame.lastPlayed).toLocaleDateString("en-GB", {
                      day: "numeric",
                      month: "numeric",
                      year: "2-digit",
                    })}
                  </Text>
                )}
            </Box>
          </Box>
        </Box>
      </Box>
    </Box>
  )
}