# Imagem do frontend para produção. Construída pelo docker-compose.prod.yml (contexto: esta pasta).
#
# Etapa 1: o Node gera os arquivos estáticos (pasta dist). Etapa 2: o nginx serve esses arquivos e
# repassa /api para a API. Assim tudo fica no mesmo endereço e o navegador não precisa de CORS.

FROM node:24-alpine AS build
WORKDIR /app
# Só package.json e lock primeiro: o "npm ci" fica em cache enquanto as dependências não mudam.
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.30-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
