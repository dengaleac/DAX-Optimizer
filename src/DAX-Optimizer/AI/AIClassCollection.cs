using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX_Optimizer.AI
{

    /// <summary>
    /// Supported API providers for AI chat functionality.
    /// </summary>
    public enum ApiProvider
    {
        OpenAI,
        Ollama
    }

    /// <summary>
    /// Configuration for connecting to an AI API provider.
    /// </summary>
    public class ApiConfig
    {
        /// <summary>
        /// The selected API provider (OpenAI or Ollama).
        /// </summary>
        public ApiProvider Provider { get; set; }
        /// <summary>
        /// API key for authentication (required for OpenAI).
        /// </summary>
        public string ApiKey { get; set; } // Only needed for OpenAI
        /// <summary>
        /// Custom base URL for the API endpoint.
        /// </summary>
        public string BaseUrl { get; set; } // Custom base URL if needed
        /// <summary>
        /// The model name to use for requests.
        /// </summary>
        public string Model { get; set; }
    }

    /// <summary>
    /// Represents a chat request to the AI API.
    /// </summary>
    public class ChatRequest
    {
        /// <summary>
        /// The system prompt (instructions for the AI).
        /// </summary>
        public string SystemPrompt { get; set; }
        /// <summary>
        /// The user's prompt or question.
        /// </summary>
        public string UserPrompt { get; set; }
        /// <summary>
        /// Controls randomness of the response (higher = more random).
        /// </summary>
        public double Temperature { get; set; } = 0.7;
        /// <summary>
        /// Maximum number of tokens in the response.
        /// </summary>
        public int MaxTokens { get; set; } = 1000;
        /// <summary>
        /// Whether to stream the response.
        /// </summary>
        public bool Stream { get; set; } = false;
    }

    /// <summary>
    /// Represents a chat response from the AI API.
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// The content of the AI's response.
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// Indicates if the request was successful.
        /// </summary>
        public bool IsSuccess { get; set; }
        /// <summary>
        /// Error message if the request failed.
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    // OpenAI API Models

    /// <summary>
    /// Request payload for OpenAI chat completion API.
    /// </summary>
    public class OpenAIRequest
    {
        public string model { get; set; }
        public OpenAIMessage[] messages { get; set; }
        public double temperature { get; set; }
        public int max_completion_tokens { get; set; }
    }

    /// <summary>
    /// Represents a message in OpenAI chat format.
    /// </summary>
    public class OpenAIMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    /// <summary>
    /// Response from OpenAI chat completion API.
    /// </summary>
    public class OpenAIResponse
    {
        public OpenAIChoice[] choices { get; set; }
    }

    /// <summary>
    /// Represents a single choice in OpenAI response.
    /// </summary>
    public class OpenAIChoice
    {
        public OpenAIMessage message { get; set; }
    }

    // Ollama API Models

    /// <summary>
    /// Request payload for Ollama chat API.
    /// </summary>
    public class OllamaRequest
    {
        public string model { get; set; }
        public string prompt { get; set; }
        public bool stream { get; set; }
        public OllamaOptions options { get; set; }
    }

    /// <summary>
    /// Options for Ollama chat API request.
    /// </summary>
    public class OllamaOptions
    {
        public double temperature { get; set; }
        public int num_predict { get; set; }
    }

    /// <summary>
    /// Response from Ollama chat API.
    /// </summary>
    public class OllamaResponse
    {
        public string response { get; set; }
        public bool done { get; set; }
    }

    /// <summary>
    /// Event arguments for streaming content from AI responses.
    /// </summary>
    public class StreamingContentEventArgs : EventArgs
    {
        /// <summary>
        /// The streamed content chunk.
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// Indicates if the streaming is complete.
        /// </summary>
        public bool IsComplete { get; set; }
    }

}
