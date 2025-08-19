using DAX_Optimizer.Utilities;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Threading;
using System.CodeDom;

namespace DAX_Optimizer.AI
{
    /// <summary>
    /// Provides AI chat functionality for supported providers (OpenAI, Ollama).
    /// Handles configuration, streaming queries, and connection testing.
    /// </summary>
    public class AIService : IDisposable
    {
        #region Variables
        /// <summary>
        /// HTTP client used for API requests.
        /// </summary>
        private HttpClient _httpClient;
        /// <summary>
        /// Current API configuration.
        /// </summary>
        private ApiConfig _currentConfig;

        /// <summary>
        /// Raised when the service status changes (e.g., starting, completed, error).
        /// </summary>
        public event EventHandler<string> StatusChanged;
        /// <summary>
        /// Raised when streaming content is received from the AI API.
        /// </summary>
        public event EventHandler<StreamingContentEventArgs> ContentReceived;

        /// <summary>
        /// Gets the currently configured API provider.
        /// </summary>
        public ApiProvider CurrentProvider => _currentConfig?.Provider ?? ApiProvider.Ollama;
        /// <summary>
        /// Gets the currently configured model name.
        /// </summary>
        public string CurrentModel => _currentConfig?.Model ?? "Unknown";
        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the AIService class with default Ollama configuration.
        /// </summary>
        public AIService()
        {
            // Initialize with default Ollama configuration
            ConfigureProvider(ApiProvider.Ollama);
        }

        #endregion

        #region Generic Methods
        /// <summary>
        /// Configures the AI provider, API key, model, and base URL.
        /// </summary>
        /// <param name="provider">The API provider to use.</param>
        /// <param name="apiKey">API key for authentication (if required).</param>
        /// <param name="model">Model name to use.</param>
        /// <param name="baseUrl">Custom base URL for the API endpoint.</param>
        public void ConfigureProvider(ApiProvider provider, string apiKey = null, string model = null, string baseUrl = null)
        {
            // Raise a status change event before configuration
            OnStatusChanged($"Configuring {provider} provider...");

            // Dispose existing client
            _httpClient?.Dispose();

            // create new instance of ApiConfig with provided parameters
            _currentConfig = new ApiConfig
            {
                Provider = provider,
                ApiKey = apiKey,
                Model = model,
                BaseUrl = baseUrl
            };

            // Initialize the HTTP client
            _httpClient = new HttpClient();

            // Set default timeout to 5 minutes
            _httpClient.Timeout = TimeSpan.FromMinutes(5);

            // Setup the HTTP client based on the provider
            SetupHttpClient();

            // Raise a status change event after configuration
            OnStatusChanged($"{provider} provider configured successfully");
        }

