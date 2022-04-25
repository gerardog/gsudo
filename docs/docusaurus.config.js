// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const lightCodeTheme = require('prism-react-renderer/themes/github');
const darkCodeTheme = require('prism-react-renderer/themes/dracula');

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'gsudo (sudo for windows)',
  tagline: 'The missing piece in Windows. Cherry-pick which commands to elevate with just one keyword.',
  url: 'https://your-docusaurus-test-site.com',
  baseUrl: '/gsudo/',
  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',
  favicon: 'img/favicon.ico',
  organizationName: 'gerardog', // Usually your GitHub org/user name.
  projectName: 'gsudo', // Usually your repo name.
  trailingSlash: false,

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: require.resolve('./sidebars.js'),
          // Please change this to your repo.
          editUrl: 'https://github.com/gerardog/gsudo/blob/docs/docs/',
        },
        blog: {
          showReadingTime: true,
          // Please change this to your repo.
          editUrl:
            'https://github.com/gerardog/gsudo/tree/docs/docs/blog/',
        },
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
        googleAnalytics: {
          trackingID: 'G-RLH6G3T39R',
          anonymizeIP: true,
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        defaultMode: 'dark',
      },
      navbar: {
        title: 'gsudo',
        logo: {
          alt: 'gsudo Logo',
          src: 'img/gsudo.png',
        },
        items: [
          {
            type: 'doc',
            docId: 'intro',
            position: 'left',
            label: 'Docs',
          },
          {to: '/docs/install', label: 'Install', position: 'left'},
          {to: '/docs/sponsor', label: 'Sponsor', position: 'left'},
          {to: '/blog', label: 'Blog', position: 'left'},
          {
            href: 'https://github.com/gerardog/gsudo',
            label: 'GitHub',
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
                label: 'Tutorial',
                to: '/docs/intro',
              },
            ],
          },
          {
            title: 'Community',
            items: [ /*
              {
                label: 'Stack Overflow',
                href: 'https://stackoverflow.com/questions/tagged/docusaurus',
              },*/
              {
                label: 'Discord',
                href: 'https://discord.gg/dEEA3P5WqF',
              }, 
              {
                label: 'Report an Issue',
                href: 'https://github.com/gerardog/gsudo/issues',
              },
              {
                label: 'Follow me on Twitter',
                href: 'https://twitter.com/gerardo_gr',
              },
            ],
          },
          {
            title: 'More',
            items: [
              {
                label: 'Blog',
                to: '/blog',
              },
              {
                label: 'GitHub',
                href: 'https://github.com/gerardog/gsudo',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} Gerardo Grignoli. Built with Docusaurus.`,
      },
      prism: {
        theme: lightCodeTheme,
        darkTheme: darkCodeTheme,
        additionalLanguages: ['powershell'],
      },
    }),
};

module.exports = config;
