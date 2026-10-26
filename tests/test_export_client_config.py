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
             patch.object(exporter, "check_websocket") as websocket, \
             contextlib.redirect_stdout(io.StringIO()), \
             contextlib.redirect_stderr(io.StringIO()):
            exporter.main()
        return requests, websocket

    def test_https_export_checks_the_game_websocket(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            requests, websocket = self.run_export(
                ["--api-url", "https://game.example:8443/", "--output", str(output)],
                [{"room_mode": "static", "static_world_endpoint": "wss://game.example:8443/game/30080"}],
            )
            requests.assert_called_once_with("https://game.example:8443/match/diagnostics")
            websocket.assert_called_once_with("wss://game.example:8443/game/30080")
            self.assertEqual(json.loads(output.read_text())["apiBaseUrl"], "https://game.example:8443")

    def test_loopback_room_does_not_create_config(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            with self.assertRaises(SystemExit):
                self.run_export(
                    ["--api-url", "http://game.example:8000", "--output", str(output)],
                    [{"room_mode": "static", "static_world_endpoint": "wss://127.0.0.1:8443/game/30080"}],
                )
            self.assertFalse(output.exists())

    def test_default_export_uses_the_project_config(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            (Path(directory) / "network-config.json").write_text('{"apiBaseUrl":"https://game.example:8443"}')
            with patch.object(exporter, "PROJECT_ROOT", Path(directory)):
                requests, websocket = self.run_export(["--output", str(output)], [
                    {"room_mode": "k8s", "room_endpoint_example": "wss://game.example:8443/game/30081"},
                ])
            requests.assert_called_once_with("https://game.example:8443/match/diagnostics")
            websocket.assert_called_once_with("wss://game.example:8443/game/30081")
            self.assertEqual(json.loads(output.read_text())["apiBaseUrl"], "https://game.example:8443")

    def test_failed_game_websocket_preserves_existing_config(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "network-config.json"
            output.write_text("existing config")
            with patch("sys.argv", ["export_client_config.py", "--api-url", "https://game.example:8443", "--output", str(output)]), \
                 patch.object(exporter, "read_json", return_value={"room_mode": "static", "static_world_endpoint": "wss://game.example:8443/game/30080"}), \
                 patch.object(exporter, "check_websocket", side_effect=OSError("TLS handshake failed")), \
                 contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                exporter.main()
            self.assertEqual(output.read_text(), "existing config")

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
