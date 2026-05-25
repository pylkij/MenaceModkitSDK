import { defineConfig } from 'vitepress'

export default defineConfig({
  // Core metadata
  title: 'Menace SDK',
  description: 'Modding API and tools for the Unity game Menace',
  lang: 'en-US',
  base: '/MenaceModkitSDK/',

  // Appearance
  appearance: 'dark', // Game docs usually look better dark by default
  
  // Clean URLs (no .html extension)
  cleanUrls: true,

  // Head tags
  head: [
    ['link', { rel: 'icon', type: 'image/png', href: '/favicon.png' }],
    ['meta', { name: 'theme-color', content: '#your-brand-color' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'Menace SDK' }],
    ['meta', { property: 'og:description', content: 'Modding API and tools for Menace' }],
  ],

  themeConfig: {
    // Site logo (place in /public)
    logo: '/logo.png',
    siteTitle: 'Menace SDK',

    // Top navigation
    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'API Reference', link: '/api/' },
      {
        text: 'Community',
        items: [
          { text: 'Discord', link: 'https://discord.gg/jfsFnPzJY' },
          { text: 'GitHub', link: 'https://github.com/pylkij/MenaceModkitSDK' },
        ]
      }
    ],

    // Sidebar
    sidebar: {
      '/guide/': [
        {
          text: 'Introduction',
          items: [
            { text: 'What is Menace SDK?', link: '/guide/what-is-menace-sdk' },
            { text: 'Getting Started', link: '/guide/getting-started' },
            { text: 'Installation', link: '/guide/installation' },
          ]
        },
        {
          text: 'Core Concepts',
          items: [
            { text: 'Your First Mod', link: '/guide/your-first-mod' },
            { text: 'Event Hooks', link: '/guide/event-hooks-guide' },
            { text: 'Asset Loading', link: '/guide/loading-your-model' },
          ]
        },
        {
          text: 'Advanced',
          items: [
            { text: 'Patching', link: '/guide/game-patch' },
            { text: 'Publishing Your Mod', link: '/guide/publishing' },
            { text: 'Dev Mode', link: '/guide/dev-mode' },
          ]
        }
      ],
      '/api/': [
        {
          text: 'Core',
          items: [
            { text: 'Overview', link: '/api/' },
            { text: 'Core Systems', link: '/api/core-systems' },
            { text: 'Mod Error', link: '/api/mod-error'}
          ]
        },
        {
          text: 'Event Hooks',
          items: [
            { text: 'Strategy Event Hooks', link: '/api/events/strategy-event-hooks' },
            { text: 'Tactical Event Hooks', link: '/api/events/tactical-event-hooks' },
          ]
        },
        {
          text: 'Strategy',
          items: [
            { text: 'Operation', link: '/api/strategy/operation'},
            { text: 'Mission', link: '/api/strategy/mission'},
            { text: 'Roster', link: '/api/strategy/roster'},
            { text: 'Perks', link: '/api/strategy/perks'},
            { text: 'OCI', link: '/api/strategy/oci'},
            { text: 'Black Market', link: '/api/strategy/black-market'},
            { text: 'Emotions', link: '/api/strategy/emotions'},
          ]
        },
        {
          text: 'Tactical',
          items: [
            { text: 'Tile Map', link: '/api/tactical/tile-map'},
            { text: 'Line of Sight', link: '/api/tactical/line-of-sight'},
          ]
        }
      ]
    },

    // Search — built-in local search, no Algolia account needed
    search: {
      provider: 'local'
    },

    // Social links (top-right icons)
    socialLinks: [
      { icon: 'github', link: 'https://github.com/pylkij/MenaceModkitSDK' },
      { icon: 'discord', link: 'https://discord.gg/jfsFnPzJY' },
    ],

    // Footer
    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2024 Menace SDK Contributors'
    },

    // Edit link (if your docs are on GitHub)
    editLink: {
      pattern: 'https://github.com/pylkij/MenaceModkitSDK/tree/master/docs:path',
      text: 'Edit this page on GitHub'
    },

    // Right-side page outline
    outline: {
      level: [2, 3], // Show h2 and h3 in the ToC
      label: 'On this page'
    },

    // Previous/Next navigation labels
    docFooter: {
      prev: 'Previous',
      next: 'Next'
    },

    // Last updated timestamp (needs lastUpdated: true at root level)
    lastUpdated: {
      text: 'Last updated',
      formatOptions: { dateStyle: 'medium' }
    },
  },

  // Enable last updated (requires git history)
  lastUpdated: true,

  // Markdown options
  markdown: {
    // Line numbers in code blocks
    lineNumbers: true,
    // Anchor links on headings
    anchor: { permalink: true },
    // Theme — good dark options for code: 'one-dark-pro', 'dracula', 'github-dark'
    theme: {
      light: 'github-light',
      dark: 'one-dark-pro',
    }
  },
})