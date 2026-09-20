import { lazy, Suspense, useEffect, useRef } from "react"
import { useParams, useNavigate } from "react-router-dom"
import { Flex, Spinner, Box, Text } from "@chakra-ui/react"

const Map = lazy(() => import("../components/Map").then(({ Map }) => ({ default: Map })))

const Network = lazy(() => import("../components/Network").then(({ Network }) => ({ default: Network })))

const TierList = lazy(() => import("../components/TierList").then(({ TierList }) => ({ default: TierList })))

const Overview = lazy(() => import("../components/Overview").then(({ Overview }) => ({ default: Overview })))

export default function Results({ analytics, isSearching, searchError, activeTab, handleSearch }: {
    analytics: any, isSearching: boolean, searchError: string, activeTab: string, handleSearch: (username: string) => Promise<void>
}) {
    const { username } = useParams()
    const searchedUsername = useRef<string | null>(null)
    const navigate = useNavigate()

    useEffect(() => {
        if (username && !analytics && searchedUsername.current !== username) {
            searchedUsername.current = username
            handleSearch(username)
        }
    }, [username, analytics, handleSearch])

    useEffect(() => {
        if (searchError) {
            navigate("/", { replace: true })
        }
    }, [searchError, navigate])

    if (isSearching) {
        return (
            <Box flex="1" display="flex" flexDirection="column" justifyContent="center">
                <Flex justify="center" align="center" flexDirection="column" gap="4">
                    <Spinner size="lg" animationDuration="0.5s" />
                    <Text color="fg.muted" textAlign="center" className="shimmer-text" fontSize="md">Generating analytics...</Text>
                </Flex>
            </Box>
        )
    }

    return (
        <Box flex="1" display="flex" flexDirection="column" justifyContent="center">
            <Suspense
                fallback={
                    <Flex justify="center" align="center">
                        <Spinner size="lg" animationDuration="0.5s" />
                    </Flex>
                }
            >
                {activeTab === "map" && <Map />}

                {activeTab === "network" && <Network />}

                {activeTab === "overview" && (
                    <Overview
                        activityAnalytics={analytics.psn.activityAnalytics}
                        trophyAnalytics={analytics.psn.trophyAnalytics}
                        gameModeAnalytics={analytics.igdb.gameModeAnalytics}
                        genreAnalytics={analytics.igdb.genreAnalytics}
                        themeAnalytics={analytics.igdb.themeAnalytics}
                        releaseYearAnalytics={analytics.igdb.releaseDateAnalytics.releaseYearAnalytics}
                    />
                )}

                {activeTab === "tier-list" && (
                    <TierList
                        ratingTiers={analytics.igdb.reviewRatingAnalytics.ratingTiers}
                        averageReviewRating={analytics.igdb.reviewRatingAnalytics.averageReviewRating}
                        averageReviewTier={analytics.igdb.reviewRatingAnalytics.averageReviewTier}
                    />
                )}
            </Suspense>
        </Box>
    )
}