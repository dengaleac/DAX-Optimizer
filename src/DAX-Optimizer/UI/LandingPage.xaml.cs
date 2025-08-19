using DAX_Optimizer.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MAT = Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices.AdomdClient;
using System.Windows.Forms.Design;
using DAX_Optimizer.AI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Markdig;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Data;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Win32;
using DAX_Optimizer.Utilities;
using System.Threading;
using System.Windows.Threading;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using System.Management;


namespace DAX_Optimizer.UI
{
    /// <summary>
    /// Interaction logic for LandingPage.xaml
    /// Main window for the DAX Optimizer UI.
    /// Handles Power BI instance discovery, connection, metadata loading, AI services, and documentation export.
    /// </summary>
    public partial class LandingPage : Window, INotifyPropertyChanged
    {
        #region Variables
        // Tabular server and database objects
        private MAT.Server _server;
        private MAT.Database _database;

        // Metadata items for UI binding
        private ObservableCollection<MetadataItem> _metadataItems;
        private MetadataItem _selectedItem;
        private string _connectionString;

        // Power BI instance management
        private ObservableCollection<PowerBIInstance> powerBIInstances;
        private PowerBIInstance selectedInstance;

        // AI service and streaming response management
        private AIService _aiService;
        private StringBuilder _currentResponse;
        private bool _updateScheduled = false;
        private object _lock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        #endregion

