FROM grafana/k6:latest AS k6official

FROM node:18-alpine AS builder

WORKDIR /app
COPY ["LoadTests/K6LoadTests/", "."]
RUN npm install

COPY --from=k6official /usr/bin/k6 /usr/bin/k6

EXPOSE 3001
CMD ["node", "app.js"]