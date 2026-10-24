import os
from dataclasses import dataclass
from urllib.parse import urlparse, urlunparse

from kubernetes import client, config
from kubernetes.client.exceptions import ApiException
from urllib3.exceptions import HTTPError as UrllibHttpError


@dataclass
class K8sRoomServer:
    server_name: str
    service_name: str
    node_port: int


@dataclass
class K8sRoomSettings:
    namespace: str
    image: str
    game_port: int
    transport: str = "websocket"
    tls_secret_name: str = "avian-game-tls"

    @property
    def protocol(self):
        return "TCP" if self.transport == "websocket" else "UDP"


@dataclass
class K8sRoomStatus:
    status: str


class K8sRoomError(Exception):
    pass


def load_kubernetes_client():
    if os.getenv("KUBERNETES_SERVICE_HOST"):
        config.load_incluster_config()
    else:
        config.load_kube_config(config_file=os.getenv("KUBECONFIG"))

    configuration = client.Configuration.get_default_copy()
    api_host_override = os.getenv("KUBERNETES_API_HOST", "").strip()
    if api_host_override:
        parsed = urlparse(configuration.host)
        configuration.host = urlunparse((
            parsed.scheme or "https",
            api_host_override,
            parsed.path,
            parsed.params,
            parsed.query,
            parsed.fragment,
        ))

    if not get_bool_env("KUBERNETES_VERIFY_SSL", True):
        configuration.verify_ssl = False

    client.Configuration.set_default(configuration)


def get_bool_env(name, default_value):
    raw_value = os.getenv(name, "").strip().lower()
    if not raw_value:
        return default_value

    if raw_value in ("1", "true", "yes", "on"):
        return True

    if raw_value in ("0", "false", "no", "off"):
        return False

    raise ValueError(f"{name} must be true or false")


def create_room_server(room_id, room_seed, node_port, settings):
    server_name = None
    created_service = False

    try:
        load_kubernetes_client()

        server_name = build_resource_name("avian-room", room_id)
        labels = {
            "app": "avian-game-room",
            "avian-managed-by": "matchmaker",
            "avian-room-id": room_id,
        }

        create_service(server_name, node_port, settings, labels)
        created_service = True
        create_job(server_name, room_id, room_seed, settings, labels)
    except (ApiException, UrllibHttpError, OSError, ValueError) as exception:
        if created_service and server_name:
            try:
                delete_service(server_name, settings)
            except Exception as cleanup_exception:
                print(
                    f"[Matchmaker] Failed to clean partial k8s service {server_name}: {cleanup_exception}",
                    flush=True,
                )

        raise K8sRoomError(str(exception)) from exception

    return K8sRoomServer(
        server_name=server_name,
        service_name=server_name,
        node_port=node_port,
    )


def read_room_status(server_name, room_id, settings):
    load_kubernetes_client()

    try:
        job = client.BatchV1Api().read_namespaced_job(
            name=server_name,
            namespace=settings.namespace,
        )
    except ApiException as exception:
        if exception.status == 404:
            return K8sRoomStatus(status="missing")

        raise

    job_status = job.status
    if job_status.succeeded and job_status.succeeded > 0:
        return K8sRoomStatus(status="complete")

    if job_status.failed and job_status.failed > 0:
        return K8sRoomStatus(status="failed")

    if has_running_ready_pod(room_id, settings):
        return K8sRoomStatus(status="running")

    return K8sRoomStatus(status="starting")


def delete_room_server(server_name, settings):
    load_kubernetes_client()

    delete_job(server_name, settings)
    delete_service(server_name, settings)


def has_running_ready_pod(room_id, settings):
    pods = client.CoreV1Api().list_namespaced_pod(
        namespace=settings.namespace,
        label_selector=f"avian-room-id={room_id}",
    )

    for pod in pods.items:
        if pod.status.phase != "Running":
            continue

        container_statuses = pod.status.container_statuses or []
        if any(container.ready for container in container_statuses):
            return True

    return False


def delete_job(server_name, settings):
    try:
        client.BatchV1Api().delete_namespaced_job(
            name=server_name,
            namespace=settings.namespace,
            propagation_policy="Background",
        )
    except ApiException as exception:
        if exception.status != 404:
            raise


def delete_service(server_name, settings):
    try:
        client.CoreV1Api().delete_namespaced_service(
            name=server_name,
            namespace=settings.namespace,
        )
    except ApiException as exception:
        if exception.status != 404:
            raise


def create_service(server_name, node_port, settings, labels):
    service = client.V1Service(
        metadata=client.V1ObjectMeta(
            name=server_name,
            labels=labels,
        ),
        spec=client.V1ServiceSpec(
            type="NodePort",
            selector={
                "avian-room-id": labels["avian-room-id"],
            },
            ports=[
                client.V1ServicePort(
                    name="game",
                    protocol=settings.protocol,
                    port=settings.game_port,
                    target_port=settings.game_port,
                    node_port=node_port,
                )
            ],
        ),
    )

    client.CoreV1Api().create_namespaced_service(
        namespace=settings.namespace,
        body=service,
    )


def create_job(server_name, room_id, room_seed, settings, labels):
    container = client.V1Container(
        name="game-room",
        image=settings.image,
        image_pull_policy="IfNotPresent",
        ports=[
            client.V1ContainerPort(
                container_port=settings.game_port,
                protocol=settings.protocol,
            )
        ],
        env=[
            client.V1EnvVar(name="ROOM_ID", value=room_id),
            client.V1EnvVar(name="MAP_SEED", value=str(room_seed)),
            client.V1EnvVar(name="GAME_PORT", value=str(settings.game_port)),
            client.V1EnvVar(name="GAME_TRANSPORT", value=settings.transport),
        ],
    )

    volumes = []
    if settings.transport == "websocket":
        container.env.extend([
            client.V1EnvVar(name="GAME_TLS_CERT_FILE", value="/run/avian-tls/tls.crt"),
            client.V1EnvVar(name="GAME_TLS_KEY_FILE", value="/run/avian-tls/tls.key"),
        ])
        container.volume_mounts = [client.V1VolumeMount(
            name="game-tls", mount_path="/run/avian-tls", read_only=True,
        )]
        volumes = [client.V1Volume(name="game-tls", secret=client.V1SecretVolumeSource(
            secret_name=settings.tls_secret_name, default_mode=0o400,
        ))]

    pod_template = client.V1PodTemplateSpec(
        metadata=client.V1ObjectMeta(labels=labels),
        spec=client.V1PodSpec(
            restart_policy="Never",
            containers=[container],
            volumes=volumes,
        ),
    )

    job = client.V1Job(
        metadata=client.V1ObjectMeta(
            name=server_name,
            labels=labels,
        ),
        spec=client.V1JobSpec(
            backoff_limit=0,
            ttl_seconds_after_finished=120,
            template=pod_template,
        ),
    )

    client.BatchV1Api().create_namespaced_job(
        namespace=settings.namespace,
        body=job,
    )


def build_resource_name(prefix, room_id):
    safe_room_id = "".join(
        character if character.isalnum() or character == "-" else "-"
        for character in room_id.lower()
    ).strip("-")

    name = f"{prefix}-{safe_room_id}"
    return name[:63].strip("-")
