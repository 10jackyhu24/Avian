import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import export_client_config as exporter


class ExportClientConfigTests(unittest.TestCase):
    def run_export(self, arguments, responses):
        with patch("sys.argv", ["export_client_config.py", *arguments]), \
             patch.object(exporter, "read_json", side_effect=responses) as requests, \
             contextlib.redirect_stdout(io.StringIO()), \
             contextlib.redirect_stderr(io.StringIO()):
            exporter.main()
        return requests

    def test_direct_api_does_not_contact_ngrok(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            requests = self.run_export(
                ["--api-url", "http://game.example:8000/", "--output", str(output)],
                [{"room_mode": "static", "static_world_endpoint": "game.example:30080"}],
            )
            requests.assert_called_once_with("http://game.example:8000/match/diagnostics")
            self.assertEqual(json.loads(output.read_text())["apiBaseUrl"], "http://game.example:8000")

    def test_loopback_room_does_not_create_config(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            with self.assertRaises(SystemExit):
                self.run_export(
                    ["--api-url", "http://game.example:8000", "--output", str(output)],
                    [{"room_mode": "static", "static_world_endpoint": "127.0.0.1:30080"}],
                )
            self.assertFalse(output.exists())

    def test_ngrok_export_still_works(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            requests = self.run_export(["--output", str(output)], [
                {"tunnels": [{"proto": "https", "config": {"addr": exporter.AVIAN_UPSTREAM},
                              "public_url": "https://game.ngrok.app"}]},
                {"room_mode": "k8s", "room_endpoint_example": "game.example:30081"},
            ])
            self.assertEqual(requests.call_count, 2)
            self.assertEqual(json.loads(output.read_text())["apiBaseUrl"], "https://game.ngrok.app")

    def test_invalid_direct_url_is_rejected_before_network_access(self):
        for url in ["game.example", "udp://game.example:30080", "http://game.example:99999", "http://user:pass@game.example"]:
            with self.subTest(url=url), patch.object(exporter, "read_json") as request, \
                 patch("sys.argv", ["export_client_config.py", "--api-url", url]), \
                 contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                try:
                    exporter.main()
                finally:
                    request.assert_not_called()


if __name__ == "__main__":
    unittest.main()