        /// <summary>
        /// Sets up the HTTP client based on the current provider and configuration.
        /// </summary>
        private void SetupHttpClient()
        {
            
            switch (_currentConfig.Provider)
            {
                // Set base address and headers for OpenAI
                case ApiProvider.OpenAI:
                    _httpClient.BaseAddress = new Uri(_currentConfig.BaseUrl ?? ConfigUtil.GetAPIKey("OPEN_AI_BASE_URL"));
                    if (!string.IsNullOrEmpty(_currentConfig.ApiKey))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentConfig.ApiKey);
                    }
                    break;
                // Set base address for Ollama    
                case ApiProvider.Ollama:
                    _httpClient.BaseAddress = new Uri(_currentConfig.BaseUrl ?? ConfigUtil.GetAPIKey("OLLAMA_BASE_URL"));
                    break;
            }
            // Add a default User-Agent header for all requests
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AIService/1.0");
        }

        /// <summary>
        /// Executes a streaming chat query to the configured AI provider.
        /// </summary>
        /// <param name="systemPrompt">System prompt for the AI.</param>
        /// <param name="userPrompt">User's prompt or question.</param>
        /// <param name="temperature">Controls randomness of the response.</param>
        /// <param name="maxTokens">Maximum number of tokens in the response.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public async Task ExecuteStreamingQueryAsync(string systemPrompt, string userPrompt, double temperature = 1, int maxTokens = 1000, CancellationToken cancellationToken = default)
        {
            // IF _currentConfig is null, throw an exception
            if (_currentConfig == null)
                throw new InvalidOperationException("AI service not configured. Call ConfigureProvider first.");
            // if userPrompt is null or empty, throw an exception
            if (string.IsNullOrWhiteSpace(userPrompt))
                throw new ArgumentException("Prompt cannot be empty", nameof(userPrompt));

            // raise a status change event before starting the streaming query
            OnStatusChanged($"Starting streaming query to {_currentConfig.Provider}...");

            // Create the chat request object with provided parameters
            var request = new ChatRequest
            {
                UserPrompt = userPrompt,
                SystemPrompt = systemPrompt,
                Temperature = temperature,
                MaxTokens = maxTokens,
                Stream = true // Enable streaming
            };


            try
            {
                switch (_currentConfig.Provider)
                {
                    // Call Streaming method for Open AI provider
                    case ApiProvider.OpenAI:
                        await CallOpenAIStreaming(request, cancellationToken);
                        break;
                    // Call Streaming method for Ollama provider. TODO: These two methods can be combined and parameterised
                    case ApiProvider.Ollama:
                        await CallOllamaChatStreaming(request, cancellationToken);
                        break;
                    default:
                        // Unsupported provider
                        throw new Exception("Unsupported API provider for streaming");
                }
                // raise a status change event after successful streaming
                OnStatusChanged("Streaming completed successfully");
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                OnStatusChanged("Streaming was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during streaming
                OnStatusChanged($"Streaming failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Gets information about the current provider and model.
        /// </summary>
        /// <returns>Provider and model information string.</returns>
        public string GetProviderInfo()
        {
            // If no configuration is set, return a default message
            if (_currentConfig == null)
                return "Not configured";

            // Return formatted string with provider and model information
            return $"Provider: {_currentConfig.Provider}, Model: {_currentConfig.Model ?? "Default"}";
        }

        /// <summary>
        /// Raises the StatusChanged event.
        /// </summary>
        /// <param name="status">Status message.</param>
        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Raises the ContentReceived event.
        /// </summary>
        /// <param name="args">Streaming content event arguments.</param>
        protected virtual void OnContentReceived(StreamingContentEventArgs args)
        {
            ContentReceived?.Invoke(this, args);
        }

        /// <summary>
        /// Disposes the HTTP client and clears configuration.
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
            _currentConfig = null;
        }

        #endregion

        #region OpenAI Methods
        /// <summary>
        /// Calls the OpenAI chat API with streaming enabled and processes the response.
        /// </summary>
        /// <param name="request">Chat request details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task CallOpenAIStreaming(ChatRequest request, CancellationToken cancellationToken)
        {
            // Create the request body for OpenAI chat completions
            var requestBody = new
            {
                model = _currentConfig.Model ?? "gpt-3.5-turbo",
                messages = new[] { new { role = "system", content = request.SystemPrompt }, new { role = "user", content = request.UserPrompt } },
                temperature = 1,
                max_completion_tokens = request.MaxTokens,
                stream = true
            };

            // Serialize the request body to JSON
            var json = JsonSerializer.Serialize(requestBody);

            // Use the base URL from configuration or environment variable
            var baseUrl = _currentConfig.BaseUrl ?? ConfigUtil.GetAPIKey("OPEN_AI_BASE_URL");

            // Construct the endpoint URL for chat completions
            var endpoint = $"{baseUrl}/chat/completions";

            // Create the HTTP request message with the appropriate headers and content
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

            // Add the Authorization header with the API key
            httpRequest.Headers.Add("Authorization", $"Bearer {_currentConfig.ApiKey}");

            // Set the content type to application/json and add the serialized JSON body
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // initialize variables for response handling
            HttpResponseMessage response = null;
            Stream stream = null;
            StreamReader reader = null;

            try
            {
                // Send the HTTP request and get the response
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Check if the response was successful
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {response.StatusCode} - {errorResponse}");
                }

                // Read the response stream
                stream = await response.Content.ReadAsStreamAsync();
                // Create a StreamReader to read the response line by line
                reader = new StreamReader(stream);

                string line;
                // Read lines from the response stream until the end or cancellation is requested
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    // Read a line from the stream
                    line = await reader.ReadLineAsync();
                    // If the line is not null and starts with "data: ", process it
                    if (line != null && line.StartsWith("data: "))
                    {
                        // Extract the data part of the line
                        var data = line.Substring(6);

                        // If the data is "[DONE]", signal completion
                        if (data == "[DONE]")
                        {
                            // Raise the ContentReceived event with an empty content and IsComplete set to true
                            OnContentReceived(new StreamingContentEventArgs
                            {
                                Content = "",
                                IsComplete = true
                            });
                            // Break the loop as streaming is complete
                            break;
                        }

                        try
                        {
                            // Parse the JSON data to extract the content
                            using (var jsonDoc = JsonDocument.Parse(data))
                            {
                                // Get the root element of the JSON document
                                var root = jsonDoc.RootElement;

                                // Check if the root has a "choices" property and it contains at least one choice
                                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                                {
                                    // Get the first choice from the choices array
                                    var choice = choices[0];

                                    // Check if the choice has a "delta" property and it contains "content"
                                    if (choice.TryGetProperty("delta", out var delta))
                                    {
                                        // If the delta has a "content" property, extract its value
                                        if (delta.TryGetProperty("content", out var content))
                                        {
                                            // Get the content as a string
                                            var contentText = content.GetString();
                                            // If the content is not null or empty, raise the ContentReceived event
                                            if (!string.IsNullOrEmpty(contentText))
                                            {
                                                // Raise the ContentReceived event to show the streamed content but indicate it's not complete yet
                                                OnContentReceived(new StreamingContentEventArgs
                                                {
                                                    Content = contentText,
                                                    IsComplete = false
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed JSON
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Show error message if an exception occurs during streaming
                OnContentReceived(new StreamingContentEventArgs
                {
                    Content = ex.Message,
                    IsComplete = true
                });
            }
            finally
            {
                //dispose of resources to free up memory
                if (reader != null) reader.Dispose();
                if (stream != null) stream.Dispose();
                if (response != null) response.Dispose();
            }
        }

        #endregion

        #region Ollama Methods
        /// <summary>
        /// Calls the Ollama chat API with streaming enabled and processes the response.
        /// </summary>
        /// <param name="request">Chat request details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task CallOllamaChatStreaming(ChatRequest request, CancellationToken cancellationToken)
        {
            // create the request body for Ollama chat completions
            var requestBody = new
            {
                model = _currentConfig.Model ?? "llama3.2",
                messages = new[]
                {
                        new { role = "system", content = request.SystemPrompt },
                        new { role = "user", content = request.UserPrompt }
                    },
                stream = true,
                options = new
                {
                    temperature = request.Temperature,
                    num_predict = request.MaxTokens
                }
            };

            // serialize the request body to JSON
            var json = JsonSerializer.Serialize(requestBody);
            // Use the base URL from configuration or environment variable
            var baseUrl = _currentConfig.BaseUrl ?? ConfigUtil.GetAPIKey("OLLAMA_BASE_URL");
            // construct the endpoint URL for Ollama chat completions
            var endpoint = $"{baseUrl}/chat";

            // create the HTTP request message with the appropriate headers and content
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Variables to hold response and stream objects
            HttpResponseMessage response = null;           
            Stream stream = null;
            StreamReader reader = null;

            try
            {
                // Send the HTTP request and get the response
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                // Ensure the response was successful
                response.EnsureSuccessStatusCode();

                // Read the response stream
                stream = await response.Content.ReadAsStreamAsync();
                // Create a StreamReader to read the response line by line
                reader = new StreamReader(stream);

                string line;
                // Read lines from the response stream until the end or cancellation is requested
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    // Read a line from the stream
                    line = await reader.ReadLineAsync();
                    // If the line is not null or whitespace, process it
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try
                        {
                            // Parse the JSON line to extract the content
                            using (var jsonDoc = JsonDocument.Parse(line))
                            {
                                // Get the root element of the JSON document
                                JsonElement root = jsonDoc.RootElement;

                                // Check if the root has a "message" property and it contains "content"
                                if (root.TryGetProperty("message", out var message))
                                {
                                    if (message.TryGetProperty("content", out var content))
                                    {
                                        // Get the content as a string
                                        string contentText = content.GetString();
                                        // If the content is not null or empty, raise the ContentReceived event
                                        if (!string.IsNullOrEmpty(contentText))
                                        {
                                            // Raise the ContentReceived event to show the streamed content but indicate it's not complete yet
                                            OnContentReceived(new StreamingContentEventArgs
                                            {
                                                Content = contentText,
                                                IsComplete = false
                                            });
                                        }
                                    }
                                }

                                // Check if the root has a "done" property indicating completion
                                if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                                {
                                    // If "done" is true, signal that streaming is complete
                                    OnContentReceived(new StreamingContentEventArgs
                                    {
                                        Content = "",
                                        IsComplete = true
                                    });
                                    break;
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Continue on bad JSON line
                            continue;
                        }
                    }
                }
            }
            finally
            {
                // Dispose of resources to free up memory
                if (reader != null) reader.Dispose();
                if (stream != null) stream.Dispose();
                if (response != null) response.Dispose();
            }
        }

        #endregion

        #region Non-Streaming Methods for documentation

        /// <summary>
        /// Executes a non-streaming chat query to the configured AI provider and returns the response as a string.
        /// </summary>
        /// <param name="SystemPrompt">System prompt for the AI (instructions).</param>
        /// <param name="UserPrompt">User's prompt or question.</param>
        /// <param name="temperature">Controls randomness of the response.</param>
        /// <param name="maxTokens">Maximum number of tokens in the response.</param>
        /// <returns>AI response content as a string.</returns>
        public async Task<string> ExecuteQueryAsync(string SystemPrompt, string UserPrompt, double temperature = 1, int maxTokens = 1000)
        {
            // Ensure the service is configured
            if (_currentConfig == null)
                throw new InvalidOperationException("AI service not configured. Call ConfigureProvider first.");

            // Validate the user prompt
            if (string.IsNullOrWhiteSpace(UserPrompt))
                throw new ArgumentException("Prompt cannot be empty", nameof(UserPrompt));

            // Notify status before sending the query
            OnStatusChanged($"Sending query to {_currentConfig.Provider}...");

            // Build the chat request object
            var request = new ChatRequest
            {
                UserPrompt = UserPrompt,
                SystemPrompt = SystemPrompt,
                Temperature = temperature,
                MaxTokens = maxTokens
            };

            // Send the chat request and get the response
            var response = await SendChatAsync(request);

            // Handle the response
            if (response.IsSuccess)
            {
                OnStatusChanged($"Query completed successfully");
                return response.Content;
            }
            else
            {
                OnStatusChanged($"Query failed: {response.ErrorMessage}");
                throw new Exception(response.ErrorMessage);
            }
        }

        /// <summary>
        /// Sends a chat request to the configured provider and returns the response.
        /// Handles provider selection and error handling.
        /// </summary>
        /// <param name="request">Chat request details.</param>
        /// <returns>ChatResponse object containing result or error.</returns>
        private async Task<ChatResponse> SendChatAsync(ChatRequest request)
        {
            try
            {
                switch (_currentConfig.Provider)
                {
                    case ApiProvider.OpenAI:
                        return await CallOpenAI(request);
                    case ApiProvider.Ollama:
                        return await CallOllama(request);
                    default:
                        return new ChatResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Unsupported API provider"
                        };
                }
            }
            catch (HttpRequestException ex)
            {
                return new ChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
            catch (TaskCanceledException ex)
            {
                return new ChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Request timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Error calling {_currentConfig.Provider} API: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calls the OpenAI API for a non-streaming chat completion.
        /// </summary>
        /// <param name="request">Chat request details.</param>
        /// <returns>ChatResponse containing the AI's reply.</returns>
        private async Task<ChatResponse> CallOpenAI(ChatRequest request)
        {
            // Create a ChatClient instance with model and API key
            ChatClient client = new ChatClient(
                 _currentConfig.Model ?? "gpt-3.5-turbo",
                _currentConfig.ApiKey
                );

            // Request a chat completion from OpenAI
            ChatCompletion completion = client.CompleteChat(request.UserPrompt
                );

            // Return the response content
            return new ChatResponse
            {
                IsSuccess = true,
                Content = completion.Content[0].Text
            };
        }

        /// <summary>
        /// Calls the Ollama API for a non-streaming chat completion.
        /// </summary>
        /// <param name="request">Chat request details.</param>
        /// <returns>ChatResponse containing the AI's reply.</returns>
        private async Task<ChatResponse> CallOllama(ChatRequest request)
        {
            try
            {
                // Build the request payload for Ollama
                var requestData = new
                {
                    model = _currentConfig.Model ?? "llama3.2",
                    prompt = request.UserPrompt,
                    stream = false
                };

                // Serialize payload to JSON
                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var baseUrl = _currentConfig.BaseUrl ?? ConfigUtil.GetAPIKey("OLLAMA_BASE_URL");
                // Construct the endpoint URL for Ollama chat completions
                var endpoint = $"{baseUrl}/generate";
                // Send the HTTP POST request
                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                // Read and deserialize the response
                var responseJson = await response.Content.ReadAsStringAsync();
                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                return new ChatResponse
                {
                    IsSuccess = true,
                    Content = ollamaResponse?.response ?? "No response received"
                };

            }
            catch (Exception ex)
            {
                // Swallow exception and return default response
            }

            return new ChatResponse
            {
                IsSuccess = true,
                Content = "No response received"
            };
        }


        #endregion

    }
}
