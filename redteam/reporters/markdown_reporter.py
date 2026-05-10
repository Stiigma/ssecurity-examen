"""Markdown reporter that writes a report file."""

from datetime import datetime
from pathlib import Path
from typing import List

from scenarios.base_scenario import ScenarioResult


class MarkdownReporter:
    def __init__(self, output_path: str = ""):
        if not output_path:
            date_str = datetime.now().strftime("%Y-%m-%d")
            output_path = f"reporte-red-team-{date_str}.md"
        self.output_path = Path(output_path)

    def write(self, results: List[ScenarioResult], mode: str, target: str):
        lines: List[str] = []
        lines.append("# Red Team Scenarios Report\n")
        lines.append(f"**Fecha:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        lines.append(f"**Target:** {target}\n")
        lines.append(f"**Modo:** `{mode}`\n")
        lines.append("---\n")

        lines.append("## Resumen\n")
        passed = sum(1 for r in results if r.passed)
        total = len(results)
        lines.append(f"- **Escenarios ejecutados:** {total}\n")
        lines.append(f"- **Escenarios pasados:** {passed}\n")
        lines.append(f"- **Escenarios fallidos:** {total - passed}\n")
        lines.append("\n")

        lines.append("## Detalle por escenario\n")
        for idx, r in enumerate(results, 1):
            status_icon = "✅" if r.passed else "❌"
            lines.append(f"### {idx}. {r.name} {status_icon}\n")
            lines.append(f"- **Descripción:** {r.description}\n")
            lines.append(f"- **Ataque:** {r.attack_summary}\n")
            lines.append(f"- **Defensa detectada:** {'Sí' if r.defense_detected else 'No'}\n")
            lines.append(f"- **Resultado:** {'PASS' if r.passed else 'FAIL'}\n")
            lines.append(f"- **Tiempo:** {r.duration_seconds:.2f}s\n")
            if r.details:
                lines.append("- **Detalles:**\n")
                for d in r.details:
                    lines.append(f"  - {d}\n")
            lines.append("\n")

        lines.append("## Conclusión\n")
        if passed == total:
            lines.append(
                "La rama **fixed** demuestra evidencia durable frente a cada ataque simulado. "
                "Todos los escenarios generaron eventos y/o alertas de seguridad como se esperaba.\n"
            )
        else:
            lines.append(
                f"Se detectaron {total - passed} escenario(s) donde la defensa no respondió como se esperaba. "
                "Revisar los detalles individuales para más información.\n"
            )

        self.output_path.write_text("".join(lines), encoding="utf-8")
        return str(self.output_path)
