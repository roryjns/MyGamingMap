import { Field, Group, Input, Flex, IconButton , Text} from "@chakra-ui/react"
import { LuSearch } from "react-icons/lu"
import { InfoTip } from "../components/ui/toggle-tip"

export default function Home({ username, searchError, handleUsernameChange, handleSubmit }: {
    username: string, searchError: string, handleUsernameChange: (e: React.ChangeEvent<HTMLInputElement>) => void
    handleSubmit: (e: React.SubmitEvent) => void
}) {
    return (
        <Flex flex="1" align="center" justify="center">
            <form onSubmit={handleSubmit} style={{ width: "100%" }}>
                <Field.Root required w="100%" maxW="350px" mx="auto" invalid={!!searchError}>
                    <Group attached w="100%">
                        <Input zIndex={1} fontSize="md" placeholder="Enter a PSN username" value={username} onChange={handleUsernameChange} />
                        <IconButton variant="outline" aria-label="Search" type="submit">
                            <LuSearch />
                        </IconButton>
                    </Group>

                    <Field.HelperText textAlign="center" fontSize="sm">
                        Account must have gaming history set to public.
                        <InfoTip>
                            <Text fontSize="sm">
                                If you are the account owner: <br />
                                1. Go to Settings on your PlayStation. <br />
                                2. Select Users and Accounts → Privacy. <br />
                                3. Select "View and Customize Your Privacy Settings". <br />
                                4. Set "Who can see your gaming history" to "Anyone".
                            </Text>

                        </InfoTip>
                    </Field.HelperText>

                    <Field.ErrorText textAlign="center" fontSize="sm">{searchError}</Field.ErrorText>
                </Field.Root>
            </form>
        </Flex>
    )
}