# PramosClearing

**PramosClearing** é uma clearing fictícia de negociação de ações, desenvolvida para simular operações de compra e venda de ativos, gerenciamento de portfólio e acompanhamento de preços em tempo real.

---

## 📋 Domínio do Sistema

Os usuários da plataforma podem:

- **Criar conta** – Cadastro com autenticação segura.
- **Receber saldo fictício** – Saldo inicial para simulação de operações.
- **Comprar ações** – Adquirir ativos disponíveis na bolsa simulada.
- **Vender ações** – Liquidar posições e retornar saldo à conta.
- **Ver portfólio** – Visualizar a carteira de investimentos com posições e rendimentos.
- **Acompanhar preço em tempo real** – Streaming de cotações atualizado continuamente via WebSocket/eventos.

---

## 🛠️ Stack de Tecnologias

| Camada | Tecnologia |
|---|---|
| **Backend** | C# .NET |
| **API** | ASP.NET Core |
| **Mensageria** | Apache Kafka / RabbitMQ |
| **Cache** | Redis |
| **Container** | Docker |
| **Orquestração** | Kubernetes |
| **Observabilidade** | Prometheus + Grafana |

### Detalhes

- **C# .NET** – Toda a lógica de negócio é implementada em .NET, aproveitando o ecossistema maduro da plataforma Microsoft para alta performance e manutenibilidade.
- **ASP.NET Core** – Exposição de endpoints REST e WebSocket para consumo pelos clientes front-end e integrações externas.
- **Apache Kafka / RabbitMQ** – Comunicação assíncrona entre serviços (ex.: execução de ordens, atualização de preços, notificações). Kafka é preferível para fluxos de alta vazão e replay de eventos; RabbitMQ para roteamento flexível com menor latência.
- **Redis** – Cache distribuído para cotações recentes, sessões de usuário e dados de portfólio consultados com frequência, reduzindo a carga no banco de dados.
- **Docker** – Empacotamento de cada serviço em imagem de container, garantindo paridade entre ambientes de desenvolvimento, teste e produção.
- **Kubernetes** – Orquestração dos containers em produção, fornecendo auto-scaling, self-healing, rolling updates e gerenciamento de secrets/configs.
- **Prometheus + Grafana** – Coleta de métricas de aplicação e infraestrutura (Prometheus) com visualização em dashboards interativos (Grafana), além de suporte a alertas.

---

## 🏗️ Arquitetura de Alto Nível

```
┌──────────────────────────────────────────────────────┐
│                    Clientes                          │
│         (Web App / Mobile / CLI)                     │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP / WebSocket
┌─────────────────────▼────────────────────────────────┐
│               ASP.NET Core API Gateway               │
└──────┬──────────────┬──────────────┬─────────────────┘
       │              │              │
┌──────▼──────┐ ┌─────▼─────┐ ┌─────▼──────┐
│  Accounts   │ │  Orders   │ │  Market    │
│  Service    │ │  Service  │ │  Data Svc  │
└──────┬──────┘ └─────┬─────┘ └─────┬──────┘
       │              │              │
       └──────────────▼──────────────┘
               Kafka / RabbitMQ
                 (Event Bus)
       ┌──────────────┬──────────────┐
       │              │              │
┌──────▼──────┐ ┌─────▼─────┐ ┌─────▼──────┐
│  Database   │ │   Redis   │ │ Prometheus │
│ (SQL/NoSQL) │ │  (Cache)  │ │ + Grafana  │
└─────────────┘ └───────────┘ └────────────┘
```

Todo o conjunto é empacotado com **Docker** e orquestrado via **Kubernetes**.

---

## 🚀 Como Executar (desenvolvimento local)

> **Pré-requisitos:** Docker e Docker Compose instalados.

```bash
# Clone o repositório
git clone https://github.com/PedroHRamos/PramosClearing.git
cd PramosClearing

# Suba os serviços de infraestrutura
docker compose up -d

# Acesse a API
# http://localhost:5000/swagger
```

---

## 📈 Observabilidade

- **Prometheus** coleta métricas expostas em `/metrics` por cada serviço.
- **Grafana** disponibiliza dashboards pré-configurados acessíveis em `http://localhost:3000`.
- Alertas podem ser configurados para latência de API, erros de ordem e lag de consumidores Kafka/RabbitMQ.

---

## 📝 Licença

Este projeto é de uso pessoal e educacional.
