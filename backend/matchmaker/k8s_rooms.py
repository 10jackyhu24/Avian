import os
from dataclasses import dataclass
from urllib.parse import urlparse, urlunparse

from kubernetes import client, config


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


def load_kubernetes_client():
    if os.getenv("KUBERNETES_SERVICE_HOST"):
        config.load_incluster_config()
    else:
        config.load_kube_config(config_file=os.getenv("KUBECONFIG"))

    api_host_override = os.getenv("KUBERNETES_API_HOST", "").strip()
    if api_host_override:
        configuration = client.Configuration.get_default_copy()
        parsed = urlparse(configuration.host)
        configuration.host = urlunparse((
            parsed.scheme or "https",
            api_host_override,
            parsed.path,
            parsed.params,
            parsed.query,
            parsed.fragment,
        ))
        client.Configuration.set_default(configuration)


def create_room_server(room_id, room_seed, node_port, settings):
    load_kubernetes_client()

    server_name = build_resource_name("avian-room", room_id)
    labels = {
        "app": "avian-game-room",
        "avian-managed-by": "matchmaker",
        "avian-room-id": room_id,
    }

    create_service(server_name, node_port, settings, labels)
    create_job(server_name, room_id, room_seed, settings, labels)

    return K8sRoomServer(
        server_name=server_name,
        service_name=server_name,
        node_port=node_port,
    )


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
                    name="udp-game",
                    protocol="UDP",
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
                protocol="UDP",
            )
        ],
        env=[
            client.V1EnvVar(name="ROOM_ID", value=room_id),
            client.V1EnvVar(name="MAP_SEED", value=str(room_seed)),
            client.V1EnvVar(name="GAME_PORT", value=str(settings.game_port)),
        ],
    )

    pod_template = client.V1PodTemplateSpec(
        metadata=client.V1ObjectMeta(labels=labels),
        spec=client.V1PodSpec(
            restart_policy="Never",
            containers=[container],
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
