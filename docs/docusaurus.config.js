// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const lightCodeTheme = require('prism-react-renderer/themes/github');
const darkCodeTheme = require('prism-react-renderer/themes/dracula');

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'gsudo (sudo for windows)',
  tagline: 'The missing piece in Windows. Cherry-pick which commands to elevate with just one keyword.',
  url: 'https://gerardog.github.io',
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
            'https://github.com/gerardog/gsudo/tree/master/docs/blog/',
        },
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
        gtag: {
          trackingID: 'G-RLH6G3T39R',
          anonymizeIP: true,
		},
		sitemap: {
          changefreq: 'weekly',
          priority: 0.5,
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
          src: 'img/AnimatedPrompt.gif',
        },
        items: [
          {
            type: 'doc',
            docId: 'intro',
            position: 'left',
            label: 'Docs',
          },
          {to: '/docs/install', label: 'Install', position: 'left'},
          {to: '/sponsor', label: 'Sponsor', position: 'left'},
          /**{to: '/blog', label: 'Blog', position: 'left'},**/
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
                label: 'Documentation',
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
        additionalLanguages: ['powershell','batch'],
      },
      algolia: {
        // The application ID provided by Algolia
        appId: '48THU2IKHD',

        // Public API key: it is safe to commit it
        apiKey: '062d00b0a8d02c9951557954935de46e',

        indexName: 'gsudo',

        // Optional: see doc section below
        contextualSearch: true,

        // Optional: Specify domains where the navigation should occur through window.location instead on history.push. Useful when our Algolia config crawls multiple documentation sites and we want to navigate with window.location.href to them.
        externalUrlRegex: 'external\\.com|domain\\.com',

        // Optional: Algolia search parameters
        searchParameters: {},

        // Optional: path for search page that enabled by default (`false` to disable it)
        searchPagePath: 'search',

        //... other Algolia params
      },
      metadata: [{
        name: 'keywords',
        content: 'windows sudo, powershell sudo, sudo for windows, sudo for powershell'
      }]
    }),
};

module.exports = config;
