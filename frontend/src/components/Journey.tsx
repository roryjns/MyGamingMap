import { Text, Box, Heading } from "@chakra-ui/react"

type JourneyProps = {

}

export function Journey({ }: JourneyProps) {
    return (
        <Box width="100%">
            <Box mb="12" textAlign="center">
                <Heading className="page-title">Journey</Heading>
                <Text className="page-subtitle">Your gaming history.</Text>
            </Box>
        </Box>
    )
}