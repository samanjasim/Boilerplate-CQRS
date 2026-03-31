# -- Stage 1: Build --
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files first (maximizes layer cache)
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and build
COPY . .

# Build args for environment variables baked at build time
ARG VITE_API_BASE_URL=https://your-api-url.com/api/v1
ARG VITE_APP_NAME=Starter
ARG VITE_ABLY_API_KEY=
ARG VITE_BASE_DOMAIN=starter.com

RUN npm run build

# -- Stage 2: Runtime (nginx) --
FROM nginx:alpine AS runtime

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
