#!/bin/bash
set -euxo pipefail
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y docker.io ca-certificates
systemctl enable --now docker
mkdir -p /opt/zuzel
