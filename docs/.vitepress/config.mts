import { defineConfig } from 'vitepress'

export default defineConfig({
  base: '/MenaceModkitSDK/',
  title: "Menace SDK",
  description: "A wiki for the Menace SDK",
  themeConfig: {
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Examples', link: '/markdown-examples' }
    ],
    sidebar: [
      {
        text: 'API',
        items: [
          { text: 'TacticalEventHooks', link: '/api/tactical-event-hooks' },
          { text: 'GameMethod', link: '/api/game-method' }
        ]
      }
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/vuejs/vitepress' }
    ]
  }
})