        #region Properties & Related Methods
        /// <summary>
        /// PropertyChanged event for INotifyPropertyChanged.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Collection of metadata items (tables, columns, measures) for display.
        /// </summary>
        public ObservableCollection<MetadataItem> MetadataItems
        {
            get => _metadataItems;
            set
            {
                _metadataItems = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Currently selected metadata item.
        /// </summary>
        public MetadataItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ItemProperties));
                OnPropertyChanged(nameof(DaxExpression));
            }
        }

        /// <summary>
        /// Properties of the selected metadata item for display.
        /// </summary>
        public Dictionary<string, string> ItemProperties
        {
            get
            {
                if (SelectedItem == null) return new Dictionary<string, string>();

                var properties = new Dictionary<string, string>
                {
                    ["Name"] = SelectedItem.Name,
                    ["Type"] = SelectedItem.Type.ToString(),
                    ["Data Type"] = SelectedItem.DataType,
                    ["Format"] = SelectedItem.Format ?? "General",
                    ["Sort By Column"] = SelectedItem.SortByColumn ?? "None"
                };

                if (!string.IsNullOrEmpty(SelectedItem.Description))
                    properties["Description"] = SelectedItem.Description;

                return properties;
            }
        }

        /// <summary>
        /// DAX expression of the selected item (if any).
        /// </summary>
        public string DaxExpression
        {
            get => SelectedItem?.Expression ?? "";
        }

        /// <summary>
        /// Raises PropertyChanged event for data binding.
        /// </summary>
        /// <param name="propertyName">Name of property changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Init functions

        /// <summary>
        /// Constructor. Initializes UI, Power BI instance discovery, and AI service.
        /// </summary>
        public LandingPage()
        {
            InitializeComponent();
            powerBIInstances = new ObservableCollection<PowerBIInstance>();
            cmbInstances.ItemsSource = powerBIInstances;
            DataContext = this;
            MetadataItems = new ObservableCollection<MetadataItem>();
            InitializeDiscoverPBIInstanceAsync();
            InitializeAIService();
        }

        /// <summary>
        /// Starts asynchronous discovery of Power BI Desktop instances.
        /// </summary>
        private async void InitializeDiscoverPBIInstanceAsync()
        {
            try
            {
                _ = DiscoverInstancesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes the AI service and subscribes to events.
        /// </summary>
        private void InitializeAIService()
        {
            _aiService = new AIService();

            // Subscribe to status updates
            _aiService.StatusChanged += OnAIServiceStatusChanged;
            _aiService.ContentReceived += OnContentReceived;

            // Default to Ollama (no API key needed)
            _aiService.ConfigureProvider(
                provider: ApiProvider.Ollama,
                model: "llama3.2"
            );
        }

        /// <summary>
        /// Disconnects from server while closing the window.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _server?.Disconnect();
            base.OnClosed(e);
        }

        /// <summary>
        /// Scrolls markdown viewer to bottom after content load.
        /// </summary>
        private void markdownViewer_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            dynamic doc = markdownViewer.Document;            
            doc.parentWindow.scrollTo(0, doc.body.scrollHeight);
        }
        #endregion

        #region PBI services

        /// <summary>
        /// Refreshes the list of Power BI Desktop instances.
        /// </summary>
        private async void btnRefreshInstances_Click(object sender, RoutedEventArgs e)
        {
            await DiscoverInstancesAsync();
        }

        /// <summary>
        /// Discovers running Power BI Desktop instances and updates UI.
        /// </summary>
        private async Task DiscoverInstancesAsync()
        {
            btnRefreshInstances.IsEnabled = false;
            btnConnect.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;

            try
            {
                var instances = await Task.Run(() => DiscoverPowerBIInstances());

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    powerBIInstances.Clear();
                    foreach (var instance in instances)
                    {
                        powerBIInstances.Add(instance);
                    }

                });
            }
            catch (Exception ex)
            {
                // TODO:Error handling needs to be handled
            }
            finally
            {
                btnRefreshInstances.IsEnabled = true;
                btnConnect.IsEnabled = true;
                progressBar.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Finds Power BI Desktop processes and their Analysis Services ports.
        /// </summary>
        private List<PowerBIInstance> DiscoverPowerBIInstances()
        {
            var instances = new List<PowerBIInstance>();

            try
            {
                //Find Analysis Services processes                
                var asProcesses = Process.GetProcessesByName("msmdsrv");

                foreach (var asProcess in asProcesses)
                {
                    try
                    {
                        // Get Parent Power BI Desktop process
                        var pbiProcess = GetParentProcess(asProcess);

                        // Get the port used by this Power BI instance
                        var ports = GetPortsForProcess(asProcess.Id);

                        foreach (var port in ports)
                        {
                            //Get instances of the Power BI Desktop
                            var instance = new PowerBIInstance
                            {
                                ProcessId = pbiProcess.Id,
                                ProcessName = pbiProcess.ProcessName,
                                Port = port,
                                Server = $"localhost:{port}",
                                StartTime = pbiProcess.StartTime,
                                WindowTitle = GetProcessWindowTitle(pbiProcess.Id)
                            };

                            // Try to get database information
                            try
                            {
                                //Get Databases from Power BI Instance
                                var databases = GetDatabasesForInstance(instance.Server);
                                instance.Databases = databases;
                                if (databases.Any())
                                {
                                    instance.DefaultDatabase = databases.First();
                                }
                            }
                            catch
                            {
                                // Continue even if we can't get database info
                            }

                            instances.Add(instance);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing PBI instance {asProcess.Id}: {ex.Message}");
                    }
                }               
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DiscoverPowerBIInstances: {ex.Message}");
            }

            return instances.OrderBy(i => i.Port).ToList();
        }

        public static Process GetParentProcess(Process process)
        {
            try
            {
                using (var query = new ManagementObjectSearcher(
                    $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}"))
                {
                    foreach (var obj in query.Get())
                    {
                        var parentId = Convert.ToInt32(obj["ParentProcessId"]);
                        return Process.GetProcessById(parentId);
                    }
                }
            }
            catch
            {
                // Handle exceptions (e.g., process exited)
            }

            return null;
        }

        /// <summary>
        /// Gets Analysis Services ports for a given process ID.
        /// </summary>
        private List<int> GetPortsForProcess(int processId)
        {
            var ports = new List<int>();

            try
            {
                // Get TCP connections for the process
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnections = properties.GetActiveTcpListeners();

                // Cross-reference with netstat data
                var netstatPorts = GetNetstatPortsForProcess(processId);
                ports.AddRange(netstatPorts);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting ports for process {processId}: {ex.Message}");
            }

            return ports.Distinct().ToList();
        }

        /// <summary>
        /// Uses netstat to find ports used by a process.
        /// </summary>
        private List<int> GetNetstatPortsForProcess(int processId)
        {
            var ports = new List<int>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var lines = output.Split('\n');

                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5 && parts[1].StartsWith("127.0.0.1:") && parts[4].Replace("\r","").Replace("\n","") == processId.ToString())
                        {
                            var portPart = parts[1].Split(':')[1];
                            if (int.TryParse(portPart, out int port))
                            {
                                ports.Add(port);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error running netstat: {ex.Message}");
            }

            return ports;
        }
        
        /// <summary>
        /// Gets list of databases for a given Analysis Services server.
        /// </summary>
        private List<string> GetDatabasesForInstance(string server)
        {
            var databases = new List<string>();

            try
            {
                var connectionString = $"Provider=MSOLAP;Data Source={server};Connect Timeout=5;";
                using (var connection = new AdomdConnection(connectionString))
                {
                    connection.Open();

                    var catalogs = connection.GetSchemaDataSet("DBSCHEMA_CATALOGS", null);
                    foreach (DataRow row in catalogs.Tables[0].Rows)
                    {
                        var catalogName = row["CATALOG_NAME"].ToString();
                        if (!string.IsNullOrEmpty(catalogName) && catalogName != "master")
                        {
                            databases.Add(catalogName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting databases for {server}: {ex.Message}");
            }

            return databases;
        }

        /// <summary>
        /// Gets the window title for a process by ID.
        /// </summary>
        private string GetProcessWindowTitle(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return string.IsNullOrEmpty(process.MainWindowTitle) ?
                    $"Power BI Desktop (PID: {processId})" :
                    process.MainWindowTitle;
            }
            catch
            {
                return $"Power BI Desktop (PID: {processId})";
            }
        }

        /// <summary>
        /// Connects to a Power BI instance and loads metadata.
        /// </summary>
        private async Task ConnectToInstance(string server, string database)
        {
            _server = new MAT.Server();

            var connectionString = $"Provider=MSOLAP;Data Source={server};Initial Catalog={database};";

            using (var connection = new AdomdConnection(connectionString))
            {
                connection.Open(); // Synchronous call wrapped in Task.Run

                // Test the connection by getting server properties
                var serverName = connection.ServerVersion;

                try
                {
                    _connectionString = $"Data Source={connection};";
                    _server.Connect(connectionString);

                    if (_server.Databases.Count > 0)
                    {
                        _database = _server.Databases[0]; // Get the first (usually only) database
                    }
                }
                catch
                {
                    // Try next connection
                }

                if (_database == null)
                {
                    throw new InvalidOperationException("Could not connect to Power BI Desktop. Please ensure Power BI Desktop is running with a model loaded.");
                }
                else
                {
                    await LoadMetadata();
                }
            }

            // Update UI on the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {   // Store connection string for queries
                this.Tag = connectionString;
            });
        }

        /// <summary>
        /// Loads metadata (tables, columns, measures) from the connected model.
        /// </summary>
        private async Task LoadMetadata()
        {
            await Task.Run(() =>
            {
                var items = new List<MetadataItem>();

                foreach (MAT.Table table in _database.Model.Tables)
                {
                    // Add table
                    var tableItem = new MetadataItem
                    {
                        Name = table.Name,
                        Type = MetadataItemType.Table,
                        Icon = "📊",
                        Parent = null,
                        Description = table.Description
                    };

                    tableItem.Expression = TOM_Utilities.GetTableMScript(table);

                    items.Add(tableItem);

                    // Add columns
                    foreach (MAT.Column column in table.Columns)
                    {
                        if (column.Type == MAT.ColumnType.RowNumber) continue; // Skip system columns

                        var columnItem = new MetadataItem
                        {
                            Name = column.Name,
                            Type = MetadataItemType.Column,
                            Icon = "📋",
                            Parent = tableItem,
                            DataType = column.DataType.ToString(),
                            Format = column.FormatString,
                            SortByColumn = column.SortByColumn?.Name,
                            Description = column.Description,
                            Expression = column.Type == MAT.ColumnType.Calculated ? ((MAT.CalculatedColumn)column).Expression : null
                        };
                        items.Add(columnItem);
                    }

                    // Add measures
                    foreach (MAT.Measure measure in table.Measures)
                    {
                        var measureItem = new MetadataItem
                        {
                            Name = measure.Name,
                            Type = MetadataItemType.Measure,
                            Icon = "🔢",
                            Parent = tableItem,
                            DataType = measure.DataType.ToString(),
                            Format = measure.FormatString,
                            Description = measure.Description,
                            Expression = measure.Expression
                        };
                        items.Add(measureItem);
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    MetadataItems.Clear();
                    foreach (var item in items)
                    {
                        MetadataItems.Add(item);
                    }
                });
            });
        }

        /// <summary>
        /// Handles selection change for Power BI instance combo box.
        /// Updates database list and enables connect button.
        /// </summary>
        private void cmbInstances_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedInstance = cmbInstances.SelectedItem as PowerBIInstance;

            if (selectedInstance != null)
            {
                cmbDatabases.ItemsSource = selectedInstance.Databases;
                if (!string.IsNullOrEmpty(selectedInstance.DefaultDatabase))
                {
                    cmbDatabases.SelectedItem = selectedInstance.DefaultDatabase;
                }
                btnConnect.IsEnabled = true;
            }
            else
            {
                btnConnect.IsEnabled = false;
                cmbDatabases.ItemsSource = null;
            }
        }

        /// <summary>
        /// Refreshes metadata from the connected model.
        /// </summary>
        private async void RefreshMetadata_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadMetadata();
                MessageBox.Show("Metadata refreshed successfully!", "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Refresh failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Connects to the selected Power BI instance and database.
        /// </summary>
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedInstance == null)
            {
                MessageBox.Show("Please select a Power BI instance.", "No Instance Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedDatabase = cmbDatabases.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedDatabase))
            {
                MessageBox.Show("Please select a database.", "No Database Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await ConnectToInstance(selectedInstance.Server, selectedDatabase);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Documentation Services

        private async void ExportDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            string output = string.Empty;
            try
            {
                _aiService.ContentReceived -= OnContentReceived;
                _aiService.ContentReceived += OnContentDocReceived;
                // Show save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*",
                    Title = "Save Metadata Documentation",
                    FileName = $"MetadataDocumentation_{DateTime.Now:yyyyMMdd_HHmmss}.html"
                };

                if (saveFileDialog.ShowDialog() == true)
                {

                    ExpressionModel _expressionModel = GenerateExpressionModel(_database.Model);

                    
                    try
                    {

                        string expressionJson = JsonSerializer.Serialize(_expressionModel, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        });

                        if (string.IsNullOrWhiteSpace(expressionJson))
                        {
                            return;
                        }

                        _currentResponse = new StringBuilder();

                        // Configure AI with system promptContentReceived
                        var systemPrompt = AIPromptBuilder.BuildSystemPromptToExplainDocumenation();
                        var userPrompt = AIPromptBuilder.BuildUserPromptForDocumentation(expressionJson, "");

                        // Send to AI service
                        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

                        try
                        {
                            //await _aiService.ExecuteStreamingQueryAsync(systemPrompt, userPrompt, 0.7, 1000, _cancellationTokenSource.Token);
                            
                            output = await _aiService.ExecuteQueryAsync(systemPrompt, userPrompt, 0.7, 1000);

                           
                        }
                        catch (OperationCanceledException)
                        {
                            MessageBox.Show("\n\n[Explanation cancelled by user.]\n\n");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"\n\n[Error: {ex.Message}]\n\n");
                        }
                        finally
                        {
                            //markdownViewer.NavigateToString(ConvertToHTML(_currentResponse.ToString()));
                        
                            _cancellationTokenSource?.Dispose();
                        }
                        


                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error while explaining expression: {ex.Message}");
                    }

                    try
                    {
                        _expressionModel = JsonSerializer.Deserialize<ExpressionModel>(output);
                    }
                    catch(Exception ex)
                    {

                    }

                    

                    // Export to HTML
                    var exporter = new Utilities.MetadataHTMLExporter();
                    exporter.ExportToHtml(_database.Model, _expressionModel, saveFileDialog.FileName);

                    // Show success message and offer to open the file
                    var result = MessageBox.Show(
                        $"Documentation exported successfully to:\n{saveFileDialog.FileName}\n\nWould you like to open the file now?",
                        "Export Complete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = saveFileDialog.FileName,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting documentation: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _aiService.ContentReceived -= OnContentDocReceived;
            _aiService.ContentReceived += OnContentReceived;
        }
               
        private ExpressionModel GenerateExpressionModel(MAT.Model model)
        {
            ExpressionModel _expressionModel = new ExpressionModel();

            foreach (var table in model.Tables)
            {
                var expressionTable = new ExpressionTable
                {
                    Name = table.Name
                };
                foreach (var measure in table.Measures)
                {
                    var expressionMeasure = new ExpressionMeasure
                    {
                        Name = measure.Name,
                        Expression = measure.Expression
                    };
                    expressionTable.Measures.Add(expressionMeasure);
                }
                _expressionModel.Tables.Add(expressionTable);
            }
            return _expressionModel;
        }
               
        #endregion

        #region AI services
        /// <summary>
        /// Handles status updates from the AI service.
        /// </summary>
        private void OnAIServiceStatusChanged(object sender, string status)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Content = status;
            });
        }

        /// <summary>
        /// Handles streaming content received from the AI service.
        /// Updates markdown viewer with streamed content.
        /// </summary>
        private void OnContentReceived(object sender, StreamingContentEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!e.IsComplete)
                {
                    _currentResponse.Append(e.Content);
                }
                else
                {
                    _currentResponse.Append("\n\n");
                    StatusLabel.Content = "Ready";
                }
                lock (_lock)
                {
                    if (!_updateScheduled)
                    {
                        _updateScheduled = true;
                        Task.Delay(300).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                markdownViewer.NavigateToString(ConvertToHTML(_currentResponse.ToString()));
                                lock (_lock)
                                {
                                    _updateScheduled = false;
                                }
                            });
                        });
                    }
                }
            }, DispatcherPriority.Background);
        }

        private void OnContentDocReceived(object sender, StreamingContentEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!e.IsComplete)
                {
                    _currentResponse.Append(e.Content);
                }
                else
                {
                    _currentResponse.Append("\n\n");                 
                }                
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Handles AI provider selection change.
        /// Configures AI service for selected provider and model.
        /// </summary>
        private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_aiService == null || cbAIProvider.SelectedItem == null)
                return;

            if (cbAIProvider.SelectedItem is ComboBoxItem selected)
            {
                string provider = selected.Content.ToString().Split('-')[0];
                string model = selected.Content.ToString().Replace(provider + "-", "").Trim();

                switch (provider)
                {
                    case "OpenAI":
                        _aiService.ConfigureProvider(
                            provider: ApiProvider.OpenAI,
                            apiKey: ConfigUtil.GetAPIKey("OPENAI_API_KEY"),
                            model: model
                        );
                        break;

                    case "Ollama":
                        _aiService.ConfigureProvider(
                            provider: ApiProvider.Ollama,
                            model: model
                        );
                        break;
                }
            }
        }

        /// <summary>
        /// Optimizes the selected DAX expression using the AI service.
        /// </summary>
        private async void btnOptimizeDAX_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtExpression.Text))
                {
                    MessageBox.Show("Please select a Table, Measure or calculated column to optimize their expression");
                    return;
                }

                markdownViewer.NavigateToString(ConvertToHTML("Loading..."));
                _currentResponse = new StringBuilder();

                // Configure AI with system prompt
                var systemPrompt = AIPromptBuilder.BuildSystemPromptToOptimize();
                var userPrompt = AIPromptBuilder.BuildUserPrompt(txtExpression.Text, "");

                // Combine prompts
                var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";

                // Send to AI service
                _cancellationTokenSource = new CancellationTokenSource();

                try
                {
                    await _aiService.ExecuteStreamingQueryAsync(systemPrompt, userPrompt, 0.7, 1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _currentResponse.Append("\n\n[Optimization cancelled by user.]\n\n");
                }
                catch (Exception ex)
                {
                    _currentResponse.Append($"\n\n[Error: {ex.Message}]\n\n");
                }
                finally
                {
                    markdownViewer.NavigateToString(ConvertToHTML(_currentResponse.ToString()));
                    _cancellationTokenSource?.Dispose();
                }
            }
            catch (Exception ex)
            {
                markdownViewer.NavigateToString(ConvertToHTML($"Error while optimizing expression: {ex.Message}"));
            }
        }

        /// <summary>
        /// Explains the selected DAX expression using the AI service.
        /// </summary>
        private async void btnExplainDAX_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtExpression.Text))
                {
                    MessageBox.Show("Please select a Table, Measure or calculated column to optimize their expression");
                    return;
                }

                markdownViewer.NavigateToString(ConvertToHTML("Loading..."));
                _currentResponse = new StringBuilder();

                // Configure AI with system promptContentReceived
                var systemPrompt = AIPromptBuilder.BuildSystemPromptToExplain();
                var userPrompt = AIPromptBuilder.BuildUserPrompt(txtExpression.Text, "");
                                
                // Send to AI service
                _cancellationTokenSource = new CancellationTokenSource();

                try
                {
                    await _aiService.ExecuteStreamingQueryAsync(systemPrompt, userPrompt, 0.7, 1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _currentResponse.Append("\n\n[Explanation cancelled by user.]\n\n");
                }
                catch (Exception ex)
                {
                    _currentResponse.Append($"\n\n[Error: {ex.Message}]\n\n");
                }
                finally
                {
                    markdownViewer.NavigateToString(ConvertToHTML(_currentResponse.ToString()));
                    _cancellationTokenSource?.Dispose();
                }
            }
            catch (Exception ex)
            {
                markdownViewer.NavigateToString(ConvertToHTML($"Error while explaining expression: {ex.Message}"));
            }
        }

        /// <summary>
        /// Converts markdown text to HTML for display in the markdown viewer.
        /// </summary>
        /// <param name="markdown">Markdown text.</param>
        /// <returns>HTML string.</returns>
        private string ConvertToHTML(string markdown)
        {
            return $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <style>
                            body {{ font-family: Segoe UI, sans-serif; line-height: 1.6; font-size:14px; }}
                            h1, h2, h3 {{ color: #333; }}
                            pre {{ background-color: #f4f4f4; padding: 10px; border-radius: 5px; }}
                            code {{ background-color: #f4f4f4; padding: 2px 5px; border-radius: 3px; }}
                        </style>
                    </head>
                    <body>
                        {Markdown.ToHtml(markdown)}
                    </body>
                    </html>
                ";
        }
        #endregion
    }
}
