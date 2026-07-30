# ==========================================
# Etapa 1: Build de Angular con Node.js 22
# ==========================================
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
RUN npm run build -- --configuration production

# ==========================================
# Etapa 2: Servidor NGINX ultraligero de Producción
# ==========================================
FROM nginx:alpine AS runner
COPY --from=build /app/dist/OrderFlow.App/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
