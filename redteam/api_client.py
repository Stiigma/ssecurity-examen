"""HTTP client wrapper with JWT handling."""

from typing import Any, Optional
import requests
import config


class ApiClient:
    def __init__(self, base_url: str = config.BASE_URL):
        self.base_url = base_url.rstrip("/")
        self.session = requests.Session()
        self.session.headers["Content-Type"] = "application/json"
        self._admin_token: Optional[str] = None
        self._student_token: Optional[str] = None
        self._auditor_token: Optional[str] = None

    # ------------------------------------------------------------------
    # Low-level helpers
    # ------------------------------------------------------------------
    def get(self, path: str, headers: Optional[dict] = None, **kwargs) -> requests.Response:
        return self.session.get(self._url(path), headers=headers, timeout=config.REQUEST_TIMEOUT, **kwargs)

    def post(self, path: str, json: Optional[Any] = None, headers: Optional[dict] = None, **kwargs) -> requests.Response:
        return self.session.post(self._url(path), json=json, headers=headers, timeout=config.REQUEST_TIMEOUT, **kwargs)

    def _url(self, path: str) -> str:
        if path.startswith("http"):
            return path
        return f"{self.base_url}{path}"

    # ------------------------------------------------------------------
    # Auth helpers
    # ------------------------------------------------------------------
    def login(self, email: str, password: str, forwarded_for: Optional[str] = None) -> requests.Response:
        headers = {}
        if forwarded_for:
            headers["X-Forwarded-For"] = forwarded_for
        return self.post("/api/auth/login", json={"email": email, "password": password}, headers=headers)

    def get_admin_token(self) -> str:
        if self._admin_token is None:
            resp = self.login(config.ADMIN_EMAIL, config.ADMIN_PASSWORD)
            resp.raise_for_status()
            self._admin_token = resp.json()["accessToken"]
        return self._admin_token

    def get_student_token(self) -> str:
        if self._student_token is None:
            resp = self.login(config.STUDENT_EMAIL, config.STUDENT_PASSWORD)
            resp.raise_for_status()
            self._student_token = resp.json()["accessToken"]
        return self._student_token

    def get_auditor_token(self) -> str:
        if self._auditor_token is None:
            resp = self.login(config.AUDITOR_EMAIL, config.AUDITOR_PASSWORD)
            resp.raise_for_status()
            self._auditor_token = resp.json()["accessToken"]
        return self._auditor_token

    def auth_headers(self, token: str) -> dict:
        return {"Authorization": f"Bearer {token}"}

    def admin_headers(self) -> dict:
        return self.auth_headers(self.get_admin_token())

    def student_headers(self) -> dict:
        return self.auth_headers(self.get_student_token())

    def auditor_headers(self) -> dict:
        return self.auth_headers(self.get_auditor_token())

    # ------------------------------------------------------------------
    # Security endpoints
    # ------------------------------------------------------------------
    def get_events(self, params: Optional[dict] = None, token: Optional[str] = None) -> requests.Response:
        headers = self.auth_headers(token) if token else self.admin_headers()
        return self.get("/api/security/events", headers=headers, params=params)

    def get_alerts(self, params: Optional[dict] = None, token: Optional[str] = None) -> requests.Response:
        headers = self.auth_headers(token) if token else self.admin_headers()
        return self.get("/api/security/alerts", headers=headers, params=params)

    def integrity_check(self, token: Optional[str] = None) -> requests.Response:
        headers = self.auth_headers(token) if token else self.admin_headers()
        return self.get("/api/security/integrity-check", headers=headers)

    def get_lockouts(self, token: Optional[str] = None) -> requests.Response:
        headers = self.auth_headers(token) if token else self.admin_headers()
        return self.get("/api/security/lockouts", headers=headers)

    def unlock_lockout(self, lockout_id: str, token: Optional[str] = None) -> requests.Response:
        headers = self.auth_headers(token) if token else self.admin_headers()
        return self.post(f"/api/security/lockouts/{lockout_id}/unlock", headers=headers)

    # ------------------------------------------------------------------
    # Demo / data helpers
    # ------------------------------------------------------------------
    def resolve_student_ids(self):
        """Resolve actual student IDs from the admin users endpoint."""
        resp = self.get("/api/admin/users", headers=self.admin_headers())
        resp.raise_for_status()
        users = resp.json()
        for u in users:
            if u["email"] == config.STUDENT_EMAIL:
                config.STUDENT1_ID = u["id"]
            # find another student to use for cross-access
            if u["email"] == "student2@demo.local":
                config.STUDENT2_ID = u["id"]
