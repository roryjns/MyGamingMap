import { AbsoluteCenter, ProgressCircle, Text, VStack, } from "@chakra-ui/react"
import { useEffect, useRef, useState } from "react"

type AnimatedProgressCircleProps = {
    value: number
    max: number
    label?: string
    decimals?: number
    size?: string
    isPercentage?: boolean
}

export function AnimatedProgressCircle({
    value,
    max,
    label,
    decimals = 0,
    size = "100px",
    isPercentage = false,
}: AnimatedProgressCircleProps) {
    const [animatedValue, setAnimatedValue] = useState(0)
    const [isVisible, setIsVisible] = useState(false)
    const hasAnimated = useRef(false)
    const circleRef = useRef<HTMLDivElement>(null)

    useEffect(() => {
        const element = circleRef.current

        if (!element) return

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting && !hasAnimated.current) {
                    hasAnimated.current = true
                    setIsVisible(true)
                    observer.disconnect()
                }
            },
            {
                threshold: 0.25,
            }
        )

        observer.observe(element)

        return () => observer.disconnect()
    }, [])

    useEffect(() => {
        if (!isVisible) return

        const duration = 2000
        const start = performance.now()

        const animate = (time: number) => {
            const progress = Math.min(
                (time - start) / duration,
                1
            )

            const eased = 1 - Math.pow(1 - progress, 3)
            const currentValue = value * eased

            setAnimatedValue(currentValue)

            if (progress < 1) {
                requestAnimationFrame(animate)
            }
        }

        requestAnimationFrame(animate)
    }, [isVisible, value])

    const percentage = (animatedValue / max) * 100

    return (
        <VStack gap="4" ref={circleRef}>
            <ProgressCircle.Root size="xl" value={percentage}>
                <ProgressCircle.Circle
                    css={{
                        "--size": size,
                        "--thickness": "5px"
                    }}
                >
                    <ProgressCircle.Track />
                    <ProgressCircle.Range strokeLinecap="round" transition="none" stroke="blue" />
                </ProgressCircle.Circle>
                <AbsoluteCenter>
                    <ProgressCircle.ValueText fontSize="2xl">
                        {animatedValue.toFixed(decimals)}
                        {isPercentage && "%"}
                    </ProgressCircle.ValueText>
                </AbsoluteCenter>
            </ProgressCircle.Root>

            {label && (
                <Text className="stat-name">{label}</Text>
            )}
        </VStack>
    )
}