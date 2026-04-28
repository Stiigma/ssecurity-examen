import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'ExamenSecurity A09',
  tagline: 'Hacking etico y defensa aplicada',
  favicon: 'img/favicon.ico',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://example.com',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: '/',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'uabc-demo',
  projectName: 'ExamenSecurity',

  onBrokenLinks: 'throw',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'es',
    locales: ['es'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    // Replace with your project's social card
    image: 'img/docusaurus-social-card.jpg',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'ExamenSecurity A09',
      logo: {
        alt: 'ExamenSecurity A09',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'tutorialSidebar',
          position: 'left',
          label: 'Documentacion',
        },
        {
          to: '/docs/canva-map',
          label: 'Canva',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {
              label: 'Inicio',
              to: '/docs/intro',
            },
            {
              label: 'Mapa Canva',
              to: '/docs/canva-map',
            },
          ],
        },
        {
          title: 'Demo',
          items: [
            {
              label: 'Red Team',
              to: '/docs/demo-red-team-main',
            },
            {
              label: 'Blue Team',
              to: '/docs/demo-blue-team-fixed',
            },
          ],
        },
        {
          title: 'Proyecto',
          items: [
            {
              label: 'Causa raiz',
              to: '/docs/causa-raiz',
            },
            {
              label: 'Implementacion',
              to: '/docs/implementacion-fixed',
            },
          ],
        },
      ],
      copyright: `ExamenSecurity ${new Date().getFullYear()} - laboratorio academico local.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
