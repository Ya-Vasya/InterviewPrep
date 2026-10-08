# Build context: frontend/   (docker build -t interviewprep-web frontend)

FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final

# Rendered with envsubst on start; nginx's own variables ($uri, ...) are left alone
# because only the variables defined in the environment are substituted.
ENV BACKEND_URL=http://backend:8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
