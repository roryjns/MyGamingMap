import { Box, Flex, Heading, Text } from "@chakra-ui/react"
import { GameCard } from "./ui/GameCard"
import { AnimatedProgressCircle } from "./ui/AnimatedStatCircle"
import { AnimatedStamp } from "./ui/AnimatedStamp"
import { useEffect, useRef, useState } from "react"

const CARD_ANIMATION_DURATION = 200 // How many ms for a card to animate it
const CARD_STAGGER = 10 // How many ms after the previous card to wait

type TierListProps = {
    ratingTiers: any[]
    averageReviewRating: number
    averageReviewTier: string
}

type TierRowProps = {
    tier: any
    color: string
}

function TierRow({
    tier,
    color,
}: TierRowProps) {
    const rowRef = useRef<HTMLDivElement>(null)
    const [isVisible, setIsVisible] = useState(false)

    useEffect(() => {
        const element = rowRef.current
        if (!element) return

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting) {
                    setIsVisible(true)
                    observer.disconnect()
                }
            },
            {
                threshold: 0.1,
            }
        )

        observer.observe(element)

        return () => observer.disconnect()
    }, [])

    return (
        <Flex ref={rowRef} width="100%">
            <Flex minWidth="65px" alignItems="center" justifyContent="center" bg={color}>
                <Text fontWeight="bold" fontSize="2xl" color="white">{tier.name}</Text>
            </Flex>

            <Flex flex="1" flexWrap="wrap">
                {tier.games.map((game: any, index: number) => {
                    const delay = index * CARD_STAGGER

                    return (
                        <Box
                            key={game.playerGame?.titleId ?? index}
                            opacity={0}
                            animation={
                                isVisible
                                    ? `tier-card-in ${CARD_ANIMATION_DURATION}ms ease forwards`
                                    : undefined
                            }
                            animationDelay={`${delay}ms`}
                        >
                            <GameCard game={game} width="81px" borderRadius="0" borderWidth="0" showReviewRating />
                        </Box>
                    )
                })}
            </Flex>
        </Flex>
    )
}

export function TierList({ ratingTiers, averageReviewRating, averageReviewTier }: TierListProps) {
    const tierColors: Record<string, string> = {
        S: "purple.500",
        A: "blue.500",
        B: "green.500",
        C: "yellow.500",
        D: "orange.500",
        E: "red.500",
        F: "red.700",
    }

    return (
        <Box width="100%">
            <Box mb="12" textAlign="center">
                <Heading className="page-title">Tier List</Heading>
                <Text className="page-subtitle">Your games, ranked.</Text>
            </Box>

            <Box
                overflow="hidden"
                borderRadius="xl"
                borderWidth="1px"
            >
                {ratingTiers.map((tier) => (
                    <TierRow
                        key={tier.name}
                        tier={tier}
                        color={tierColors[tier.name] ?? "gray.500"}
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
                <AnimatedStamp innerLabel={averageReviewTier} outerLabel="TIER" color={tierColors[averageReviewTier.replace(/[+-]$/, "")]} delay={2500} />
            </Flex>
        </Box>
    )
}