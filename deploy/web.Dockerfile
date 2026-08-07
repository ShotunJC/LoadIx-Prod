FROM node:24.13.1-alpine AS build
WORKDIR /app

COPY apps/loadix-web/package.json apps/loadix-web/package-lock.json ./
RUN npm ci
COPY apps/loadix-web/ ./
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.31.3-alpine3.24 AS final
ARG API_UPSTREAM=api:8080
ARG API_HOST=api
COPY deploy/nginx.conf.template /tmp/nginx.conf.template
RUN sed -e "s|__API_UPSTREAM__|${API_UPSTREAM}|g" \
    -e "s|__API_HOST__|${API_HOST}|g" \
    /tmp/nginx.conf.template \
    > /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
USER 101
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://127.0.0.1:8080/health || exit 1
