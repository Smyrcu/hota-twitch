#!/usr/bin/env bash
# Idempotent server bootstrap for hota-twitch. Run as root on the target host,
# from within a checkout of this directory (it reads env.example,
# hota-twitch.service and Caddyfile from its own location).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "${WORKDIR}"' EXIT

HOTA_USER=hota
DEPLOY_USER=deploy
RELEASES_ROOT=/opt/hota-twitch
DATA_DIR=/var/lib/hota-twitch
CONF_DIR=/etc/hota-twitch
ENV_FILE="${CONF_DIR}/env"
UNIT_FILE=/etc/systemd/system/hota-twitch.service
CADDYFILE=/etc/caddy/Caddyfile
SUDOERS_FILE=/etc/sudoers.d/deploy-hota-twitch
SSHD_DROPIN=/etc/ssh/sshd_config.d/60-hota-twitch.conf
AUTO_UPGRADES_FILE=/etc/apt/apt.conf.d/20auto-upgrades
CADDY_KEYRING=/usr/share/keyrings/caddy-stable-archive-keyring.gpg
CADDY_APT_LIST=/etc/apt/sources.list.d/caddy-stable.list

log() { printf '==> %s\n' "$*"; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

if [[ ${EUID} -ne 0 ]]; then
  die "bootstrap.sh must run as root"
fi

# Installs src at dest with the given mode/owner unless dest already has that
# exact content. Returns 1 (no error) when nothing changed, so callers can do
# `if install_file_if_changed ...; then <react to the change>; fi`. Any real
# failure calls die() directly rather than relying on a propagated exit code,
# because bash suspends -e inside a function invoked as an if/while/||/&&
# condition, which is exactly how this helper is normally called.
install_file_if_changed() {
  local src=$1 dest=$2 mode=$3 owner=$4
  if [[ -f "${dest}" ]] && cmp -s "${src}" "${dest}"; then
    log "unchanged: ${dest}"
    return 1
  fi
  install -o "${owner%%:*}" -g "${owner##*:}" -m "${mode}" "${src}" "${dest}" \
    || die "failed to install ${dest}"
  log "wrote: ${dest}"
  return 0
}

configure_users() {
  log "users"
  if ! id "${HOTA_USER}" >/dev/null 2>&1; then
    useradd --system --home-dir "${DATA_DIR}" --no-create-home \
      --shell /usr/sbin/nologin "${HOTA_USER}" || die "failed to create ${HOTA_USER}"
    log "created system user ${HOTA_USER}"
  else
    log "user ${HOTA_USER} already exists"
  fi

  if ! id "${DEPLOY_USER}" >/dev/null 2>&1; then
    useradd --create-home --shell /bin/bash "${DEPLOY_USER}" \
      || die "failed to create ${DEPLOY_USER}"
    log "created user ${DEPLOY_USER}"
  else
    log "user ${DEPLOY_USER} already exists"
  fi

  local deploy_home
  deploy_home=$(getent passwd "${DEPLOY_USER}" | cut -d: -f6)
  install -d -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" -m 700 "${deploy_home}/.ssh"
  if [[ ! -f "${deploy_home}/.ssh/authorized_keys" ]]; then
    install -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" -m 600 /dev/null \
      "${deploy_home}/.ssh/authorized_keys"
    log "created empty authorized_keys for ${DEPLOY_USER}"
  else
    log "authorized_keys for ${DEPLOY_USER} already present, left untouched"
  fi
}

configure_sudo_rule() {
  log "sudo rule"
  local tmp
  tmp=$(mktemp -p "${WORKDIR}")
  cat >"${tmp}" <<'EOF'
Cmnd_Alias HOTA_TWITCH_CTL = /usr/bin/systemctl restart hota-twitch, /usr/bin/systemctl status hota-twitch
deploy ALL=(root) NOPASSWD: HOTA_TWITCH_CTL
EOF
  visudo -cf "${tmp}" >/dev/null || die "generated sudoers rule failed validation"
  install_file_if_changed "${tmp}" "${SUDOERS_FILE}" 440 root:root || true
}

configure_directory_layout() {
  log "directory layout"
  install -d -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" -m 755 "${RELEASES_ROOT}"
  install -d -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" -m 755 "${RELEASES_ROOT}/releases"
  if [[ ! -e "${RELEASES_ROOT}/current" ]]; then
    install -d -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" -m 755 "${RELEASES_ROOT}/current"
    log "created placeholder ${RELEASES_ROOT}/current (first deploy replaces it with a symlink)"
  else
    log "${RELEASES_ROOT}/current already exists"
  fi
  install -d -o "${HOTA_USER}" -g "${HOTA_USER}" -m 750 "${DATA_DIR}"
  install -d -o root -g "${HOTA_USER}" -m 750 "${CONF_DIR}"
}

configure_env_file() {
  log "environment file"
  if [[ ! -f "${ENV_FILE}" ]]; then
    install -o "${HOTA_USER}" -g "${HOTA_USER}" -m 600 \
      "${SCRIPT_DIR}/env.example" "${ENV_FILE}" || die "failed to seed ${ENV_FILE}"
    log "created ${ENV_FILE} from env.example (placeholder values, edit before first deploy)"
  else
    log "${ENV_FILE} already exists, left untouched"
  fi
}

configure_systemd_unit() {
  log "systemd unit"
  if install_file_if_changed "${SCRIPT_DIR}/hota-twitch.service" "${UNIT_FILE}" 644 root:root; then
    systemctl daemon-reload || die "systemctl daemon-reload failed"
  fi
  systemctl enable hota-twitch.service >/dev/null || die "failed to enable hota-twitch.service"
}

install_caddy_package() {
  log "caddy package"
  if [[ ! -s "${CADDY_KEYRING}" ]]; then
    local tmp
    tmp=$(mktemp -p "${WORKDIR}")
    curl -fsSL 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o "${tmp}" \
      || die "failed to fetch/decode the caddy apt keyring"
    install -o root -g root -m 644 "${tmp}" "${CADDY_KEYRING}"
    log "installed caddy apt keyring"
  fi
  if [[ ! -s "${CADDY_APT_LIST}" ]]; then
    local tmp
    tmp=$(mktemp -p "${WORKDIR}")
    curl -fsSL 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' -o "${tmp}" \
      || die "failed to fetch the caddy apt source list"
    install -o root -g root -m 644 "${tmp}" "${CADDY_APT_LIST}"
    log "added caddy apt repository"
  fi
  if ! dpkg -s caddy >/dev/null 2>&1; then
    apt-get update -qq || die "apt-get update failed"
    apt-get install -y -qq caddy || die "failed to install caddy"
    log "installed caddy"
  else
    log "caddy already installed"
  fi
}

configure_caddy_site() {
  caddy validate --config "${SCRIPT_DIR}/Caddyfile" --adapter caddyfile \
    || die "Caddyfile in ${SCRIPT_DIR} failed validation, refusing to install it"
  if install_file_if_changed "${SCRIPT_DIR}/Caddyfile" "${CADDYFILE}" 644 root:root; then
    systemctl reload caddy 2>/dev/null || systemctl restart caddy || die "failed to (re)start caddy"
  fi
  systemctl enable caddy.service >/dev/null || die "failed to enable caddy.service"
}

configure_firewall() {
  log "firewall"
  ufw default deny incoming >/dev/null
  ufw default allow outgoing >/dev/null
  ufw allow OpenSSH >/dev/null
  ufw allow 80/tcp >/dev/null
  ufw allow 443/tcp >/dev/null
  ufw --force enable >/dev/null
}

configure_unattended_upgrades() {
  log "unattended upgrades"
  if ! dpkg -s unattended-upgrades >/dev/null 2>&1; then
    apt-get install -y -qq unattended-upgrades || die "failed to install unattended-upgrades"
  fi
  local tmp
  tmp=$(mktemp -p "${WORKDIR}")
  cat >"${tmp}" <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF
  install_file_if_changed "${tmp}" "${AUTO_UPGRADES_FILE}" 644 root:root || true
  systemctl enable --now unattended-upgrades.service >/dev/null \
    || die "failed to enable unattended-upgrades.service"
}

configure_sshd() {
  log "sshd"
  if [[ ! -s /root/.ssh/authorized_keys ]]; then
    log "WARNING: /root/.ssh/authorized_keys is missing or empty; leaving password" \
      "authentication as-is to avoid an unrecoverable lockout. Add a root key and" \
      "re-run bootstrap.sh once it works."
    return
  fi

  local tmp
  tmp=$(mktemp -p "${WORKDIR}")
  printf 'PasswordAuthentication no\n' >"${tmp}"
  if [[ -f "${SSHD_DROPIN}" ]] && cmp -s "${tmp}" "${SSHD_DROPIN}"; then
    log "unchanged: ${SSHD_DROPIN}"
    return
  fi

  local backup=""
  if [[ -f "${SSHD_DROPIN}" ]]; then
    backup=$(mktemp -p "${WORKDIR}")
    cp "${SSHD_DROPIN}" "${backup}"
  fi
  install -o root -g root -m 644 "${tmp}" "${SSHD_DROPIN}" || die "failed to install ${SSHD_DROPIN}"
  if ! sshd -t; then
    if [[ -n "${backup}" ]]; then
      mv "${backup}" "${SSHD_DROPIN}"
    else
      rm -f "${SSHD_DROPIN}"
    fi
    die "sshd config validation failed, rolled back ${SSHD_DROPIN}"
  fi
  systemctl reload ssh || die "failed to reload ssh"
  log "wrote: ${SSHD_DROPIN} and reloaded ssh"
}

configure_users
configure_sudo_rule
configure_directory_layout
configure_env_file
configure_systemd_unit
install_caddy_package
configure_caddy_site
configure_firewall
configure_unattended_upgrades
configure_sshd

log "bootstrap complete"
