#!/bin/bash
set -e

echo "Starting ComfyUI server..."

# Install custom nodes
# echo "Installing custom nodes via API..."
# /app/install-custom-nodes.sh

# Start ComfyUI in background
/app/start-comfy.sh &
COMFY_PID=$!
echo "ComfyUI process started with PID: $COMFY_PID"

# Wait for ComfyUI to initialize
echo "Waiting for ComfyUI to initialize..."
MAX_RETRIES=30
RETRY_INTERVAL=10
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if ! ps -p $COMFY_PID > /dev/null; then
        echo "ERROR: ComfyUI process died during initialization."
        exit 1
    fi

    echo "Attempt $((RETRY_COUNT+1))/$MAX_RETRIES: Checking if ComfyUI server is responding..."
    RESPONSE=$(curl -s -m 5 "http://$Comfy__Host:$Comfy__Port/system_stats" 2>/dev/null || echo "")

    if [ ! -z "$RESPONSE" ]; then
        echo "ComfyUI server is ready!"
        break
    fi

    sleep $RETRY_INTERVAL
    RETRY_COUNT=$((RETRY_COUNT + 1))
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo "ERROR: ComfyUI server failed to start within the expected time."
    exit 1
fi

# Start .NET application
echo "Starting Selfix.ImageGenerator.EntryPoint..."
dotnet /app/Selfix.ImageGenerator.EntryPoint.dll &
DOTNET_PID=$!

# Handle termination
trap 'kill $COMFY_PID $DOTNET_PID 2>/dev/null || true' SIGTERM SIGINT

# Monitor both processes
while kill -0 $COMFY_PID &>/dev/null && kill -0 $DOTNET_PID &>/dev/null; do
    sleep 2
done

# If we get here, something died
if ! kill -0 $COMFY_PID &>/dev/null; then
    echo "ERROR: ComfyUI service exited unexpectedly"
else
    echo "ERROR: .NET application exited unexpectedly"
fi

# Cleanup
kill $COMFY_PID $DOTNET_PID &>/dev/null || true
exit 1