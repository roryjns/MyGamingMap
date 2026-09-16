import { Field, Group, Input, Text, Link } from "@chakra-ui/react"
import { IconButton } from "@chakra-ui/react"
import { LuSearch, LuMenu, LuMoon } from "react-icons/lu"
import { Box, Flex, Heading, Tabs, Menu, Portal } from '@chakra-ui/react'
import { ColorModeButton, useColorMode } from './components/ui/color-mode'
import { InfoTip } from './components/ui/toggle-tip'
import { useState } from "react"
import { Spinner } from "@chakra-ui/react"
import { lazy, Suspense } from "react"
import './App.css'

const Map = lazy(() =>
  import("./components/Map").then(({ Map }) => ({ default: Map }))
)

const Journey = lazy(() =>
  import("./components/Journey").then(({ Journey }) => ({ default: Journey }))
)

const TierList = lazy(() =>
  import("./components/TierList").then(({ TierList }) => ({ default: TierList }))
)

const Overview = lazy(() =>
  import("./components/Overview").then(({ Overview }) => ({ default: Overview }))
)

function App() {
  const [username, setUsername] = useState('')
  const [isSearching, setIsSearching] = useState(false)
  const [searchError, setSearchError] = useState('')
  const [analytics, setAnalytics] = useState<any>(null)
  const [activeTab, setActiveTab] = useState("map")
  const { toggleColorMode } = useColorMode()

  const handleUsernameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const sanitised = e.target.value
      .replace(/[^a-zA-Z0-9_-]/g, '')
      .slice(0, 16)

    setUsername(sanitised)
  }

  const handleSearch = async () => {
    if (!username) {
      return
    }

    setSearchError('')
    setIsSearching(true)

    try {
      const response = await fetch(
        `http://localhost:5199/api/${encodeURIComponent(username)}/map`
      )

      if (!response.ok) {
        let message = 'Something went wrong'

        try {
          const errorData = await response.json()

          if (errorData.message) {
            message = errorData.message
          }
        } catch {
          // Response wasn't JSON, so use the status below
        }

        switch (response.status) {
          case 404:
            message = message || 'PSN user not found'
            break

          case 409:
            message = message || 'Analytics are already being generated for this user'
            break

          case 429:
            message = message || 'Please wait before trying again'
            break

          case 500:
            message = 'The server encountered an error while generating the map'
            break

          default:
            message = `Request failed (${response.status})`
        }

        setSearchError(message)
        setUsername('')
        return
      }

      const data = await response.json()
      setAnalytics(data)
    } catch (error) {
      setSearchError('Unable to connect to the server')
      setUsername('')
    } finally {
      setIsSearching(false)
    }
  }

  const handleTabChange = (tab: string) => {
    setActiveTab(tab)
    window.scrollTo(0, 0)
  }

  return (
    <Box minH="100vh" display="flex" flexDirection="column">
      <Flex as="header" bg="bg" position="sticky" top="0" left="0" right="0" zIndex="1000" align="center" justify="space-between" px="6" py="4" borderBottomWidth="1px">
        <Heading size="md">MyGamingMap</Heading>

        {/* Desktop navigation */}
        {analytics && (
          <Tabs.Root
            value={activeTab}
            onValueChange={(details) => handleTabChange(details.value)}
            variant="plain"
            position="absolute"
            left="50%"
            transform="translateX(-50%)"
            activationMode="manual"
            display={{ base: "none", md: "block" }}
            css={{
              "--tabs-indicator-bg": "colors.gray.subtle",
              "--tabs-indicator-shadow": "shadows.xs",
              "--tabs-trigger-radius": "radii.full",
            }}
          >
            <Tabs.List>
              <Tabs.Trigger fontSize="md" value="map">Map</Tabs.Trigger>
              <Tabs.Trigger fontSize="md" value="journey">Journey</Tabs.Trigger>
              <Tabs.Trigger fontSize="md" value="tier-list">Tier List</Tabs.Trigger>
              <Tabs.Trigger fontSize="md" value="overview">Overview</Tabs.Trigger>
              <Tabs.Indicator />
            </Tabs.List>
          </Tabs.Root>
        )}

        <Box ml="auto" display={{ base: "none", md: "block" }}>
          <ColorModeButton />
        </Box>

        {/* Mobile menu */}
        <Box ml="auto" display={{ base: "block", md: "none" }}>
          {!analytics ? (
            // Before analytics: keep colour mode button visible
            <ColorModeButton />
          ) : (
            // After analytics: collapse everything into hamburger menu
            <Menu.Root>
              <Menu.Trigger asChild>
                <IconButton variant="ghost" aria-label="Open menu">
                  <LuMenu />
                </IconButton>
              </Menu.Trigger>

              <Portal>
                <Menu.Positioner>
                  <Menu.Content>
                    {analytics && (
                      <>
                        <Menu.Item value="map" onClick={() => handleTabChange("map")}>Map</Menu.Item>
                        <Menu.Item value="journey" onClick={() => handleTabChange("journey")}>Journey</Menu.Item>
                        <Menu.Item value="tier-list" onClick={() => handleTabChange("tier-list")}>Tier List</Menu.Item>
                        <Menu.Item value="overview" onClick={() => handleTabChange("overview")}>Overview</Menu.Item>
                        <Menu.Separator />
                      </>
                    )}
                    <Menu.Item value="color-mode" onClick={toggleColorMode}>
                      <Flex align="center" gap="2">
                        <Text>Toggle Colour Mode</Text>
                      </Flex>
                    </Menu.Item>
                  </Menu.Content>
                </Menu.Positioner>
              </Portal>
            </Menu.Root>
          )}
        </Box>

      </Flex>

      <Box as="main" flex="1" display="flex" flexDirection="column" justifyContent="center" p="12">
        {!analytics && !isSearching && (
          <form
            onSubmit={(e) => {
              e.preventDefault()
              handleSearch()
            }}
            style={{ width: '100%' }}
          >
            <Field.Root required w="100%" maxW="350px" mx="auto" invalid={!!searchError}>
              <Group attached w="100%">
                <Input placeholder="Enter a PSN username" value={username} onChange={handleUsernameChange} />
                <IconButton variant="outline" aria-label="Search" onClick={handleSearch}>
                  <LuSearch />
                </IconButton>
              </Group>
              <Field.HelperText textAlign="center">
                Account must have gaming history set to public.
                <InfoTip>
                  If you are the account owner: <br />
                  1. Go to Settings on your PlayStation. <br />
                  2. Select Users and Accounts → Privacy. <br />
                  3. Select "View and Customize Your Privacy Settings". <br />
                  4. Set "Who can see your gaming history" to "Anyone".
                </InfoTip>
              </Field.HelperText>
              <Field.ErrorText textAlign="center">{searchError}</Field.ErrorText>
            </Field.Root>
          </form>
        )}

        {isSearching && (
          <Flex justify="center" align="center" flexDirection="column" gap="4">
            <Spinner size="lg" animationDuration="0.5s" />
            <Text color="fg.muted" textAlign="center" className="shimmer-text">Generating analytics...</Text>
          </Flex>
        )}

        {analytics && (
          <Suspense
            fallback={
              <Flex justify="center" align="center">
                <Spinner size="lg" animationDuration="0.5s" />
              </Flex>
            }
          >
            {activeTab === "map" && (
              <Map />
            )}

            {activeTab === "journey" && (
              <Journey />
            )}

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
        )}
      </Box>

      <Flex as="footer" direction="column" align="center" gap="3" px="6" py="4" borderTopWidth="1px">
        <Flex width="100%" direction={{ base: "column", md: "row" }} align="center" justify="space-between" gap="2">
          <Text color="fg.muted" textAlign="center">© 2026 MyGamingMap. All Rights Reserved.</Text>
          <Flex gap="4">
            <Link href="#">FAQs</Link>
            <Link href="#">Privacy</Link>
            <Link href="#">Contact</Link>
          </Flex>
        </Flex>
        <Text fontSize="xs" color="fg.muted" textAlign="center">
          MyGamingMap is not affiliated with Sony or PlayStation in any way.
        </Text>
      </Flex>
    </Box >
  )
}

export default App