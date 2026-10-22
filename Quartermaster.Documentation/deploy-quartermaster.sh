#!/bin/bash
# Usage: sudo ./deploy-quartermaster.sh <archive.tar.gz> <sha256> [--backup]
# --backup dumps the database first; use it for every release that contains a migration.
set -euo pipefail

APP_DIR=/var/www/quartermaster
NEW_DIR=/var/www/quartermaster-new
SERVICE=quartermaster

die() {
    echo "Error: $*" >&2
    exit 1
}

[ "$(id -u)" -eq 0 ] || die "run this script with sudo"
[ $# -ge 2 ] || die "usage: sudo $0 <archive.tar.gz> <sha256> [--backup]"

ARCHIVE=$1
EXPECTED=${2,,}
BACKUP=${3:-}

[ -f "$ARCHIVE" ] || die "archive $ARCHIVE not found"
[ -z "$BACKUP" ] || [ "$BACKUP" = "--backup" ] || die "unknown option $BACKUP"
[ -f "$APP_DIR/appsettings.Production.json" ] || die "$APP_DIR/appsettings.Production.json not found"
[ ! -e "$NEW_DIR" ] || die "$NEW_DIR already exists, remove it first"

echo "Checking checksum..."
ACTUAL=$(sha256sum "$ARCHIVE" | cut -d' ' -f1)
[ "$ACTUAL" = "$EXPECTED" ] || die "checksum mismatch: expected $EXPECTED, got $ACTUAL"

CURRENT=$(find "$APP_DIR/Changelog" -maxdepth 1 -name 'V*.md' -printf '%f\n' 2>/dev/null | sed -n 's/^V\([0-9.]*\)\.md$/\1/p' | sort -V | tail -1)
OLD_DIR="/var/www/quartermaster-v${CURRENT:-unknown}"
[ ! -e "$OLD_DIR" ] || OLD_DIR="$OLD_DIR-$(date +%Y%m%d-%H%M%S)"

if [ "$BACKUP" = "--backup" ]; then
    OWNER=${SUDO_USER:-root}
    OWNER_HOME=$(getent passwd "$OWNER" | cut -d: -f6)
    DUMP="$OWNER_HOME/quartermaster-$(date +%F-%H%M)-pre-deploy.sql.gz"
    echo "Dumping the database to $DUMP..."
    mysqldump --single-transaction quartermaster | gzip > "$DUMP"
    chown "$OWNER" "$DUMP"
fi

echo "Unpacking into $NEW_DIR..."
mkdir -p "$NEW_DIR"
tar -xzf "$ARCHIVE" -C "$NEW_DIR"
if [ ! -f "$NEW_DIR/Quartermaster.Server" ]; then
    rm -rf "$NEW_DIR"
    die "Quartermaster.Server is not at the top level of the archive"
fi
chown -R root:root "$NEW_DIR"
chmod +x "$NEW_DIR/Quartermaster.Server"

echo "Stopping $SERVICE..."
systemctl stop "$SERVICE"
trap 'echo "Error: deployment stopped halfway; $SERVICE is not running. Check $APP_DIR, $NEW_DIR and $OLD_DIR." >&2' ERR

cp -a "$APP_DIR/appsettings.Production.json" "$NEW_DIR/"
cp -a "$APP_DIR/data" "$NEW_DIR/"
mv "$APP_DIR" "$OLD_DIR"
mv "$NEW_DIR" "$APP_DIR"

echo "Starting $SERVICE..."
systemctl start "$SERVICE"
trap - ERR
sleep 10

if ! systemctl is-active --quiet "$SERVICE"; then
    echo "Error: $SERVICE did not stay up. Last log lines:" >&2
    journalctl -u "$SERVICE" -n 30 --no-pager >&2
    echo "Roll back with:" >&2
    echo "  systemctl stop $SERVICE && mv $APP_DIR $APP_DIR-failed && mv $OLD_DIR $APP_DIR && systemctl start $SERVICE" >&2
    [ "$BACKUP" != "--backup" ] || echo "  and restore $DUMP if a migration ran" >&2
    exit 1
fi

echo "Done. $SERVICE is running, the previous version is in $OLD_DIR."
echo "Follow the log with: journalctl -u $SERVICE -f"
