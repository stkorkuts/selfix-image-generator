# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# Copy project files first to leverage Docker cache for restore
COPY ["Selfix.ImageGenerator.EntryPoint/Selfix.ImageGenerator.EntryPoint.csproj", "Selfix.ImageGenerator.EntryPoint/"]
COPY ["Selfix.ImageGenerator.Application/Selfix.ImageGenerator.Application.csproj", "Selfix.ImageGenerator.Application/"]
COPY ["Selfix.ImageGenerator.Infrastructure/Selfix.ImageGenerator.Infrastructure.csproj", "Selfix.ImageGenerator.Infrastructure/"]
COPY ["Selfix.ImageGenerator.Shared/Selfix.ImageGenerator.Shared.csproj", "Selfix.ImageGenerator.Shared/"]
COPY ["Submodules/Selfix.Schema/Selfix.Schema/Selfix.Schema.csproj", "Submodules/Selfix.Schema/Selfix.Schema/"]
RUN dotnet restore "Selfix.ImageGenerator.EntryPoint/Selfix.ImageGenerator.EntryPoint.csproj"
# Copy the rest of the code
COPY . .
WORKDIR "/src/Selfix.ImageGenerator.EntryPoint"
RUN dotnet publish "Selfix.ImageGenerator.EntryPoint.csproj" -c Release -o /app

# Runtime stage with ComfyUI
FROM nvidia/cuda:12.4.1-base-ubuntu22.04 AS runtime

# Install .NET Runtime and Python dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    wget \
    curl \
    apt-transport-https \
    software-properties-common \
    libgl1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    python3-pip \
    python3-dev \
    git \
    build-essential && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    dotnet-runtime-9.0 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Install PyTorch with CUDA support
RUN pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124

# Define environment variables for UID and GID
ENV PUID=${PUID:-1000}
ENV PGID=${PGID:-1000}

# Create a group and user with specified GID/UID
RUN groupadd -g "${PGID}" appuser && \
    useradd -m -s /bin/sh -u "${PUID}" -g "${PGID}" appuser

# Setup directories
RUN mkdir -p /app /workspace /workspace/workflows

# Clone and install ComfyUI
RUN git clone https://github.com/comfyanonymous/ComfyUI.git /workspace/comfyui && \
    cd /workspace/comfyui && \
    git checkout tags/v0.3.28 && \
    pip install --no-cache-dir -r requirements.txt 

# Install custom nodes
RUN cd /workspace/comfyui/custom_nodes && \
    # Impact Pack
    git clone https://github.com/ltdrdata/ComfyUI-Impact-Pack.git && \
    cd ComfyUI-Impact-Pack && pip install -r requirements.txt && cd .. && \
    # Impact Subpack
    git clone https://github.com/ltdrdata/ComfyUI-Impact-Subpack.git && \
    cd ComfyUI-Impact-Subpack && pip install -r requirements.txt && cd ..

# Copy the published .NET app
COPY --from=build /app /app

# Copy startup scripts
COPY Scripts/start-comfy.sh /app/start-comfy.sh
COPY Scripts/entrypoint.sh /app/entrypoint.sh
# COPY Scripts/install-custom-nodes.sh /app/install-custom-nodes.sh
COPY Workflows /workspace/workflows
RUN chmod +x /app/start-comfy.sh /app/entrypoint.sh #/app/install-custom-nodes.sh

# Set proper ownership
RUN chown -R appuser:appuser /workspace /app

# Switch to non-root user
USER appuser

ENTRYPOINT ["/app/entrypoint.sh"]