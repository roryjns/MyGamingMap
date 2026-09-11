import { Box, Flex, Heading, Text } from "@chakra-ui/react"
import { GameCard } from "./ui/GameCard"
import { AnimatedProgressCircle } from "./ui/AnimatedStatCircle"
import { AnimatedStamp } from "./ui/AnimatedStamp"

const CARD_ANIMATION_DURATION = 100 // How many ms for a card to animate it
const CARD_STAGGER = 8 // How many ms after the previous card to wait

type TierListProps = {
    ratingTiers: any[]
    averageReviewRating: number
}

type TierRowProps = {
    tier: any
    color: string
    animationStartDelay: number
}

function TierRow({
    tier,
    color,
    animationStartDelay,
}: TierRowProps) {
    return (
        <Flex width="100%">
            <Flex minWidth="65px" alignItems="center" justifyContent="center" bg={color}>
                <Text fontWeight="bold" fontSize="2xl" color="white">{tier.name}</Text>
            </Flex>

            <Flex flex="1" flexWrap="wrap">
                {tier.games.map((game: any, index: number) => {
                    const delay = animationStartDelay + index * CARD_STAGGER

                    return (
                        <Box
                            key={game.playerGame?.titleId ?? index}
                            opacity={0}
                            transform="translateY(12px) scale(0.95)"
                            animation={`tier-card-in ${CARD_ANIMATION_DURATION}ms ease forwards`}
                            animationDelay={`${delay}ms`}
                        >
                            <GameCard
                                game={game}
                                width="81px"
                                height="108px"
                                borderRadius="0"
                                borderWidth="0"
                                showOverlay={false}
                                showName={false}
                                showDevelopers={false}
                                showPlayHours={false}
                                showSessions={false}
                                showDateRange={false}
                            />
                        </Box>
                    )
                })}
            </Flex>
        </Flex>
    )
}

export function TierList({ ratingTiers, averageReviewRating }: TierListProps) {
    const tierColors: Record<string, string> = {
        S: "purple.500",
        A: "blue.500",
        B: "green.500",
        C: "yellow.500",
        D: "orange.500",
        E: "red.500",
        F: "red.700",
    }

    let totalDelay = 0

    const tierDelays = ratingTiers.map((tier) => {
        const startDelay = totalDelay
        const cardCount = tier.games.length

        const tierDuration =
            cardCount > 0
                ? CARD_ANIMATION_DURATION +
                (cardCount - 1) * CARD_STAGGER
                : 0

        totalDelay += tierDuration

        return startDelay
    })

    return (
        <Box width="100%">
            <Box mb="12" textAlign="center">
                <Heading
                    fontSize={{ base: "3xl", md: "4xl", lg: "5xl" }}
                    fontWeight="900"
                    lineHeight="0.9"
                >
                    Tier List
                </Heading>
                <Text mt="3" fontSize="lg" color="fg.muted">Your games, ranked.</Text>
            </Box>

            <Box
                overflow="hidden"
                borderRadius="xl"
                borderWidth="1px"
            >
                {ratingTiers.map((tier, index) => (
                    <TierRow
                        key={tier.name}
                        tier={tier}
                        color={tierColors[tier.name] ?? "gray.500"}
                        animationStartDelay={tierDelays[index]}
                    />
                ))}
            </Box>

            <Flex flexWrap="wrap" mt="12" justifyContent="center" gap="10">
                <AnimatedProgressCircle value={averageReviewRating}
                    max={100}
                    label="AVERAGE RATING"
                    decimals={1}
                    isPercentage
                />
                <AnimatedStamp innerLabel={"S"} outerLabel="TIER" color={tierColors["S"]} delay={2500}/>
            </Flex>

        </Box>
    )
}