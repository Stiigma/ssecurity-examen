import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';

import styles from './index.module.css';

export default function Home(): ReactNode {
  return (
    <Layout
      title="OWASP A09"
      description="Documentacion del seminario de Hacking Etico y Defensa Aplicada para OWASP A09">
      <main className={styles.main}>
        <section className={styles.hero}>
          <div className="container">
            <p className={styles.eyebrow}>Seminario de Hacking Etico y Defensa Aplicada</p>
            <Heading as="h1" className={styles.title}>
              A09: Security Logging and Alerting Failures
            </Heading>
            <p className={styles.subtitle}>
              Documentacion tecnica para demostrar la version vulnerable, explicar la causa raiz y defender la rama fixed con eventos y alertas en SQL Server.
            </p>
            <div className={styles.actions}>
              <Link className="button button--primary button--lg" to="/docs/intro">
                Abrir documentacion
              </Link>
              <Link className="button button--secondary button--lg" to="/docs/canva-map">
                Ver mapa Canva
              </Link>
            </div>
          </div>
        </section>
        <section className={styles.grid}>
          <article>
            <h2>Red Team</h2>
            <p>Pruebas sobre la rama vulnerable: login fallido, sondeo admin, expedientes ajenos y eventos vacios.</p>
          </article>
          <article>
            <h2>Causa raiz</h2>
            <p>Analisis de la ausencia de pipeline de auditoria: sin eventos, correlacion, severidad ni alertas.</p>
          </article>
          <article>
            <h2>Blue Team</h2>
            <p>Implementacion fixed con SecurityEvents, SecurityAlerts, correlation ID y endpoints de investigacion.</p>
          </article>
        </section>
      </main>
    </Layout>
  );
}
