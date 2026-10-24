"""Provision Avian's private backend PKI without changing any OS trust store.

Only the root certificate is shared with Caddy. Each service receives only its
own key/certificate and the public CA. Output is git-ignored backend/.tls.
Run deployments regularly: leaves last 90 days and renew with <=30 days left.
Restart TLS services / room Pods after renewal; live contexts retain old certs.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import os
from pathlib import Path
import tempfile

from cryptography import x509
from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID


DEFAULT_DIRECTORY = Path(__file__).resolve().parents[1] / ".tls"
SERVICE_NAMES = {"lobby": "avian-lobby", "matchmaker": "avian-matchmaker", "game": "avian-game-room"}
LEAF_DAYS = 90
RENEW_DAYS = 30


class ProvisionError(ValueError):
    pass


@dataclass(frozen=True)
class ProvisionResult:
    directory: Path
    renewed: tuple[str, ...]


def _expiry(cert):
    if hasattr(cert, "not_valid_after_utc"):
        return cert.not_valid_after_utc
    return cert.not_valid_after.replace(tzinfo=timezone.utc)


def _starts(cert):
    if hasattr(cert, "not_valid_before_utc"):
        return cert.not_valid_before_utc
    return cert.not_valid_before.replace(tzinfo=timezone.utc)


def _write(path, content, private=False):
    """Write generated PEMs atomically, private before first byte (POSIX)."""
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=".avian-tls-", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as output:
            os.chmod(temporary, 0o600 if private else 0o644)
            output.write(content)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def _key_bytes(key):
    return key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8,
                             serialization.NoEncryption())


def _cert_bytes(cert):
    return cert.public_bytes(serialization.Encoding.PEM)


def _public_bytes(key):
    return key.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)


def _load_pair(cert_path, key_path):
    if cert_path.exists() != key_path.exists():
        raise ProvisionError(f"Incomplete certificate/key pair in {cert_path.parent}; restore its matching pair before deployment.")
    try:
        cert = x509.load_pem_x509_certificate(cert_path.read_bytes())
        key = serialization.load_pem_private_key(key_path.read_bytes(), password=None)
        if not isinstance(key, rsa.RSAPrivateKey) or key.key_size < 2048:
            raise ProvisionError("Expected an RSA key of at least 2048 bits.")
        if _public_bytes(cert.public_key()) != _public_bytes(key.public_key()):
            raise ProvisionError("Certificate does not match its private key.")
        return cert, key
    except (ValueError, OSError) as error:
        raise ProvisionError(f"Invalid TLS material in {cert_path.parent}: {error}") from error


def _verify_signature(cert, issuer):
    if cert.issuer != issuer.subject:
        raise ProvisionError("Certificate issuer does not match the internal CA.")
    try:
        issuer.public_key().verify(cert.signature, cert.tbs_certificate_bytes,
                                   padding.PKCS1v15(), cert.signature_hash_algorithm)
    except InvalidSignature as error:
        raise ProvisionError("Certificate signature does not match the internal CA.") from error


def _authority(directory, now, check_only):
    cert_path, key_path = directory / "ca.crt", directory / "ca.key"
    if cert_path.exists() or key_path.exists():
        cert, key = _load_pair(cert_path, key_path)
        _verify_signature(cert, cert)
        try:
            constraints = cert.extensions.get_extension_for_class(x509.BasicConstraints).value
            usage = cert.extensions.get_extension_for_class(x509.KeyUsage).value
        except x509.ExtensionNotFound as error:
            raise ProvisionError("Internal CA is missing required constraints.") from error
        if not constraints.ca or not usage.key_cert_sign or _starts(cert) > now:
            raise ProvisionError("Invalid internal CA constraints or validity period.")
        if _expiry(cert) <= now + timedelta(days=LEAF_DAYS):
            raise ProvisionError("Internal CA is near expiry. Plan an explicit CA/trust-store rotation; it will not be silently replaced.")
        return cert, key
    if check_only:
        raise ProvisionError("Internal CA is missing. Run backend/tls/setup_tls.py first.")
    # Refuse to replace the authority if only service material survived a partial restore.
    if any(directory.glob("*/tls.*")):
        raise ProvisionError("CA is missing but service credentials exist. Restore ca.crt AND ca.key; do not silently create a new CA.")
    key = rsa.generate_private_key(public_exponent=65537, key_size=3072)
    subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "Avian Internal Backend CA")])
    cert = (x509.CertificateBuilder().subject_name(subject).issuer_name(subject).public_key(key.public_key())
            .serial_number(x509.random_serial_number()).not_valid_before(now - timedelta(minutes=5))
            .not_valid_after(now + timedelta(days=3650))
            .add_extension(x509.BasicConstraints(ca=True, path_length=0), critical=True)
            .add_extension(x509.KeyUsage(digital_signature=False, content_commitment=False,
                           key_encipherment=False, data_encipherment=False, key_agreement=False,
                           key_cert_sign=True, crl_sign=True, encipher_only=False, decipher_only=False), critical=True)
            .add_extension(x509.SubjectKeyIdentifier.from_public_key(key.public_key()), critical=False)
            .sign(key, hashes.SHA256()))
    _write(key_path, _key_bytes(key), private=True)
    _write(cert_path, _cert_bytes(cert))
    return cert, key


def _issue(hostname, authority, authority_key, now):
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    cert = (x509.CertificateBuilder()
            .subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, hostname)]))
            .issuer_name(authority.subject).public_key(key.public_key()).serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=5)).not_valid_after(now + timedelta(days=LEAF_DAYS))
            .add_extension(x509.SubjectAlternativeName([x509.DNSName(hostname)]), critical=False)
            .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
            .add_extension(x509.ExtendedKeyUsage([ExtendedKeyUsageOID.SERVER_AUTH]), critical=False)
            .add_extension(x509.KeyUsage(digital_signature=True, content_commitment=False,
                           key_encipherment=True, data_encipherment=False, key_agreement=False,
                           key_cert_sign=False, crl_sign=False, encipher_only=False, decipher_only=False), critical=True)
            .add_extension(x509.SubjectKeyIdentifier.from_public_key(key.public_key()), critical=False)
            .add_extension(x509.AuthorityKeyIdentifier.from_issuer_public_key(authority.public_key()), critical=False)
            .sign(authority_key, hashes.SHA256()))
    return cert, key


def ensure_certificates(directory=DEFAULT_DIRECTORY, *, now=None, check_only=False):
    directory = Path(directory).resolve()
    now = now or datetime.now(timezone.utc)
    authority, authority_key = _authority(directory, now, check_only)
    renewed = []
    for service, hostname in SERVICE_NAMES.items():
        folder = directory / service
        cert_path, key_path = folder / "tls.crt", folder / "tls.key"
        needs_renewal = not cert_path.exists() and not key_path.exists()
        if not needs_renewal:
            cert, key = _load_pair(cert_path, key_path)
            _verify_signature(cert, authority)
            try:
                names = cert.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
                usage = cert.extensions.get_extension_for_class(x509.ExtendedKeyUsage).value
                constraints = cert.extensions.get_extension_for_class(x509.BasicConstraints).value
            except x509.ExtensionNotFound as error:
                raise ProvisionError(f"Invalid certificate extensions for {service}.") from error
            if hostname not in names.get_values_for_type(x509.DNSName) or ExtendedKeyUsageOID.SERVER_AUTH not in usage or constraints.ca:
                raise ProvisionError(f"Invalid server identity or usage for {service}.")
            if _starts(cert) > now:
                raise ProvisionError(f"Certificate for {service} is not valid yet.")
            needs_renewal = _expiry(cert) <= now + timedelta(days=RENEW_DAYS)
        if needs_renewal:
            if check_only:
                raise ProvisionError(f"Certificate for {service} is missing or needs renewal.")
            cert, key = _issue(hostname, authority, authority_key, now)
            _write(key_path, _key_bytes(key), private=True)
            _write(cert_path, _cert_bytes(cert))
            renewed.append(service)
        ca_copy = folder / "ca.crt"
        if not ca_copy.exists() or ca_copy.read_bytes() != _cert_bytes(authority):
            if check_only:
                raise ProvisionError(f"Public CA copy for {service} is missing or incorrect.")
            _write(ca_copy, _cert_bytes(authority))
    return ProvisionResult(directory, tuple(renewed))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, default=DEFAULT_DIRECTORY)
    parser.add_argument("--check", action="store_true", help="Validate existing certificates without writing files")
    args = parser.parse_args()
    try:
        result = ensure_certificates(args.directory, check_only=args.check)
    except (ProvisionError, OSError) as error:
        parser.exit(1, f"TLS provisioning failed: {error}\n")
    print(f"Internal TLS ready: {result.directory}; renewed: {', '.join(result.renewed) or 'none'}")
    print("Keys are local and git-ignored. Caddy trusts only ca.crt; no OS trust store was modified.")


if __name__ == "__main__":
    main()
