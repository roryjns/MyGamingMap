import { useCallback, useState, useEffect } from "react"
import { Text, Link, IconButton, Box, Flex, Heading, Tabs, Menu, Portal } from "@chakra-ui/react"
import { LuMenu } from "react-icons/lu"
import { ColorModeButton, useColorMode } from './components/ui/color-mode'
import { BrowserRouter, Route, Routes, Link as RouterLink, useLocation, useNavigate} from "react-router-dom"

import Home from "./pages/Home"
import FAQs from "./pages/FAQs"
import PrivacyPolicy from "./pages/Privacy"
import Results from "./pages/Results"

import './App.css'

export default function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  )
}

function AppContent() {
  const [username, setUsername] = useState("")
  const [isSearching, setIsSearching] = useState(false)
  const [searchError, setSearchError] = useState('')
  const [analytics, setAnalytics] = useState<any>(null)
  const [activeTab, setActiveTab] = useState("")
  const { toggleColorMode } = useColorMode()
  const location = useLocation()
  const navigate = useNavigate()

  const handleUsernameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const sanitised = e.target.value
      .replace(/[^a-zA-Z0-9_-]/g, '')
      .slice(0, 16)

    setUsername(sanitised)
  }

  const handleSubmitSearch = (e: React.FormEvent) => {
    e.preventDefault()
    if (!username) return
    setSearchError("")
    navigate(`/${encodeURIComponent(username)}`)
  }

  const handleSearch = useCallback(async (searchUsername: string) => {
    if (!searchUsername) return

    setSearchError("")
    setIsSearching(true)

    try {
      const response = await fetch(
        `http://localhost:5199/api/${encodeURIComponent(searchUsername)}/analytics`
      )

      if (!response.ok) {
        let message = `Request failed (${response.status})`

        try {
          const errorData = await response.json()
          if (errorData.message) {
            message = errorData.message
          }
        } catch {
          if (response.status === 500) {
            message = 'The server encountered an unknown error'
          }
        }

        setSearchError(message)
        setUsername("")
        return
      }

      const data = await response.json()
      setAnalytics(data)
      setActiveTab("network")
    } catch (error) {
      setSearchError('Unable to connect to the server')
      setUsername("")
    } finally {
      setIsSearching(false)
    }
  }, [])

  const handleTabChange = (tab: string) => {
    setActiveTab(tab)
    location.pathname = "/:username"
    window.scrollTo(0, 0)
  }

  useEffect(() => {
    if (location.pathname === "/") {
      setAnalytics(null)
      setActiveTab("")
      setUsername("")
    } else if (
      location.pathname === "/faqs" ||
      location.pathname === "/privacy"
    ) {
      setActiveTab("")
    }
    window.scrollTo(0, 0)
  }, [location.pathname])

  return (
    <Box minH="100vh" display="flex" flexDirection="column">
      <Flex as="header" bg="bg" position="sticky" top="0" left="0" right="0" zIndex="1000" align="center" justify="space-between" px="6" py="4" borderBottomWidth="1px">
        <RouterLink to="/">
          <Box>
            <Heading size="md" fontFamily="'logo-font'" fontSize="1.4rem" letterSpacing="-0.02em">MyGamingMap</Heading>
            <svg className="logo-route" margin-top="5px" viewBox="0 0 300 35" aria-hidden="true">
              <path d="M 8 28 Q 150 2 292 28" />
            </svg>
          </Box>
        </RouterLink>

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
              "--tabs-indicator-shadow": "shadows.xs",
              "--tabs-trigger-radius": "radii.full",
            }}
          >
            <Tabs.List>
              <Tabs.Trigger _selected={{ _dark: { bg: "brand.dark_blue", }, }} fontSize="md" value="map">Map</Tabs.Trigger>
              <Tabs.Trigger _selected={{ _dark: { bg: "brand.dark_blue", }, }} fontSize="md" value="network">Network</Tabs.Trigger>
              <Tabs.Trigger _selected={{ _dark: { bg: "brand.dark_blue", }, }} fontSize="md" value="tier-list">Tier List</Tabs.Trigger>
              <Tabs.Trigger _selected={{ _dark: { bg: "brand.dark_blue", }, }} fontSize="md" value="overview">Overview</Tabs.Trigger>
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
                    <Menu.Item value="map" onClick={() => handleTabChange("map")} _highlighted={{ _dark: { bg: "brand.dark_blue" } }}>Map</Menu.Item>
                    <Menu.Item value="network" onClick={() => handleTabChange("network")} _highlighted={{ _dark: { bg: "brand.dark_blue" } }}>Network</Menu.Item>
                    <Menu.Item value="tier-list" onClick={() => handleTabChange("tier-list")} _highlighted={{ _dark: { bg: "brand.dark_blue" } }}>Tier List</Menu.Item>
                    <Menu.Item value="overview" onClick={() => handleTabChange("overview")} _highlighted={{ _dark: { bg: "brand.dark_blue" } }}>Overview</Menu.Item>
                    <Menu.Separator />
                    <Menu.Item value="color-mode" _highlighted={{ _dark: { bg: "brand.dark_blue" } }} onClick={toggleColorMode}>Toggle Colour Mode</Menu.Item>
                  </Menu.Content>
                </Menu.Positioner>
              </Portal>
            </Menu.Root>
          )}
        </Box>
      </Flex>

      <Box as="main" flex="1" display="flex" width="100%" mx="auto" flexDirection="column" p="10">
        <Routes>
          <Route path="/" element={
            <Home username={username} searchError={searchError} handleUsernameChange={handleUsernameChange} handleSubmit={handleSubmitSearch} />
          }
          />
          <Route path="/:username" element={
            <Results
              analytics={analytics}
              isSearching={isSearching}
              searchError={searchError}
              activeTab={activeTab}
              handleSearch={handleSearch} />
          } />
          <Route path="/faqs" element={<FAQs />} />
          <Route path="/privacy" element={<PrivacyPolicy />} />
        </Routes>
      </Box>

      <Flex as="footer" direction="column" align="center" px="6" py="5" borderTopWidth="1px">
        <Flex width="100%" direction={{ base: "column", md: "row" }} align="center" justify="space-between" gap="2">
          <Text color="fg.muted" textAlign="center">
            © {new Date().getFullYear()} MyGamingMap. MyGamingMap is not affiliated with Sony or PlayStation in any way.
          </Text>
          <Flex gap="4">
            <Link asChild>
              <RouterLink to="/faqs">FAQs</RouterLink>
            </Link>
            <Link asChild>
              <RouterLink to="/privacy">Privacy</RouterLink>
            </Link>
            <Link href="mailto:contact@mygamingmap.com">Contact</Link>
          </Flex>
        </Flex>
      </Flex>
    </Box>
  )
}