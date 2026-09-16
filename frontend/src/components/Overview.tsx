import { Chart, useChart } from "@chakra-ui/charts"
import { Box, Flex, Heading, Text, Select, createListCollection, Field, VisuallyHidden } from "@chakra-ui/react"
import { useState } from "react"
import { PolarAngleAxis, PolarGrid, Radar, RadarChart, Tooltip } from "recharts"
import { Bar, BarChart, XAxis, LabelList } from "recharts"
import { Pie, PieChart, Sector } from "recharts"

type Analytic = {
    name: string
    gamesPlayed: number
    hoursPlayed: number
    medianHoursPerGame: number
    medianSessionsPerGame: number
    averageCompletion: number
    platinumsEarned: number
}

type OverviewProps = {
    activityAnalytics: any
    trophyAnalytics: any
    gameModeAnalytics: Analytic[]
    genreAnalytics: Analytic[]
    themeAnalytics: Analytic[]
    releaseYearAnalytics: Analytic[]
}

export function Overview({
    activityAnalytics,
    trophyAnalytics,
    gameModeAnalytics,
    genreAnalytics,
    themeAnalytics,
    releaseYearAnalytics
}: OverviewProps) {

    const platformChart = useChart({
        data: [
            {
                name: "PS3",
                value: activityAnalytics.pS3GamesPlayed,
                color: "#000000"
            },
            {
                name: "PS Vita",
                value: activityAnalytics.psVitaGamesPlayed,
                color: "#505050"
            },
            {
                name: "PS4",
                value: activityAnalytics.pS4.gamesPlayed,
                color: "#0070CC"
            },
            {
                name: "PS5",
                value: activityAnalytics.pS5.gamesPlayed,
                color: "#FFFFFF"
            },
        ].filter(platform => platform.value > 0),
        series: [
            {
                name: "value",
                label: "Games Played",
            },
        ],
    })

    const statOptions = [
        { value: "gamesPlayed", label: "Games Played" },
        { value: "hoursPlayed", label: "Hours Played" },
        { value: "medianHoursPerGame", label: "Median Hours Per Game" },
        { value: "medianSessionsPerGame", label: "Median Sessions Per Game" },
        { value: "averageCompletion", label: "Average Trophy Completion" },
        { value: "platinumsEarned", label: "Platinum Trophies Earned" },
    ]

    const getStatLabel = (value: string) => statOptions.find(option => option.value === value)?.label ?? value

    const statCollection = createListCollection({ items: statOptions })

    const [radarStat, setRadarStat] = useState("gamesPlayed")

    const gameModeColors: Record<string, string> = {
        "Single player only": "#0070CC",
        "Multiplayer only": "#A855F7",
        "Single player + Multiplayer": "#00A878",
    }

    const gameModeChart = useChart({
        data: [...gameModeAnalytics]
            .sort((a, b) => b.name.localeCompare(a.name))
            .map(gameMode => ({
                name: gameMode.name,
                value: gameMode.gamesPlayed,
                color: gameModeColors[gameMode.name] ?? "#808080",
            })),
        series: [
            {
                name: "value",
                color: "teal.solid",
                label: "Games Played"
            }
        ]
    })

    const genreChart = useChart({
        data: [...genreAnalytics]
            .sort((a, b) => a.name.localeCompare(b.name))
            .map(genre => ({
                name: genre.name,
                value: genre[radarStat as keyof typeof genre],
            })),
        series: [
            {
                name: "value",
                color: "teal.solid",
                label: getStatLabel(radarStat),
            },
        ],
    })

    const themeChart = useChart({
        data: [...themeAnalytics]
            .sort((a, b) => a.name.localeCompare(b.name))
            .map(theme => ({
                name: theme.name,
                value: theme[radarStat as keyof typeof theme],
            })),
        series: [
            {
                name: "value",
                color: "purple.solid",
                label: getStatLabel(radarStat),
            },
        ],
    })

    const releaseYearChart = useChart({
        data: [...releaseYearAnalytics]
            .map(releaseYear => ({
                name: releaseYear.name,
                value: releaseYear["gamesPlayed"],
            })),
        series: [
            {
                name: "value",
                color: "teal.solid",
                label: "Games Played",
            },
        ],
    })

    return (
        <Box width="100%">
            <Box mb="12" textAlign="center">
                <Heading className="page-title">Overview</Heading>
                <Text className="page-subtitle">Your gaming DNA.</Text>
            </Box>

            <Flex flexWrap="wrap" justify="center" flexDirection="row">
                <Flex direction="column" width="min(150px, 100%)" align="center" gap="5">
                    <Chart.Root chart={platformChart}>
                        <PieChart responsive>
                            <Pie
                                data={platformChart.data}
                                dataKey={platformChart.key("value")}
                                nameKey="name"
                                innerRadius={30}
                                startAngle={90}
                                endAngle={-360}
                                stroke="var(--chakra-colors-border)"
                                strokeWidth={1}
                                shape={(props) => (
                                    <Sector
                                        {...props}
                                        fill={platformChart.color(props.payload!.color)}
                                        outerRadius={props.isActive ? 70 : 60} 
                                        style={{
                                            transition: "all 0.3s ease-out",
                                        }}
                                    />
                                )}
                            >
                            </Pie>
                            <Tooltip
                                content={({ active, payload }) => {
                                    if (!active || !payload?.length) return null

                                    const platform = payload[0].payload

                                    return (
                                        <Box bg="bg.panel" borderWidth="1px" borderColor="border" borderRadius="md" px="3" py="2" boxShadow="md">
                                            <Text fontSize="xs" fontWeight="medium">{platform.name}</Text>
                                            <Flex justifyContent="space-between" gap="3">
                                                <Text color="var(--chakra-colors-fg-muted)" fontSize="xs">Games Played</Text>
                                                <Text fontSize="xs" fontWeight="medium">{platform.value}</Text>
                                            </Flex>
                                        </Box>
                                    )
                                }}
                            />
                        </PieChart>
                    </Chart.Root>
                    <Text className="stat-name">PLATFORMS</Text>
                </Flex>

                <Flex direction="column" width="min(150px, 100%)" align="center" gap="5">
                    <Chart.Root chart={gameModeChart}>
                        <PieChart responsive>
                            <Pie
                                data={gameModeChart.data}
                                dataKey={gameModeChart.key("value")}
                                nameKey="name"
                                innerRadius={30}
                                startAngle={90}
                                endAngle={-360}
                                stroke="var(--chakra-colors-border)"
                                strokeWidth={1}
                                shape={(props) => (
                                    <Sector
                                        {...props}
                                        fill={gameModeChart.color(props.payload!.color)}
                                        outerRadius={props.isActive ? 70 : 60}
                                        style={{
                                            transition: "all 0.3s ease-out",
                                        }} 
                                    />
                                )}
                            >
                            </Pie>
                            <Tooltip
                                content={({ active, payload }) => {
                                    if (!active || !payload?.length) return null

                                    const gameMode = payload[0].payload

                                    return (
                                        <Box bg="bg.panel" borderWidth="1px" borderColor="border" borderRadius="md" px="3" py="2" boxShadow="md">
                                            <Text fontSize="xs" fontWeight="medium">{gameMode.name}</Text>
                                            <Flex justifyContent="space-between" gap="3">
                                                <Text color="var(--chakra-colors-fg-muted)" fontSize="xs">Games Played</Text>
                                                <Text fontSize="xs" fontWeight="medium">{gameMode.value}</Text>
                                            </Flex>
                                        </Box>
                                    )
                                }}
                            />
                        </PieChart>
                    </Chart.Root>
                    <Text className="stat-name">GAME MODES</Text>
                </Flex>
            </Flex>

            <Flex flexWrap="wrap" justify="center" gap="10" mt="5" flexDirection="row">
                <Flex direction="column" align="center" width="min(600px, 100%)">
                    <Chart.Root width="100%" height="100%" chart={genreChart}>
                        <RadarChart data={genreChart.data} responsive>
                            <PolarGrid stroke={genreChart.color("border")} />
                            <PolarAngleAxis dataKey="name" tick={({ payload, x, y, textAnchor }) => (
                                <text
                                    x={x}
                                    y={y}
                                    textAnchor={textAnchor}
                                    fill="var(--chakra-colors-fg-muted)"
                                    fontSize={12}
                                >
                                    {payload.value === "Hack and slash/Beat 'em up" ? (
                                        <>
                                            <tspan x={x} dy="0">Hack and slash/</tspan>
                                            <tspan x={x} dy="15">Beat 'em up</tspan>
                                        </>
                                    ) : (
                                        payload.value
                                    )}
                                </text>
                            )} />
                            <Tooltip content={<Chart.Tooltip />} />
                            <Radar
                                dataKey="value"
                                fill="var(--chakra-colors-teal-solid)"
                                fillOpacity={0.35}
                                stroke="var(--chakra-colors-teal-solid)"
                            />
                        </RadarChart>
                    </Chart.Root>
                    <Text className="stat-name">GENRES</Text>
                </Flex>

                <Flex direction="column" align="center" width="min(600px, 100%)">
                    <Chart.Root width="100%" height="100%" chart={themeChart}>
                        <RadarChart data={themeChart.data} responsive>
                            <PolarGrid stroke={themeChart.color("border")} />
                            <PolarAngleAxis dataKey="name" tick={({ payload, x, y, textAnchor }) => (
                                <text
                                    x={x}
                                    y={y}
                                    textAnchor={textAnchor}
                                    fill="var(--chakra-colors-fg-muted)"
                                    fontSize={12}
                                >
                                    {payload.value === "4X (explore, expand, exploit, and exterminate)" ? (
                                        <>
                                            <tspan x={x} dy="0">4X (explore,</tspan>
                                            <tspan x={x} dy="15">expand, exploit</tspan>
                                            <tspan x={x} dy="15">and exterminate)</tspan>
                                        </>
                                    ) : (
                                        payload.value
                                    )}
                                </text>
                            )} />
                            <Tooltip content={<Chart.Tooltip />} />
                            <Radar
                                dataKey="value"
                                fill="var(--chakra-colors-purple-solid)"
                                fillOpacity={0.35}
                                stroke="var(--chakra-colors-purple-solid)"
                            />
                        </RadarChart>
                    </Chart.Root>
                    <Text className="stat-name">THEMES</Text>
                </Flex>
            </Flex>

            <Flex justify="center">
                <Field.Root width="230px">
                    <VisuallyHidden>
                        <Field.Label>Chart metric</Field.Label>
                    </VisuallyHidden>
                    <Select.Root size="sm" width="250px" textAlign="center" collection={statCollection} value={[radarStat]}
                        onValueChange={(details) => setRadarStat(details.value[0])} aria-label="Chart metric"
                    >
                        <Select.HiddenSelect />
                        <Select.Control>
                            <Select.Trigger>
                                <Select.ValueText />
                            </Select.Trigger>
                            <Select.IndicatorGroup>
                                <Select.Indicator />
                            </Select.IndicatorGroup>
                        </Select.Control>

                        <Select.Positioner>
                            <Select.Content>
                                {statOptions.map(option => (
                                    <Select.Item item={option.value} key={option.value}>
                                        {option.label}
                                        <Select.ItemIndicator />
                                    </Select.Item>
                                ))}
                            </Select.Content>
                        </Select.Positioner>
                    </Select.Root>
                </Field.Root>
            </Flex>

            <Flex direction="column" align="center" mt="10">
                <Chart.Root maxW="1000px" maxH="sm" chart={releaseYearChart}>
                    <BarChart barCategoryGap="0.1" data={releaseYearChart.data} responsive>
                        <XAxis axisLine={false} tickLine={false} dataKey={releaseYearChart.key("name")} />
                        <Tooltip
                            cursor={false}
                            content={<Chart.Tooltip animationDuration={0}/>}
                        />
                        {releaseYearChart.series.map((item) => (
                            <Bar key={item.name} dataKey={releaseYearChart.key(item.name)} fill={releaseYearChart.color(item.color)}>
                                <LabelList dataKey={releaseYearChart.key(item.name)} position="top" />
                            </Bar>
                        ))}
                    </BarChart>
                </Chart.Root>
                <Text className="stat-name">YEAR OF RELEASE</Text>
            </Flex>
        </Box>
    )
}