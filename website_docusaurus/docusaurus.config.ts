import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'WitWeaver Documentation',
  tagline: 'A modular Unity dialogue framework',
  url: 'https://docs.wolfstaginteractive.com',
  baseUrl: '/',

  plugins: [
    [
      require.resolve("@easyops-cn/docusaurus-search-local"),
      {
        hashed: true,
        language: ["en"],
        indexDocs: true,
        indexBlog: false,
        indexPages: false,
        docsRouteBasePath: "/witweaver",
      },
    ],
  ],

  presets: [
    [
      'classic',
      {
        docs: {
          routeBasePath: 'witweaver',
          sidebarPath: require.resolve('./sidebars.ts'),
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    navbar: {
      title: 'WitWeaver',
      items: [
        {to: '/witweaver/', label: 'Guide', position: 'left'},
        {href: 'https://docs.wolfstaginteractive.com/witweaver/api/', label: 'API', position: 'left'},
      ],
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['yaml', 'csharp'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
