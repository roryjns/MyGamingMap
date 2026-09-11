import { Field, Group, Input, Text, Link } from "@chakra-ui/react"
import { IconButton } from "@chakra-ui/react"
import { LuSearch } from "react-icons/lu"
import { Box, Flex, Heading } from '@chakra-ui/react'
import { ColorModeButton } from './components/ui/color-mode'
import { InfoTip } from './components/ui/toggle-tip'
import { useState } from "react"
import { Spinner } from "@chakra-ui/react"
import { lazy, Suspense } from "react"
import './App.css'

function App() {
  const [username, setUsername] = useState('')
  const [isSearching, setIsSearching] = useState(false)
  const [searchError, setSearchError] = useState('')
  const [map, setMap] = useState<any>(null)

  const TierList = lazy(() =>
    import("./components/TierList").then(({ TierList }) => ({ default: TierList }))
  )

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
        setSearchError('PSN user not found')
        return
      }

      const data = await response.json()
      setMap(data)
    } catch (error) {
      setSearchError('Unable to connect to the server')
    } finally {
      setIsSearching(false)
    }
  }

  return (
    <Box minH="100vh" display="flex" flexDirection="column">
      <Flex as="header" bg="bg" position="sticky" top="0" left="0" right="0" zIndex="1000" align="center" justify="space-between" px="6" py="4" borderBottomWidth="1px">
        <Heading size="md">MyGamingMap</Heading>
        <ColorModeButton />
      </Flex>

      <Box as="main" flex="1" display="flex" flexDirection="column" justifyContent="center" p="12">
        {!map && !isSearching && (
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
            <Text color="fg.muted" textAlign="center" className="shimmer-text">Generating map...</Text>
          </Flex>
        )}

        {map && (
          <Suspense>
            <TierList
              ratingTiers={map.igdb.reviewRatingAnalytics.ratingTiers}
              averageReviewRating={map.igdb.reviewRatingAnalytics.averageReviewRating}
            />
          </Suspense>
        )}
      </Box>

      <Flex as="footer" direction="column" align="center" gap="3" px="6" pt="4" pb="4" borderTopWidth="1px">
        <Flex width="100%" direction={{ base: "column", md: "row" }} align="center" justify="space-between" gap="3">
          <Text color="fg.muted">© 2026 MyGamingMap. All Rights Reserved.</Text>
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