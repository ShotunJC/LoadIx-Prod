FROM node:24.13.1-alpine AS build
WORKDIR /app

COPY apps/loadix-web/package.json apps/loadix-web/package-lock.json ./
RUN npm ci
COPY apps/loadix-web/ ./
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.29.5-alpine AS final
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
  CMD wget --quiet --spider http://127.0.0.1:8080/ || exit 1
