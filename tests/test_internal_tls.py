import contextlib
from datetime import datetime, timedelta, timezone
import importlib.util
import io
from pathlib import Path
import socket
import ssl
import sys
import tempfile
import threading
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from cryptography import x509
from cryptography.x509.oid import ExtendedKeyUsageOID
import yaml

from backend.tls import setup_tls as tls
import compose


ROOT = Path(__file__).resolve().parents[1]


class InternalCertificateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        cls.directory = Path(cls.temporary.name)
        cls.now = datetime.now(timezone.utc)
        cls.initial = tls.ensure_certificates(cls.directory, now=cls.now)

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def test_ca_is_a_restricted_signing_authority(self):
        ca = x509.load_pem_x509_certificate((self.directory / "ca.crt").read_bytes())
        constraints = ca.extensions.get_extension_for_class(x509.BasicConstraints).value
        self.assertTrue(constraints.ca)
        self.assertEqual(constraints.path_length, 0)
        self.assertTrue(ca.extensions.get_extension_for_class(x509.KeyUsage).value.key_cert_sign)

    def test_service_identity_key_usage_and_size(self):
        self.assertEqual(set(self.initial.renewed), set(tls.SERVICE_NAMES))
        ca = x509.load_pem_x509_certificate((self.directory / "ca.crt").read_bytes())
        for name, hostname in tls.SERVICE_NAMES.items():
            with self.subTest(service=name):
                cert, key = tls._load_pair(self.directory / name / "tls.crt", self.directory / name / "tls.key")
                tls._verify_signature(cert, ca)
                san = cert.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
                self.assertEqual(san.get_values_for_type(x509.DNSName), [hostname])
                self.assertEqual(list(cert.extensions.get_extension_for_class(x509.ExtendedKeyUsage).value), [ExtendedKeyUsageOID.SERVER_AUTH])
                self.assertFalse(cert.extensions.get_extension_for_class(x509.BasicConstraints).value.ca)
                self.assertGreaterEqual(key.key_size, 2048)
                self.assertLess(len((self.directory / name / "tls.crt").read_bytes()), 16383)
                self.assertLess(len((self.directory / name / "tls.key").read_bytes()), 16383)

    def test_idempotent_setup_does_not_rotate_keys(self):
        before = {p.relative_to(self.directory): p.read_bytes() for p in self.directory.rglob("*") if p.is_file()}
        result = tls.ensure_certificates(self.directory, now=self.now + timedelta(days=1))
        self.assertEqual(result.renewed, ())
        self.assertEqual(before, {p.relative_to(self.directory): p.read_bytes() for p in self.directory.rglob("*") if p.is_file()})

    def test_check_only_validates_without_writes(self):
        result = tls.ensure_certificates(self.directory, now=self.now, check_only=True)
        self.assertEqual(result.renewed, ())
        with tempfile.TemporaryDirectory() as empty:
            with self.assertRaises(tls.ProvisionError):
                tls.ensure_certificates(empty, check_only=True)
            self.assertEqual(list(Path(empty).iterdir()), [])

    def test_renewal_keeps_ca_but_refreshes_leaf_keys(self):
        with tempfile.TemporaryDirectory() as target:
            folder = Path(target)
            for source in self.directory.rglob("*"):
                if source.is_file():
                    dest = folder / source.relative_to(self.directory)
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    dest.write_bytes(source.read_bytes())
            authority = (folder / "ca.key").read_bytes()
            original_leaf = (folder / "lobby/tls.key").read_bytes()
            future = self.now + timedelta(days=65)
            with self.assertRaises(tls.ProvisionError):
                tls.ensure_certificates(folder, now=future, check_only=True)
            result = tls.ensure_certificates(folder, now=future)
            self.assertEqual(set(result.renewed), set(tls.SERVICE_NAMES))
            self.assertEqual(authority, (folder / "ca.key").read_bytes())
            self.assertNotEqual(original_leaf, (folder / "lobby/tls.key").read_bytes())

    def test_partial_ca_is_not_silently_replaced(self):
        with tempfile.TemporaryDirectory() as target:
            folder = Path(target)
            (folder / "ca.crt").write_bytes((self.directory / "ca.crt").read_bytes())
            with self.assertRaises(tls.ProvisionError):
                tls.ensure_certificates(folder)
            self.assertFalse((folder / "ca.key").exists())

    def test_orphaned_leaf_does_not_create_a_new_ca(self):
        with tempfile.TemporaryDirectory() as target:
            folder = Path(target)
            (folder / "game").mkdir()
            (folder / "game/tls.crt").write_bytes((self.directory / "game/tls.crt").read_bytes())
            with self.assertRaises(tls.ProvisionError):
                tls.ensure_certificates(folder)
            self.assertFalse((folder / "ca.key").exists())

    def _handshake(self, context, hostname):
        """Real local TLS handshake, including certificate/hostname verification."""
        server_context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        server_context.minimum_version = ssl.TLSVersion.TLSv1_2
        server_context.load_cert_chain(self.directory / "lobby/tls.crt", self.directory / "lobby/tls.key")
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            listener.listen(1)
            listener.settimeout(5)
            errors = []

            def accept():
                try:
                    connection, _ = listener.accept()
                    with connection:
                        with server_context.wrap_socket(connection, server_side=True) as encrypted:
                            if encrypted.recv(1) == b"x":
                                encrypted.sendall(b"ok")
                except ssl.SSLError:
                    pass  # Expected when the client rejects an untrusted certificate.
                except Exception as error:
                    errors.append(error)

            worker = threading.Thread(target=accept, daemon=True)
            worker.start()
            try:
                with socket.create_connection(listener.getsockname(), timeout=5) as connection:
                    with context.wrap_socket(connection, server_hostname=hostname) as encrypted:
                        encrypted.sendall(b"x")
                        self.assertEqual(encrypted.recv(2), b"ok")
                        self.assertIsNotNone(encrypted.cipher())
            finally:
                worker.join(timeout=6)
                self.assertFalse(worker.is_alive(), "TLS worker did not exit")
                self.assertEqual(errors, [])

    def test_real_tls_accepts_the_named_internal_service(self):
        context = ssl.create_default_context(cafile=str(self.directory / "ca.crt"))
        self._handshake(context, "avian-lobby")

    def test_real_tls_rejects_wrong_hostname(self):
        context = ssl.create_default_context(cafile=str(self.directory / "ca.crt"))
        with self.assertRaises(ssl.SSLCertVerificationError):
            self._handshake(context, "an-impostor-service")

    def test_real_tls_rejects_unknown_ca(self):
        context = ssl.create_default_context()
        with self.assertRaises(ssl.SSLCertVerificationError):
            self._handshake(context, "avian-lobby")


