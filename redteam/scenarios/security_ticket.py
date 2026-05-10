"""Security ticket scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario


class SecurityTicket(BaseScenario):
    @property
    def name(self) -> str:
        return "SecurityTicket"

    @property
    def description(self) -> str:
        return "Create a security-relevant support ticket and verify alert."

    def run_attack(self) -> None:
        headers = self.client.student_headers()
        payload = {
            "subject": "RedTeam: suspicious activity",
            "description": "Someone tried to access my account from another country.",
            "isSecurityRelevant": True,
        }
        resp = self.client.post("/api/support-tickets", json=payload, headers=headers)
        if resp.status_code not in (201, 200):
            resp.raise_for_status()
        self._ticket_id = resp.json().get("id") if resp.status_code == 201 else None

    def verify_defense(self) -> bool:
        events_resp = self.client.get_events(params={"eventType": "SecurityTicketCreated", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        event_found = any("RedTeam" in (e.get("message") or "") for e in events)

        alerts_resp = self.client.get_alerts(params={"alertType": "SecurityTicketRequiresReview", "pageSize": 100})
        alerts_resp.raise_for_status()
        alerts = alerts_resp.json()["items"]
        alert_found = len(alerts) > 0

        self._event_found = event_found
        self._alert_found = alert_found
        return event_found and alert_found

    def _attack_summary(self) -> str:
        return "POST /api/support-tickets with isSecurityRelevant=true"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"SecurityTicketCreated event found: {getattr(self, '_event_found', 'N/A')}",
            f"SecurityTicketRequiresReview alert found: {getattr(self, '_alert_found', 'N/A')}",
        ]
