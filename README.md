This document outlines the architectural proposal for implementing a local-first, cross-platform RAG (Retrieval-Augmented Generation) solution using Microsoft Semantic Kernel.

-----

### **Architectural Proposal: Local RAG Implementation with Semantic Kernel**

This proposal details the plan, components, and step-by-step implementation guide for a workshop focused on building a RAG pipeline. The solution adheres to the specified constraints: local-only execution, cross-platform compatibility (.NET on Windows/Ubuntu/macOS), and the use of C\# with Semantic Kernel.

-----

### \#\# 1. Overview

The goal is to build a chat application that leverages a locally hosted Large Language Model (LLM) to answer questions based on a specific corpus of PDF documents. The entire process—from document ingestion to response generation—will run on a local machine without reliance on external cloud services.

The architecture will center around the **Semantic Kernel** as the orchestration engine. It will connect three key local components:

1.  **Document Loader & Processor**: A C\# module responsible for parsing text from PDF files and chunking it for processing.
2.  **Vector Database**: A local instance of ChromaDB, running in Docker, to store document embeddings for efficient similarity searches.
3.  **Local LLM Service**: A local LLM (e.g., Mistral, Llama 3) served via LM Studio or Ollama, which provides an OpenAI-compatible API endpoint for both embedding generation and chat completions.

