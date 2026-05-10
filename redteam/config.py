"""Centralized configuration for the red team scenarios."""

import os

BASE_URL = os.getenv("REDTEAM_BASE_URL", "http://localhost:5000")

# Demo credentials
ADMIN_EMAIL = "admin@demo.local"
ADMIN_PASSWORD = "Admin123!"

STUDENT_EMAIL = "student1@demo.local"
STUDENT_PASSWORD = "Student123!"

AUDITOR_EMAIL = "auditor@demo.local"
AUDITOR_PASSWORD = "Auditor123!"

DISABLED_EMAIL = "disabled@demo.local"
DISABLED_PASSWORD = "Disabled123!"

# Timeout for HTTP requests (seconds)
REQUEST_TIMEOUT = 10

# Rate limit window in seconds (must match or be aware of API config)
RATE_LIMIT_WINDOW_SECONDS = 60

# Student IDs for cross-access attempts
STUDENT1_ID = "11111111-1111-1111-1111-111111111111"  # Placeholder; resolved at runtime
STUDENT2_ID = "22222222-2222-2222-2222-222222222222"  # Placeholder; resolved at runtime
