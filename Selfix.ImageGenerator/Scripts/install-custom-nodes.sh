#!/bin/bash
set -e

echo "Installing ComfyUI custom nodes via direct git clone..."

# Function to install a custom node
install_node() {
  NODE_NAME=$1
  REPO_URL=$2
  
  echo "Installing $NODE_NAME from $REPO_URL"
  
  # Clone the repository
  git clone "$REPO_URL" "/workspace/comfyui/custom_nodes/$NODE_NAME"
  
  # Install dependencies if requirements.txt exists
  if [ -f "/workspace/comfyui/custom_nodes/$NODE_NAME/requirements.txt" ]; then
    echo "Installing dependencies for $NODE_NAME"
    pip install -r "/workspace/comfyui/custom_nodes/$NODE_NAME/requirements.txt"
  fi
  
  echo "$NODE_NAME installed successfully"
}

# Install Impact Pack
install_node "ComfyUI-Impact-Pack" "https://github.com/ltdrdata/ComfyUI-Impact-Pack.git"

# Install Impact Subpack
install_node "ComfyUI-Impact-Subpack" "https://github.com/ltdrdata/ComfyUI-Impact-Subpack.git"

echo "All custom nodes installation complete!"