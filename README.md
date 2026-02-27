# IngestionService — AgroMonitor

Microsserviço responsável por receber dados de sensores de campo (simulados), persistir no Azure SQL e
publicar eventos no **Azure Service Bus** para que o AnalysisService processe as regras de alerta.

---

## Endpoints

| Método | Rota                             | Descrição                          | Auth |
| ------ | -------------------------------- | ---------------------------------- | ---- |
| POST   | `/sensor-data`                   | Recebe uma leitura de sensor       | JWT  |
| POST   | `/sensor-data/batch`             | Recebe até 100 leituras em um lote | JWT  |
| GET    | `/sensor-data/{fieldId}/history` | Histórico de leituras de um talhão | JWT  |
| GET    | `/health`                        | Health check                       | —    |
| GET    | `/metrics`                       | Métricas Prometheus                | —    |
| GET    | `/swagger`                       | Documentação interativa            | —    |

---

## Payload de Exemplo

### POST `/sensor-data`

```json
{
  "fieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "soilHumidity": 28.5,
  "temperature": 32.1,
  "precipitation": 0.0,
  "recordedAt": "2024-03-15T10:30:00Z"
}
```

### POST `/sensor-data/batch`

```json
{
  "readings": [
    {
      "fieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "soilHumidity": 25.0,
      "temperature": 33.5,
      "precipitation": 0.0,
      "recordedAt": "2024-03-15T09:00:00Z"
    },
    {
      "fieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "soilHumidity": 24.2,
      "temperature": 34.0,
      "precipitation": 0.0,
      "recordedAt": "2024-03-15T10:00:00Z"
    }
  ]
}
```

### GET `/sensor-data/{fieldId}/history?from=2024-03-01&to=2024-03-15&limit=100`

---

## Fluxo Interno

```
POST /sensor-data
      │
      ▼
IngestionService.IngestAsync()
      │
      ├─► Persiste SensorReading no Azure SQL (agromonitor-ingestion)
      │
      └─► ServiceBusPublisher.PublishAsync(SensorReadingEvent)
                │
                ▼
          Topic: sensor-readings   ←── AnalysisService consome
```

---

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- **Azure Service Bus** com um Topic chamado `sensor-readings`
- `dotnet-ef` instalado: `dotnet tool install --global dotnet-ef`

---

## Passo a Passo — Ambiente Local

### 1. Criar o Topic no Azure Service Bus

```bash
# Criar namespace (se ainda não existir)
az servicebus namespace create \
  --resource-group rg-agromonitor \
  --name agromonitor-bus \
  --location brazilsouth \
  --sku Standard

# Criar o topic
az servicebus topic create \
  --resource-group rg-agromonitor \
  --namespace-name agromonitor-bus \
  --name sensor-readings

# Criar a subscription (usada pelo AnalysisService)
az servicebus topic subscription create \
  --resource-group rg-agromonitor \
  --namespace-name agromonitor-bus \
  --topic-name sensor-readings \
  --name analysis-sub

# Obter a connection string
az servicebus namespace authorization-rule keys list \
  --resource-group rg-agromonitor \
  --namespace-name agromonitor-bus \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

Cole a connection string retornada no `appsettings.Development.json`.

---

### 2. Criar o banco de dados

Conecte em `localhost,1433` com `fiapas` / `123@login` e execute:

```sql
CREATE DATABASE [agromonitor-ingestion];
```

---

### 3. Gerar e aplicar a Migration

```bash
dotnet ef migrations add InitialCreate \
  --project IngestionService.csproj \
  --output-dir src/Data/Migrations

dotnet ef database update
```

---

### 4. Rodar o serviço

```bash
dotnet run
```

Acesse:

- Swagger UI: http://localhost:5003/swagger
- Metrics: http://localhost:5003/metrics

---

## Docker Compose

```bash
# Preencha ServiceBus__ConnectionString no docker-compose.yml antes de subir
docker compose up --build
```

---

## Passo a Passo — Azure (Produção)

### 1. Criar banco no Azure SQL Server existente

```bash
az sql db create \
  --resource-group rg-agromonitor \
  --server agromonitor-sqlsrv \
  --name agromonitor-ingestion \
  --service-objective Basic
```

### 2. Atualizar connection string

```json
"IngestionDb": "Server=agromonitor-sqlsrv.database.windows.net;Database=agromonitor-ingestion;User Id=fiapas;Password=123@login;Encrypt=True;"
```

### 3. Aplicar migration apontando para Azure

```bash
dotnet ef database update
```

---

## Variáveis de Ambiente

| Variável                         | Descrição                                 |
| -------------------------------- | ----------------------------------------- |
| `ConnectionStrings__IngestionDb` | Connection string do Azure SQL            |
| `ServiceBus__ConnectionString`   | Connection string do Azure Service Bus    |
| `ServiceBus__TopicName`          | Nome do topic (padrão: `sensor-readings`) |
| `Jwt__Secret`                    | Mesma chave usada no IdentityService      |
| `Jwt__Issuer`                    | Mesmo issuer (ex: `IdentityService`)      |
| `Jwt__Audience`                  | Mesmo audience (ex: `AgroMonitor`)        |

---

## Observabilidade

| Métrica                                   | Tipo      | Descrição                     |
| ----------------------------------------- | --------- | ----------------------------- |
| `ingestion_readings_total`                | Counter   | Total de leituras recebidas   |
| `ingestion_batches_total`                 | Counter   | Total de batches recebidos    |
| `ingestion_http_request_duration_seconds` | Histogram | Latência das requisições HTTP |

Configure o `prometheus.yml` para fazer scrape em `http://ingestion-service:8080/metrics`.
