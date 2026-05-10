"""Password spraying scenario: multiple failed logins."""

from typing import List
from scenarios.base_scenario import BaseScenario
import config


class PasswordSpraying(BaseScenario):
    @property
    def name(self) -> str:
        return "PasswordSpraying"

    @property
    def description(self) -> str:
        return "5 failed login attempts as admin@demo.local to trigger MultipleFailedLogins alert."

    def run_attack(self) -> None:
        for _ in range(5):
            resp = self.client.login(config.ADMIN_EMAIL, "WrongPassword123!")
            # Expect 401
            if resp.status_code not in (401, 423):
                resp.raise_for_status()

    def verify_defense(self) -> bool:
        # Check for LoginFailed events
        events_resp = self.client.get_events(params={"eventType": "LoginFailed", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        login_failed_count = sum(1 for e in events if e.get("username") == config.ADMIN_EMAIL)

        # Check for MultipleFailedLogins alert
        alerts_resp = self.client.get_alerts(params={"alertType": "MultipleFailedLogins", "pageSize": 100})
        alerts_resp.raise_for_status()
        alerts = alerts_resp.json()["items"]
        alert_found = any(a.get("relatedUsername") == config.ADMIN_EMAIL for a in alerts)

        self._login_failed_count = login_failed_count
        self._alert_found = alert_found
        return login_failed_count >= 5 and alert_found

    def _attack_summary(self) -> str:
        return f"5x POST /api/auth/login with wrong password for {config.ADMIN_EMAIL}"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"LoginFailed events for target: {getattr(self, '_login_failed_count', 'N/A')}",
            f"MultipleFailedLogins alert found: {getattr(self, '_alert_found', 'N/A')}",
        ]
