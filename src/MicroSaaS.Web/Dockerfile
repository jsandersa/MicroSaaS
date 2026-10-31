# =====================================================================
# Stage 1: Build Angular Application (Node.js LTS)
# =====================================================================
FROM node:22-alpine AS build
WORKDIR /app

COPY ["package*.json", "./"]
RUN npm install

COPY . .
RUN npm run build -- --configuration production

# =====================================================================
# Stage 2: Serve with NGINX (Alpine)
# =====================================================================
FROM nginx:alpine AS final

COPY ["nginx.conf", "/etc/nginx/conf.d/default.conf"]
COPY --from=build /app/dist/micro-saas-web/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

