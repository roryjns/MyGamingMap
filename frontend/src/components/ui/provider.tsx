"use client"

import {
  ChakraProvider,
  createSystem,
  defaultConfig,
  defineConfig,
} from "@chakra-ui/react"

import {
  ColorModeProvider,
  type ColorModeProviderProps,
} from "./color-mode"

const config = defineConfig({
  theme: {
    tokens: {
      colors: {
        brand: {
          blue: { value: "#2e6db4" },
          green: { value: "#2adc95" },
          yellow: { value: "#f5d849" },
          red: { value: "#f75f66" },
          dark_blue: { value: "#132b47" },
        }
      },
    },

    semanticTokens: {
      colors: {
        bg: {
          DEFAULT: {
            value: {
              _light: "#fffffff2",
              _dark: "#000b20f2 ",
            },
          },
        },
      },
    },

    recipes: {
      button: {
        base: {
          _hover: {
            _dark: {
              bg: "brand.dark_blue",
            },
          },
          _active: {
            _dark: {
              bg: "brand.dark_blue",
            },
          },
          _open: {
            _dark: {
              bg: "brand.dark_blue",
            },
          }
        },
      },
    }
  },
})

const system = createSystem(defaultConfig, config)

export function Provider(props: ColorModeProviderProps) {
  return (
    <ChakraProvider value={system}>
      <ColorModeProvider {...props} />
    </ChakraProvider>
  )
}