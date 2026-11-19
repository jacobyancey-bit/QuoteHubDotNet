# AWS Deployment Guide: QuoteHub.Api

This document outlines how QuoteHubDotNet is architected for deployment across **Amazon Web Services (AWS)** environments, fulfilling modern enterprise cloud standards.

---

## 1. AWS Architecture Overview

```
                      +-------------------+
                      |   Amazon Route53  | (DNS)
                      +---------+---------+
                                |
                                v
                   +-------------------------+
                   | Application Load Balancer|
                   +------------+------------+
                                |
              +-----------------+-----------------+
              |                                   |
              v                                   v
   +---------------------+             +---------------------+
   | AWS App Runner /    |             | AWS App Runner /    |
   | ECS Fargate (Task 1)|             | ECS Fargate (Task 2)|
   | .NET 10 Container   |             | .NET 10 Container   |
   +----------+----------+             +----------+----------+
              |                                   |
              +-----------------+-----------------+
                                |
                                v
                   +-------------------------+
                   |  Amazon RDS PostgreSQL   | (Multi-AZ with Automated Backups)
                   +-------------------------+
```

---

## 2. Deployment Options on AWS

### Option A: AWS App Runner (Fastest, Fully Managed)
App Runner automatically builds or pulls container images and provides HTTPS endpoints, scaling from 0 to N without cluster management.

1. **Push Container to Amazon ECR:**
   ```bash
   aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <aws_account_id>.dkr.ecr.us-east-1.amazonaws.com
   docker build -t quotehub-api .
   docker tag quotehub-api:latest <aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/quotehub-api:latest
   docker push <aws_account_id>.dkr.ecr.us-east-1.amazonaws.com/quotehub-api:latest
   ```

2. **Launch App Runner Service:**
   - Source: ECR Image
   - Port: `8080`
   - Health check path: `/health`
   - Environment Variable: `DATABASE_URL` pointing to Amazon RDS.

### Option B: Amazon ECS with AWS Fargate (Enterprise Defense Standard)
In federal and defense contractor environments (e.g., Huntsville GovCloud / IL4/IL5 environments):
- Tasks run serverless on **AWS Fargate** inside private VPC subnets.
- Traffic arrives through an **Application Load Balancer (ALB)** with an AWS WAF (Web Application Firewall) attached.
- Database credentials are fed from **AWS Secrets Manager** directly into container task definitions.

---

## 3. Resiliency & Enterprise Data Handling
- **EF Core Retries:** `EnableRetryOnFailure()` is configured in `Program.cs` to transparently handle any transient connection failovers during AWS Multi-AZ maintenance.
- **Deep Health Probes:** AWS ALB target group health checks target `/health`, which checks both container responsiveness and database connectivity via EF Core's `AddDbContextCheck<QuoteDbContext>()`.
