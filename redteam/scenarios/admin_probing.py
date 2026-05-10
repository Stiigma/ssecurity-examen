"""Admin endpoint probing scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario


class AdminProbing(BaseScenario):
    @property
    def name(self) -> str:
        return "AdminProbing"

    @property
    def description(self) -> str:
        return "Student JWT attempts to access admin endpoints 3 times."

    def run_attack(self) -> None:
        headers = self.client.student_headers()
        for _ in range(3):
            resp = self.client.get("/api/admin/users", headers=headers)
            if resp.status_code not in (403, 401):
                resp.raise_for_status()

    def verify_defense(self) -> bool:
        events_resp = self.client.get_events(params={"eventType": "AccessDenied", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        access_denied_count = sum(
            1 for e in events
            if e.get("path", "").startswith("/api/admin") and e.get("username") == "student1@demo.local"
        )

        alerts_resp = self.client.get_alerts(params={"alertType": "AdminEndpointProbing", "pageSize": 100})
        alerts_resp.raise_for_status()
        alerts = alerts_resp.json()["items"]
        alert_found = any(a.get("relatedUsername") == "student1@demo.local" for a in alerts)

        self._access_denied_count = access_denied_count
        self._alert_found = alert_found
        return access_denied_count >= 3 and alert_found

    def _attack_summary(self) -> str:
        return "3x GET /api/admin/users with student JWT"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"AccessDenied events on /api/admin: {getattr(self, '_access_denied_count', 'N/A')}",
            f"AdminEndpointProbing alert found: {getattr(self, '_alert_found', 'N/A')}",
        ]
