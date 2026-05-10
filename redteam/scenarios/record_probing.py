"""Student record probing scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario
import config


class RecordProbing(BaseScenario):
    @property
    def name(self) -> str:
        return "RecordProbing"

    @property
    def description(self) -> str:
        return "Student attempts to access another student's record twice."

    def run_attack(self) -> None:
        self.client.resolve_student_ids()
        headers = self.client.student_headers()
        target_id = config.STUDENT2_ID
        for _ in range(2):
            resp = self.client.get(f"/api/student-records/by-user/{target_id}", headers=headers)
            if resp.status_code not in (403, 401):
                resp.raise_for_status()

    def verify_defense(self) -> bool:
        events_resp = self.client.get_events(params={"eventType": "StudentRecordAccessDenied", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        denied_count = sum(
            1 for e in events
            if e.get("userId") == config.STUDENT1_ID
        )

        alerts_resp = self.client.get_alerts(params={"alertType": "StudentRecordProbing", "pageSize": 100})
        alerts_resp.raise_for_status()
        alerts = alerts_resp.json()["items"]
        alert_found = any(a.get("relatedUserId") == config.STUDENT1_ID for a in alerts)

        self._denied_count = denied_count
        self._alert_found = alert_found
        return denied_count >= 2 and alert_found

    def _attack_summary(self) -> str:
        return f"2x GET /api/student-records/by-user/{config.STUDENT2_ID} as student1"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"StudentRecordAccessDenied events: {getattr(self, '_denied_count', 'N/A')}",
            f"StudentRecordProbing alert found: {getattr(self, '_alert_found', 'N/A')}",
        ]
