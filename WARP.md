# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

Repository purpose and current state
- This repository is a workshop plan for building a local-first, cross-platform RAG (Retrieval-Augmented Generation) app using .NET 8 and Microsoft Semantic Kernel, backed by a local ChromaDB and a locally hosted LLM exposed via an OpenAI-compatible API (e.g., LM Studio or Ollama).
- No application code is checked in yet; follow the commands below (drawn from README.md) to scaffold and run the solution locally.

Common commands (from README)
- Start local ChromaDB (after creating docker-compose.yml as shown in README):
  - docker-compose up -d
  - To stop: docker-compose down
- Scaffold a .NET console app (new project directory):
  - dotnet new console -n RAGWorkshop
  - cd RAGWorkshop
- Add required NuGet packages (versions per README):
  - dotnet add package Microsoft.SemanticKernel --version "1.12.0"
  - dotnet add package Microsoft.SemanticKernel.Connectors.Chroma --version "1.12.0"
  - dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI --version "1.12.0"
  - dotnet add package iTextSharp
- Build and run (after you implement Program.cs and ingestion/chat logic):
  - dotnet build
  - dotnet run

Environment assumptions (from README)
- .NET 8 SDK installed.
- Docker Desktop installed and running (ChromaDB on http://localhost:8000).
- A local LLM (LM Studio or Ollama) running with an OpenAI-compatible endpoint (e.g., http://localhost:1234/v1). For local servers, apiKey can typically be null/any string.

High-level architecture (big picture)
- Orchestrator: Microsoft Semantic Kernel (SK) configures two OpenAI-compatible connectors against the local LLM server:
  - Chat completion (AddOpenAIChatCompletion)
  - Embedding generation (AddOpenAITextEmbeddingGeneration)
- Vector store: ChromaDB (local, via Docker) used through SK’s Chroma connector as the memory store.
- Ingestion pipeline:
  - Parse PDFs locally (e.g., iTextSharp), split text into ~512-token chunks with ~50-token overlap.
  - For each chunk, generate embeddings via SK’s embedding service and save to Chroma with an identifiable id (e.g., filePath-chunkIndex) into a named collection (e.g., "my-document-collection").
- Retrieval + augmentation:
  - For a user query, search Chroma via SK’s semantic memory (limit top-k, apply min relevance threshold).
  - Aggregate retrieved chunk texts into a context block.
- Generation:
  - Construct a prompt that embeds the retrieved context and the user’s question.
  - Send to the local LLM via SK’s chat completion to produce the final response.
- Interaction model:
  - Console loop reading user input, performing retrieve-augment-generate each turn, and printing responses.

File and component expectations once scaffolded
- docker-compose.yml at the repo root defining the chroma service, exposing port 8000 and persisting a volume.
- A .NET console project (e.g., RAGWorkshop):
  - Program.cs: Kernel setup (AddOpenAIChatCompletion, AddOpenAITextEmbeddingGeneration), Chroma memory wiring, and chat loop.
  - A DataIngestion class responsible for PDF extraction, chunking, and saving chunk embeddings to the memory store.

Key integration details (from README)
- SK kernel wiring uses the local endpoint (e.g., http://localhost:1234) for both embeddings and chat.
- Chroma memory store endpoint is http://localhost:8000.
- Memory search returns results that are concatenated into a context string, then inserted into a prompt template before calling chat completion.

Source of truth
- All of the above is summarized from README.md in this repository. Consult README.md for the full step-by-step workshop instructions and code snippets when implementing.