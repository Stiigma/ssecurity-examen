"""Honeytoken scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario


class Honeytoken(BaseScenario):
    @property
    def name(self) -> str:
        return "Honeytoken"

    @property
    def description(self) -> str:
        return "Access honeytoken endpoint and verify critical event + alert."

    def run_attack(self) -> None:
        resp = self.client.get("/api/internal/backup")
        # Accept any status code (200 with fake data is expected)
        if resp.status_code not in (200, 401, 403):
            resp.raise_for_status()

    def verify_defense(self) -> bool:
        events_resp = self.client.get_events(params={"eventType": "HoneytokenTriggered", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        event_found = any("/api/internal/backup" in (e.get("path") or "") for e in events)

        alerts_resp = self.client.get_alerts(params={"alertType": "HoneytokenAccessed", "pageSize": 100})
        alerts_resp.raise_for_status()
        alerts = alerts_resp.json()["items"]
        alert_found = len(alerts) > 0

        self._event_found = event_found
        self._alert_found = alert_found
        return event_found and alert_found

    def _attack_summary(self) -> str:
        return "GET /api/internal/backup (no auth)"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"HoneytokenTriggered event found: {getattr(self, '_event_found', 'N/A')}",
            f"HoneytokenAccessed alert found: {getattr(self, '_alert_found', 'N/A')}",
        ]
