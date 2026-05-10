#!/usr/bin/env python3
"""Red Team Scenarios entry point."""

import sys
from typing import List, Optional

import typer
from api_client import ApiClient
from scenarios import (
    PasswordSpraying,
    AdminProbing,
    RecordProbing,
    Honeytoken,
    RateLimit,
    AccountLockout,
    LogIntegrity,
    SecurityTicket,
    ImpossibleTravel,
)
from reporters import ConsoleReporter, MarkdownReporter
import config

app = typer.Typer(help="Red Team Scenarios for ExamenSecurity backend")


@app.command()
def run(
    mode: str = typer.Option("fixed", "--mode", "-m", help="fixed or vulnerable"),
    target: str = typer.Option(config.BASE_URL, "--target", "-t", help="Base URL of the API"),
    output: Optional[str] = typer.Option(None, "--output", "-o", help="Markdown report file path"),
):
    if mode not in ("fixed", "vulnerable"):
        typer.echo("Error: mode must be 'fixed' or 'vulnerable'", err=True)
        raise typer.Exit(code=1)

    client = ApiClient(base_url=target)
    reporter = ConsoleReporter()
    md_reporter = MarkdownReporter(output_path=output or "")

    # Verify API is reachable
    try:
        health = client.get("/api/demo/health")
        if health.status_code not in (200, 404):
            typer.echo(f"Warning: health check returned {health.status_code}", err=True)
    except Exception as exc:
        reporter.print_error(f"API no disponible en {target}: {exc}")
        raise typer.Exit(code=1)

    # Pre-resolve IDs needed by scenarios
    try:
        client.resolve_student_ids()
    except Exception as exc:
        reporter.print_error(f"No se pudieron resolver IDs de estudiantes: {exc}")
        raise typer.Exit(code=1)

    reporter.print_header(mode, target)

    scenarios = [
        PasswordSpraying(client, mode),
        AdminProbing(client, mode),
        RecordProbing(client, mode),
        Honeytoken(client, mode),
        RateLimit(client, mode),
        AccountLockout(client, mode),
        LogIntegrity(client, mode),
        SecurityTicket(client, mode),
        ImpossibleTravel(client, mode),
    ]

    results = reporter.run_with_progress(scenarios, mode)
    reporter.print_results(results)
    reporter.print_summary(results, mode)

    md_path = md_reporter.write(results, mode, target)
    typer.echo(f"\nReporte Markdown guardado en: {md_path}")

    # Exit with non-zero if any scenario failed
    if not all(r.passed for r in results):
        raise typer.Exit(code=1)


if __name__ == "__main__":
    app()
