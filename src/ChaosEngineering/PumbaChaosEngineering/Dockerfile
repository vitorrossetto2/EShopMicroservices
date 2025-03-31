FROM alpine:latest AS downloader

# Set environment variables
ENV PUMBA_VERSION=0.11.6
ENV PUMBA_BINARY=pumba_linux_amd64

# Install curl to download the Pumba binary
RUN apk add --no-cache curl

# Download the Pumba binary
RUN curl -L -o /pumba https://github.com/alexei-led/pumba/releases/download/${PUMBA_VERSION}/${PUMBA_BINARY}

# Make the binary executable
RUN chmod +x /pumba

FROM node:18-alpine AS builder

# Copy the Pumba binary from the downloader stage
COPY --from=downloader /pumba /usr/local/bin/pumba

WORKDIR /app
COPY ["ChaosEngineering/PumbaChaosEngineering/", "."]
RUN npm install


EXPOSE 3001
CMD ["node", "app.js"]