import React from 'react';
import ComponentCreator from '@docusaurus/ComponentCreator';

export default [
  {
    path: '/__docusaurus/debug',
    component: ComponentCreator('/__docusaurus/debug', '5ff'),
    exact: true
  },
  {
    path: '/__docusaurus/debug/config',
    component: ComponentCreator('/__docusaurus/debug/config', '5ba'),
    exact: true
  },
  {
    path: '/__docusaurus/debug/content',
    component: ComponentCreator('/__docusaurus/debug/content', 'a2b'),
    exact: true
  },
  {
    path: '/__docusaurus/debug/globalData',
    component: ComponentCreator('/__docusaurus/debug/globalData', 'c3c'),
    exact: true
  },
  {
    path: '/__docusaurus/debug/metadata',
    component: ComponentCreator('/__docusaurus/debug/metadata', '156'),
    exact: true
  },
  {
    path: '/__docusaurus/debug/registry',
    component: ComponentCreator('/__docusaurus/debug/registry', '88c'),
    exact: true
  },
  {
    path: '/__docusaurus/debug/routes',
    component: ComponentCreator('/__docusaurus/debug/routes', '000'),
    exact: true
  },
  {
    path: '/markdown-page',
    component: ComponentCreator('/markdown-page', '53a'),
    exact: true
  },
  {
    path: '/docs',
    component: ComponentCreator('/docs', 'd18'),
    routes: [
      {
        path: '/docs',
        component: ComponentCreator('/docs', '232'),
        routes: [
          {
            path: '/docs',
            component: ComponentCreator('/docs', 'fbe'),
            routes: [
              {
                path: '/docs/canva-map',
                component: ComponentCreator('/docs/canva-map', 'e49'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/causa-raiz',
                component: ComponentCreator('/docs/causa-raiz', '2ef'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/concepto-a09',
                component: ComponentCreator('/docs/concepto-a09', 'f87'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/contexto-examen',
                component: ComponentCreator('/docs/contexto-examen', '534'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/demo-blue-team-fixed',
                component: ComponentCreator('/docs/demo-blue-team-fixed', '2cd'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/demo-red-team-main',
                component: ComponentCreator('/docs/demo-red-team-main', '7ec'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/diseno-fixed',
                component: ComponentCreator('/docs/diseno-fixed', '142'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/endpoints',
                component: ComponentCreator('/docs/endpoints', '32a'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/guion-presentacion',
                component: ComponentCreator('/docs/guion-presentacion', 'ef1'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/implementacion-fixed',
                component: ComponentCreator('/docs/implementacion-fixed', '818'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/intro',
                component: ComponentCreator('/docs/intro', '89a'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/reglas-alertas',
                component: ComponentCreator('/docs/reglas-alertas', '932'),
                exact: true,
                sidebar: "tutorialSidebar"
              },
              {
                path: '/docs/version-vulnerable-main',
                component: ComponentCreator('/docs/version-vulnerable-main', 'e8d'),
                exact: true,
                sidebar: "tutorialSidebar"
              }
            ]
          }
        ]
      }
    ]
  },
  {
    path: '/',
    component: ComponentCreator('/', 'e5f'),
    exact: true
  },
  {
    path: '*',
    component: ComponentCreator('*'),
  },
];
