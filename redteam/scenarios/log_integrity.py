"""Log integrity scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario


class LogIntegrity(BaseScenario):
    @property
    def name(self) -> str:
        return "LogIntegrity"

    @property
    def description(self) -> str:
        return "Verify that the security log integrity check reports a valid chain."

    def run_attack(self) -> None:
        # No attack needed; just query the endpoint
        self._integrity_resp = self.client.integrity_check()

    def verify_defense(self) -> bool:
        if not hasattr(self, "_integrity_resp"):
            return False
        resp = self._integrity_resp
        if resp.status_code not in (200, 409):
            resp.raise_for_status()
        data = resp.json()
        self._is_valid = data.get("isValid", False)
        self._total_events = data.get("totalEventsChecked", 0)
        return self._is_valid

    def _attack_summary(self) -> str:
        return "GET /api/security/integrity-check"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"Integrity check isValid: {getattr(self, '_is_valid', 'N/A')}",
            f"Total events checked: {getattr(self, '_total_events', 'N/A')}",
        ]
