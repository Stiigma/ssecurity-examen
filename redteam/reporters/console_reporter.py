"""Console reporter using rich for colored output."""

from datetime import datetime
from typing import List

from rich.console import Console
from rich.panel import Panel
from rich.table import Table
from rich.progress import Progress, SpinnerColumn, TextColumn
from rich.text import Text

from scenarios.base_scenario import ScenarioResult


class ConsoleReporter:
    def __init__(self):
        self.console = Console()

    def print_header(self, mode: str, target: str):
        title = Text("RED TEAM SCENARIOS", style="bold red", justify="center")
        subtitle = Text(f"Target: {target}  |  Mode: {mode}", style="dim", justify="center")
        self.console.print(Panel(title, subtitle=subtitle, border_style="red"))

    def print_results(self, results: List[ScenarioResult]):
        table = Table(show_header=True, header_style="bold magenta")
        table.add_column("#", style="dim", width=4)
        table.add_column("Scenario", min_width=20)
        table.add_column("Status", width=8, justify="center")
        table.add_column("Time", width=8, justify="right")
        table.add_column("Defense", width=8, justify="center")
        table.add_column("Details")

        for idx, r in enumerate(results, 1):
            status = "[green]PASS[/green]" if r.passed else "[red]FAIL[/red]"
            defense = "[green]YES[/green]" if r.defense_detected else "[red]NO[/red]"
            details = " | ".join(r.details) if r.details else "—"
            table.add_row(
                str(idx),
                r.name,
                status,
                f"{r.duration_seconds:.2f}s",
                defense,
                details,
            )

        self.console.print(table)

    def print_summary(self, results: List[ScenarioResult], mode: str):
        passed = sum(1 for r in results if r.passed)
        total = len(results)
        color = "green" if passed == total else "yellow" if passed > total // 2 else "red"
        summary = Text(
            f"{passed}/{total} escenarios pasaron en MODO {mode}",
            style=f"bold {color}",
            justify="center",
        )
        self.console.print(Panel(summary, border_style=color))

    def print_error(self, message: str):
        self.console.print(f"[bold red]Error:[/bold red] {message}")

    def run_with_progress(self, scenarios, mode: str) -> List[ScenarioResult]:
        results: List[ScenarioResult] = []
        with Progress(
            SpinnerColumn(),
            TextColumn("[progress.description]{task.description}"),
            console=self.console,
        ) as progress:
            for scn in scenarios:
                task = progress.add_task(f"Running {scn.name}...", total=None)
                result = scn.run()
                results.append(result)
                progress.update(task, description=f"{scn.name} -> {'PASS' if result.passed else 'FAIL'}")
                progress.remove_task(task)
                # Cleanup after each scenario
                try:
                    scn.cleanup()
                except Exception as exc:
                    self.console.print(f"[yellow]Cleanup warning for {scn.name}: {exc}[/yellow]")
        return results
