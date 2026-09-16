import { Text, Box, Heading } from "@chakra-ui/react"

type MapProps = {

}

export function Map({ }: MapProps) {
    return (
        <Box width="100%">
            <Box mb="12" textAlign="center">
                <Heading className="page-title">Map</Heading>
                <Text className="page-subtitle">Your games, mapped.</Text>
            </Box>
        </Box>
    )
}