"""Base scenario class."""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional
from api_client import ApiClient


@dataclass
class ScenarioResult:
    name: str
    description: str
    attack_summary: str
    defense_detected: bool
    passed: bool
    details: List[str] = field(default_factory=list)
    duration_seconds: float = 0.0
    mode: str = "fixed"

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "description": self.description,
            "attack_summary": self.attack_summary,
            "defense_detected": self.defense_detected,
            "passed": self.passed,
            "details": self.details,
            "duration_seconds": self.duration_seconds,
            "mode": self.mode,
        }


class BaseScenario(ABC):
    def __init__(self, client: ApiClient, mode: str = "fixed"):
        self.client = client
        self.mode = mode  # "fixed" or "vulnerable"

    @property
    @abstractmethod
    def name(self) -> str:
        ...

    @property
    @abstractmethod
    def description(self) -> str:
        ...

    def run(self) -> ScenarioResult:
        import time
        start = time.perf_counter()
        try:
            self.run_attack()
            defense_detected = self.verify_defense()
        except Exception as exc:
            elapsed = time.perf_counter() - start
            return ScenarioResult(
                name=self.name,
                description=self.description,
                attack_summary=f"Attack failed with exception: {exc}",
                defense_detected=False,
                passed=False,
                details=[str(exc)],
                duration_seconds=elapsed,
                mode=self.mode,
            )

        elapsed = time.perf_counter() - start
        passed = self._evaluate(defense_detected)
        return ScenarioResult(
            name=self.name,
            description=self.description,
            attack_summary=self._attack_summary(),
            defense_detected=defense_detected,
            passed=passed,
            details=self._details(defense_detected),
            duration_seconds=elapsed,
            mode=self.mode,
        )

    def _evaluate(self, defense_detected: bool) -> bool:
        if self.mode == "fixed":
            return defense_detected
        return not defense_detected

    @abstractmethod
    def run_attack(self) -> None:
        """Execute the attack (HTTP requests)."""
        ...

    @abstractmethod
    def verify_defense(self) -> bool:
        """Query security endpoints and return True if defense evidence exists."""
        ...

    def _attack_summary(self) -> str:
        return ""

    def _details(self, defense_detected: bool) -> List[str]:
        return []

    def cleanup(self) -> None:
        """Optional cleanup after scenario (e.g. unlock account)."""
        pass
