"""HTTP requests to the deployed backend, configured with BACKEND_HEALTH_URL."""

import json
import os
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen


class BackendAPI:
    def __init__(self):
        parsed = urlsplit(os.environ.get("BACKEND_HEALTH_URL", ""))
        if parsed.scheme not in ("http", "https") or not parsed.hostname:
            raise AssertionError("Set BACKEND_HEALTH_URL to the deployed backend health URL.")
        self.base_url = f"{parsed.scheme}://{parsed.netloc}"

    def request(self, method, path, payload=None, *, token=None, params=None):
        url = self.base_url + path
        if params:
            url += "?" + urlencode(params)
        headers = {"Accept": "application/json"}
        if token:
            headers["Authorization"] = f"Bearer {token}"
        data = None
        if payload is not None:
            data = json.dumps(payload).encode("utf-8")
            headers["Content-Type"] = "application/json"
        request = Request(url, data=data, headers=headers, method=method)
        try:
            with urlopen(request, timeout=15) as response:
                status, body = response.status, response.read()
        except HTTPError as error:
            with error:
                status, body = error.code, error.read()
        except (URLError, TimeoutError):
            raise AssertionError(f"{method} {path}: connection or TLS request failed.") from None

        try:
            result = json.loads(body)
        except (ValueError, UnicodeDecodeError):
            raise AssertionError(f"{method} {path}: HTTP {status}, expected JSON.") from None
        if not isinstance(result, dict):
            raise AssertionError(f"{method} {path}: expected a JSON object.")
        return status, result