class DeploymentConfigurationTests(unittest.TestCase):
    def test_caddy_verifies_all_upstreams_and_game_identity(self):
        config = (ROOT / "backend/Caddyfile").read_text()
        self.assertIn("reverse_proxy https://avian-lobby:8000", config)
        self.assertIn("reverse_proxy https://avian-matchmaker:8080", config)
        self.assertEqual(config.count("tls_trust_pool file /etc/avian-tls/ca.crt"), 3)
        self.assertIn("tls_server_name avian-game-room", config)
        self.assertNotIn("tls_insecure_skip_verify", config)
        self.assertIn("versions 1.1\n                tls", config)

    def test_apis_listen_with_certificates_and_healthchecks_validate_tls(self):
        services = yaml.safe_load((ROOT / "backend/docker-compose.yaml").read_text())["services"]
        for service in ("lobby", "matchmaker"):
            dockerfile = (ROOT / f"backend/{service}/Dockerfile").read_text()
            self.assertIn('"--ssl-certfile", "/run/avian-tls/tls.crt"', dockerfile)
            self.assertIn('"--ssl-keyfile", "/run/avian-tls/tls.key"', dockerfile)
            spec = services[f"avian-{service}"]
            check = spec["healthcheck"]["test"][-1]
            self.assertIn(f"https://avian-{service}:", check)
            self.assertIn("ssl.create_default_context(cafile=", check)
            mounts = [m for m in spec["volumes"] if isinstance(m, dict)]
            self.assertEqual(mounts[0]["target"], "/run/avian-tls")
            self.assertTrue(mounts[0]["read_only"])
            self.assertFalse(mounts[0]["bind"]["create_host_path"])
        self.assertTrue(services["avian-matchmaker"]["ports"][0].startswith("127.0.0.1:"))

    def test_static_server_receives_a_secret_not_baked_in_keys(self):
        deployment = list(yaml.safe_load_all((ROOT / "backend/avian-cluster.yaml").read_text()))[0]
        pod = deployment["spec"]["template"]["spec"]
        container = pod["containers"][0]
        env = {v["name"]: v["value"] for v in container["env"]}
        self.assertEqual(env["GAME_TLS_KEY_FILE"], "/run/avian-tls/tls.key")
        self.assertEqual(env["GAME_TLS_CERT_FILE"], "/run/avian-tls/tls.crt")
        self.assertEqual(pod["volumes"][0]["secret"]["secretName"], "avian-game-tls")
        self.assertEqual(pod["volumes"][0]["secret"]["defaultMode"], 0o400)
        self.assertTrue(container["volumeMounts"][0]["readOnly"])

    def test_dynamic_websocket_jobs_mount_tls_but_udp_jobs_do_not(self):
        source = ROOT / "backend/matchmaker/k8s_rooms.py"
        spec = importlib.util.spec_from_file_location("avian_tls_k8s_test", source)
        module = importlib.util.module_from_spec(spec)
        with patch.dict(sys.modules, {spec.name: module}):
            spec.loader.exec_module(module)
        for mode in ("websocket", "udp"):
            with self.subTest(mode=mode), patch.object(module.client, "BatchV1Api") as api:
                settings = module.K8sRoomSettings("default", "image", 7777, mode)
                module.create_job("test-room", "room-1", 123, settings, {"avian-room-id": "room-1"})
                pod = api.return_value.create_namespaced_job.call_args.kwargs["body"].spec.template.spec
                container = pod.containers[0]
                env = {item.name: item.value for item in container.env}
                if mode == "websocket":
                    self.assertEqual(env["GAME_TLS_CERT_FILE"], "/run/avian-tls/tls.crt")
                    self.assertEqual(pod.volumes[0].secret.secret_name, "avian-game-tls")
                    self.assertTrue(container.volume_mounts[0].read_only)
                else:
                    self.assertNotIn("GAME_TLS_CERT_FILE", env)
                    self.assertEqual(pod.volumes, [])

    def test_kubectl_secret_is_piped_without_logging_private_data(self):
        stream = io.StringIO()
        with patch.object(compose.subprocess, "run", return_value=SimpleNamespace(stdout="PRIVATE_SECRET_YAML")) as run, contextlib.redirect_stdout(stream):
            compose.install_game_tls_secret(Path("local-certs"), "default")
        self.assertNotIn("PRIVATE_SECRET_YAML", stream.getvalue())
        self.assertTrue(run.call_args_list[0].kwargs["capture_output"])
        self.assertEqual(run.call_args_list[1].kwargs["input"], "PRIVATE_SECRET_YAML")

    def test_namespaces_are_validated_before_kubectl(self):
        with patch.dict("os.environ", {"ROOM_NAMESPACE": "a-valid-room"}):
            self.assertEqual(compose.room_namespace(), "a-valid-room")
        with patch.dict("os.environ", {"ROOM_NAMESPACE": "invalid; command"}):
            with self.assertRaises(ValueError):
                compose.room_namespace()


if __name__ == "__main__":
    unittest.main()
