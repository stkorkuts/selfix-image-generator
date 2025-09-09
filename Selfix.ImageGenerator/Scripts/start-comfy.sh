#!/bin/bash
set -e

echo "Starting ComfyUI..."

echo "Copying lora from mounted drive to comfyui location"

cp -r /workspace/tocopy/models/loras/* /workspace/comfyui/models/loras/

python3 /workspace/comfyui/main.py --port $Comfy__Port --listen 0.0.0.0 --fast --fp8_e4m3fn-unet