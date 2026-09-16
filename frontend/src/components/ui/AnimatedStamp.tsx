import { Box, Text, VStack } from "@chakra-ui/react"
import { useEffect, useRef, useState } from "react"

type AnimatedStampProps = {
    innerLabel: string
    outerLabel?: string
    color?: string
    delay?: number
}

export function AnimatedStamp({
    innerLabel,
    outerLabel,
    color = "blue.500",
    delay = 0,
}: AnimatedStampProps) {
    const [isVisible, setIsVisible] = useState(false)
    const hasAnimated = useRef(false)
    const stampRef = useRef<HTMLDivElement>(null)

    useEffect(() => {
        const element = stampRef.current
        if (!element) return

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting && !hasAnimated.current) {
                    hasAnimated.current = true
                    setIsVisible(true)
                    observer.disconnect()
                }
            },
            { threshold: 0.25 }
        )

        observer.observe(element)

        return () => observer.disconnect()
    }, [])

    return (
        <VStack gap="4">
            <Box
                ref={stampRef}
                opacity={isVisible ? 0 : 0}
                transform="scale(1.4) rotate(-12deg)"
                animation={
                    isVisible
                        ? "stamp-in 400ms cubic-bezier(0.34, 1.56, 0.64, 1) forwards"
                        : undefined
                } 
                animationDelay={isVisible ? `${delay}ms` : undefined}
                border="5px solid"
                borderColor={color}
                borderRadius="full"
                width="100px"
                height="100px"
                display="flex"
                alignItems="center"
                justifyContent="center"
                position="relative"
            >
                <Text
                    fontSize="4xl"
                    fontWeight="900"
                    color={color}
                    lineHeight="1"
                    textAlign="center"
                >
                    {innerLabel}
                </Text>
            </Box>

            {outerLabel && (
                <Text className="stat-name">{outerLabel}</Text>
            )}
        </VStack>
    )
}