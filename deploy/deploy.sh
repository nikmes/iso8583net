#!/bin/bash
# Deploy ISO8583Service (with WebAPI) to the target server
# Usage: ./deploy.sh <user@server>

set -e

SERVER=${1:?"Usage: $0 <user@server>"}
SERVICE_NAME="iso8583service"
REMOTE_DIR="/home/ecbxuser/linux-x64"
API_PORT="${API_PORT:-5000}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/tools/ISO8583Service/publish/linux-x64"

echo "📦 Publishing for linux-x64..."
dotnet publish "$REPO_ROOT/tools/ISO8583Service/ISO8583Service.csproj" \
    --runtime linux-x64 \
    --self-contained true \
    --configuration Release \
    --output "$PUBLISH_DIR"

echo "📁 Creating target directory..."
ssh "$SERVER" "mkdir -p $REMOTE_DIR/logs"

echo "📤 Uploading..."
rsync -avz --delete \
    --exclude 'logs/' \
    "$PUBLISH_DIR/" \
    "$SERVER:$REMOTE_DIR/"

echo "🔧 Installing systemd service..."
scp "$SCRIPT_DIR/iso8583service.service" "$SERVER:/tmp/"
ssh "$SERVER" << EOF
    sudo mv /tmp/iso8583service.service /etc/systemd/system/
    sudo systemctl daemon-reload
    sudo systemctl enable iso8583service
    sudo systemctl restart iso8583service
EOF

echo "✅ Done!"
echo "   ISO8583 port: 9443"
echo "   WebAPI:       http://$SERVER:$API_PORT/api/iso8583/status"
echo ""
echo "Check status:"
ssh "$SERVER" "sudo systemctl status iso8583service --no-pager"
