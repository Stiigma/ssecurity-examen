"""Rate limit scenario."""

from typing import List
from scenarios.base_scenario import BaseScenario
import config


class RateLimit(BaseScenario):
    @property
    def name(self) -> str:
        return "RateLimit"

    @property
    def description(self) -> str:
        return "6 rapid login attempts to trigger HTTP 429 Too Many Requests."

    def run_attack(self) -> None:
        self._status_codes = []
        for i in range(6):
            resp = self.client.login(config.ADMIN_EMAIL, "WrongPassword123!")
            self._status_codes.append(resp.status_code)
            # stop early if we already got 429
            if resp.status_code == 429:
                self._429_on_request = i + 1
                break
        else:
            self._429_on_request = None

    def verify_defense(self) -> bool:
        got_429 = 429 in self._status_codes
        has_retry_after = False
        # Verify at least one 429 response had Retry-After header (optional but nice)
        # We can't easily retro-check headers, so we rely on status code.
        self._got_429 = got_429
        return got_429

    def _attack_summary(self) -> str:
        return f"6x POST /api/auth/login rapidly. Status codes: {self._status_codes}"

    def _details(self, defense_detected: bool) -> List[str]:
        details = [f"Status codes received: {self._status_codes}"]
        if self._429_on_request:
            details.append(f"First 429 received on request #{self._429_on_request}")
        return details
