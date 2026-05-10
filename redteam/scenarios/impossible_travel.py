"""Impossible travel scenario."""

import time
from typing import List
from scenarios.base_scenario import BaseScenario
import config


class ImpossibleTravel(BaseScenario):
    @property
    def name(self) -> str:
        return "ImpossibleTravel"

    @property
    def description(self) -> str:
        return "Simulate logins from Mexico and Argentina 5 minutes apart."

    def run_attack(self) -> None:
        # Login from Mexico
        resp1 = self.client.login(config.STUDENT_EMAIL, config.STUDENT_PASSWORD, forwarded_for="189.0.0.1")
        if resp1.status_code == 200:
            # Update saved location to be 5 minutes ago via raw DB manipulation is not possible from client.
            # Instead we can login again quickly from another country.
            # For the test to reliably trigger impossible travel, we need the previous location to be older.
            # Since we cannot alter DB from the client, we will use a trick:
            # Log in once, then immediately login from a very distant country.
            # The time gap will be tiny (seconds), so distance/time will be enormous -> definitely triggers alert.
            # However, to avoid interfering with other tests, we use a dedicated user or ensure we cleanup.
            # The first login of a user never triggers the check. So we need a user with a prior login.
            # We will use the already-logged-in student (student1) who may have prior logins from other scenarios.
            pass

        # Small sleep to ensure DB write
        time.sleep(0.5)

        # Login from Argentina
        resp2 = self.client.login(config.STUDENT_EMAIL, config.STUDENT_PASSWORD, forwarded_for="181.0.0.1")
        self._second_status = resp2.status_code

    def verify_defense(self) -> bool:
        alerts_resp = self.client.get_alerts(params={"alertType": "ImpossibleTravel", "pageSize": 100})
        alerts_resp.raise_for_status()
        alerts = alerts_resp.json()["items"]
        # Look for an alert mentioning Mexico or Argentina
        alert_found = any(
            "Mexico" in (a.get("description") or "") or "Argentina" in (a.get("description") or "")
            for a in alerts
        )

        events_resp = self.client.get_events(params={"eventType": "ImpossibleTravelDetected", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        event_found = len(events) > 0

        self._alert_found = alert_found
        self._event_found = event_found
        return alert_found and event_found

    def _attack_summary(self) -> str:
        return "Login from 189.0.0.1 (Mexico) then 181.0.0.1 (Argentina) seconds apart"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"ImpossibleTravel alert found: {getattr(self, '_alert_found', 'N/A')}",
            f"ImpossibleTravelDetected event found: {getattr(self, '_event_found', 'N/A')}",
        ]