**The core workflow is as follows:**

  * **Ingestion Phase**: The application reads PDF documents, extracts text, splits it into smaller, semantically meaningful chunks, and uses SK's memory connector to generate embeddings (via the local LLM) and store them in ChromaDB.
  * **Retrieval & Augmentation Phase**: When a user submits a query, the application uses SK's memory capabilities to search ChromaDB for the most relevant document chunks. These chunks are then "augmented" into a prompt that is sent to the local LLM.
  * **Generation Phase**: The LLM receives the augmented prompt (containing both the user's question and the retrieved context) and generates a contextually aware answer.

> **Note for Stakeholders:** Think of this system as giving a powerful but general-purpose AI a specific set of reference books (our PDFs) to read before answering a question. The "Vector Database" is like a hyper-efficient index for these books, allowing the AI to find the exact right page in milliseconds instead of reading the whole library for every single question. This ensures the AI's answers are not just generic, but are grounded in our specific, proprietary data.

-----

### \#\# 2. Implementation Plan

The implementation can be broken down into four distinct stages.

  * **Stage 1: Environment and Project Setup**

      * Initialize a new .NET 8 Console Application. .NET is fully cross-platform, ensuring the solution will run on any target OS.
      * Set up a `docker-compose.yml` file to manage the local ChromaDB instance for simplicity and portability.
      * Add the required NuGet packages to the C\# project.

  * **Stage 2: Document Ingestion Pipeline**

      * Implement a service to read and parse PDF documents.
      * Implement text chunking logic. We will use a simple fixed-size chunking with overlap to start, but this can be replaced with more sophisticated methods later.
      * Configure the Semantic Kernel with connectors for the local LLM (for embeddings) and the local ChromaDB instance.

    <!-- end list -->

      - Create a script or function to orchestrate the pipeline: `foreach document -> extract text -> foreach chunk -> generate & save embedding`.

  * **Stage 3: RAG Chat Service**

      * Configure a second instance of the Semantic Kernel (or a different configuration on the same kernel) with a connector for the local LLM's chat completion endpoint.
      * Implement a user input loop for the chat interface.

  * **Stage 4: Core RAG Logic**

      * For each user query, perform a semantic search against the ChromaDB memory store.
      * Dynamically construct a prompt using the retrieved context and the user's question.
      * Invoke the LLM via the SK chat completion service to get the final answer.
      * Stream the response back to the user console.

-----

### \#\# 3. Step-by-Step Workshop Scenario

This scenario provides a detailed, sequential guide intended for execution. The instructions are granular enough to be followed by a developer or an automated agent.

**Prerequisites:**

  * .NET 8 SDK installed.
  * Docker Desktop installed and running.
  * LM Studio or Ollama installed, with a model downloaded and running, exposing an OpenAI-compatible endpoint (e.g., `http://localhost:1234/v1`).

**Step 1: Project Initialization**

1.  Create a directory for the project: `mkdir SKLocalRAG && cd SKLocalRAG`
2.  Create a `docker-compose.yml` file for ChromaDB:
    ```yaml
    version: '3.8'
    services:
      chroma:
        image: chromadb/chroma
        ports:
          - "8000:8000"
        volumes:
          - chroma-data:/chroma/chroma
    volumes:
      chroma-data:
    ```
3.  Start ChromaDB: `docker-compose up -d`
4.  Create a .NET Console App: `dotnet new console -n RAGWorkshop`
5.  Navigate into the project directory: `cd RAGWorkshop`
6.  Add required NuGet packages:
    ```shell
    dotnet add package Microsoft.SemanticKernel --version "1.12.0"
    dotnet add package Microsoft.SemanticKernel.Connectors.Chroma --version "1.12.0"
    dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI --version "1.12.0"
    dotnet add package iTextSharp # Or another PDF parsing library like PdfPig
    ```

**Step 2: Kernel and Memory Configuration**

1.  In `Program.cs`, create a `Kernel` instance.
2.  Configure the `OpenAITextEmbeddingGenerationService` and `OpenAIChatCompletionService`. **Crucially, point the `endpoint` parameter to your local LLM server's URL** (e.g., `http://localhost:1234/v1`). The `apiKey` can typically be set to `null` or any string for local servers.
3.  Configure the `ChromaMemoryStore` pointing to your local ChromaDB instance (`http://localhost:8000`).
4.  Build the `SemanticTextMemory` instance using the configured memory store and embedding generator.

*Example C\# Code Snippet:*

```csharp
#pragma warning disable SKEXP0001, SKEXP0011, SKEXP0028, SKEXP0052

var kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.AddOpenAIChatCompletion(
    modelId: "local-llm",              // Model name is arbitrary for local servers
    apiKey: null,                      // No API key needed
    endpoint: new Uri("http://localhost:1234"));

kernelBuilder.AddOpenAITextEmbeddingGeneration(
    modelId: "local-embedding-model",  // Use an embedding model
    apiKey: null,
    endpoint: new Uri("http://localhost:1234"));

var kernel = kernelBuilder.Build();

var chromaMemoryStore = new ChromaMemoryStore("http://localhost:8000");
var memory = new SemanticTextMemory(chromaMemoryStore, 
    kernel.GetRequiredService<ITextEmbeddingGenerationService>());
```

**Step 3: Implement the Ingestion Pipeline**

1.  Create a `DataIngestion` class.
2.  Write a method `IngestPdfAsync(string filePath, ISemanticTextMemory memory)` within this class.
3.  Inside the method, use `iTextSharp` to extract raw text from the PDF.
4.  Split the extracted text into chunks of \~512 tokens with an overlap of \~50 tokens.
5.  Loop through the chunks and save each to the memory store. Use the file path as part of the `id` for easier tracking.
    ```csharp
    // Inside the loop
    await memory.SaveInformationAsync(
        collection: "my-document-collection",
        text: chunk,
        id: $"{filePath}-{chunkIndex}"
    );
    ```

**Step 4: Implement the RAG Chat Loop**

1.  In `Program.cs`, create an infinite `while` loop for the chat.
2.  Prompt the user for input: `Console.Write("User > ");`
3.  Use the `memory` object to search for relevant context:
    ```csharp
    var searchResults = memory.SearchAsync(
        "my-document-collection", 
        userInput, 
        limit: 3, 
        minRelevanceScore: 0.75);

    var context = new StringBuilder();
    await foreach (var result in searchResults)
    {
        context.AppendLine(result.Metadata.Text);
    }
    ```
4.  Create a prompt template that incorporates the context.
    ```csharp
    string prompt = $"""
    You are a helpful AI assistant answering questions based on the provided context.

    Context:
    ---
    {context.ToString()}
    ---

    Question: {userInput}

    Answer:
    """;
    ```
5.  Invoke the LLM through the kernel:
    ```csharp
    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
    var response = await chatCompletionService.GetChatMessageContentAsync(prompt);
    Console.WriteLine($"AI > {response.Content}");
    ```

-----

### \#\# 4. Description of Outcomes

Upon completion of this workshop, you will have a fully functional, local, and cross-platform RAG chat application.

  * **Primary Outcome**: A .NET console application that successfully answers questions about a provided set of PDF documents, demonstrating a practical understanding of Semantic Kernel for building AI-powered applications.
  * **Key Learnings**:
      * **SK Modularity**: A clear understanding of how SK's components (Kernel, Connectors, Memory) are composed to build complex workflows.
      * **Local LLM Integration**: Practical experience connecting SK to non-Azure/OpenAI services that adhere to the OpenAI API standard.
      * **Memory and RAG**: Hands-on experience with the core concepts of semantic memory, embedding generation, and retrieval-augmented generation.
  * **Extensibility**: The final solution serves as a robust foundation that can be extended with more advanced SK features like Planners, custom Functions/Plugins, and more sophisticated document processing pipelines.

-----

### \#\# 5. Proposed Resources for Further Review

1.  **Official Semantic Kernel Documentation**: [Microsoft Learn - Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
2.  **SK GitHub Repository (Examples)**: The C\# and Python examples are invaluable. [SK GitHub Repo](https://github.com/microsoft/semantic-kernel)
3.  **Connectors in Semantic Kernel**: A deeper dive into how SK connects to various services. [SK Connectors Documentation](https://www.google.com/search?q=https://learn.microsoft.com/en-us/semantic-kernel/agents/plugins/out-of-the-box-plugins%3Fpivots%3Dprogramming-language-csharp)
4.  **Semantic Memory Concepts**: Detailed explanation of how memory works in SK. [SK Memory Documentation](https://learn.microsoft.com/en-us/semantic-kernel/memories/)"
