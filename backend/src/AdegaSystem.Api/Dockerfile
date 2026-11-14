# Imagem da API para produção. Construída pelo docker-compose.prod.yml (contexto: esta pasta).
#
# Duas etapas: a primeira tem o SDK (grande) e compila; a segunda leva só o resultado
# e o runtime do ASP.NET, então a imagem final fica bem menor e sem código-fonte.

# Etapa 1: restaura os pacotes e publica a API em modo Release.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copia primeiro só o .csproj: enquanto os pacotes não mudam, o Docker reaproveita o restore do cache.
COPY AdegaSystem.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# Etapa 2: só o runtime.
# Usamos a imagem padrão (Ubuntu), e NÃO a variante -alpine: a Alpine não traz o banco de fusos
# horários (tzdata), e sem ele o fuso de Brasília (America/Sao_Paulo, ver Common/BusinessClock.cs)
# não existe. Vendas, relatórios e validades contariam o dia errado.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
# Roda com um usuário sem privilégios (já existe na imagem) e escuta na porta 8080 (padrão da imagem).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdegaSystem.Api.dll"]
