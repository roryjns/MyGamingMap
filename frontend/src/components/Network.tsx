import { Text, Box, Heading } from "@chakra-ui/react"

type NetworkProps = {

}

export function Network({ }: NetworkProps) {
    return (
        <Box>
            <Box mb="12" textAlign="center">
                <Heading className="page-title">Network</Heading>
                <Text className="page-subtitle">Your gaming history.</Text>
            </Box>
        </Box>
    )
}