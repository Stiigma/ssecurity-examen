"""Account lockout scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario
import config


class AccountLockout(BaseScenario):
    @property
    def name(self) -> str:
        return "AccountLockout"

    @property
    def description(self) -> str:
        return "5 failed logins to lock account; 6th attempt should return 423 Locked."

    def run_attack(self) -> None:
        self._status_codes = []
        for i in range(6):
            resp = self.client.login(config.STUDENT_EMAIL, "WrongPassword123!")
            self._status_codes.append(resp.status_code)
            if resp.status_code == 423:
                self._locked_on_request = i + 1
                break
        else:
            self._locked_on_request = None

    def verify_defense(self) -> bool:
        got_423 = 423 in self._status_codes

        events_resp = self.client.get_events(params={"eventType": "LockoutAttemptDuringLock", "pageSize": 100})
        events_resp.raise_for_status()
        events = events_resp.json()["items"]
        lockout_event_found = any(e.get("username") == config.STUDENT_EMAIL for e in events)

        self._got_423 = got_423
        self._lockout_event_found = lockout_event_found
        return got_423 and lockout_event_found

    def cleanup(self) -> None:
        # Unlock the account if it was locked
        try:
            lockouts_resp = self.client.get_lockouts()
            lockouts_resp.raise_for_status()
            lockouts = lockouts_resp.json()
            for lo in lockouts:
                if lo.get("targetValue") == config.STUDENT_EMAIL and lo.get("isActive"):
                    self.client.unlock_lockout(lo["id"])
                    break
        except Exception:
            pass

    def _attack_summary(self) -> str:
        return f"5x failed login then 1x attempt. Status codes: {self._status_codes}"

    def _details(self, defense_detected: bool) -> List[str]:
        return [
            f"Status codes: {self._status_codes}",
            f"423 Locked received: {getattr(self, '_got_423', 'N/A')}",
            f"LockoutAttemptDuringLock event: {getattr(self, '_lockout_event_found', 'N/A')}",
        ]
