"""Run with BACKEND_HEALTH_URL set to a backend health-check endpoint.

Example (PowerShell):
    $env:BACKEND_HEALTH_URL = "http://localhost:8000/health"
    python -m unittest discover -s tests -p test_backend_connection.py
"""

import os
import unittest
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


class BackendConnectionTests(unittest.TestCase):
    def test_backend_is_reachable(self):
        url = os.environ.get("BACKEND_HEALTH_URL")
        self.assertTrue(
            url,
            "Set BACKEND_HEALTH_URL to the backend's health-check URL before running this test.",
        )

        request = Request(url, headers={"Accept": "application/json"})
        try:
            with urlopen(request, timeout=15) as response:
                self.assertGreaterEqual(response.status, 200)
                self.assertLess(response.status, 300)
        except HTTPError as error:
            self.fail(f"Backend returned HTTP {error.code}: {url}")
        except URLError as error:
            self.fail(f"Could not connect to backend at {url}: {error.reason}")
        except TimeoutError:
            self.fail(f"Timed out connecting to backend at {url}")


if __name__ == "__main__":
    unittest.main()
