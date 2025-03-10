using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Threading;
using System.Linq;
using System.Web;
using WpfApp1.Models;
using WpfApp1.Services;
using System.Collections.ObjectModel;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly HistoryManager _historyManager;
        private List<RequestModel> _requests;

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _historyManager = new HistoryManager();
            _requests = LoadRequests(); // 加载保存的请求记录

            InitializeEvents();
            InitializeGrids();
            LoadHistories();

            // 为 Params 和 Headers DataGrid 添加 CellEditEnding 事件处理
            gridQueryParams.CellEditEnding += DataGrid_CellEditEnding;
            gridPathParams.CellEditEnding += DataGrid_CellEditEnding;
            gridHeaders.CellEditEnding += DataGrid_CellEditEnding;
        }

        private void InitializeEvents()
        {
            btnNewRequest.Click += BtnNewRequest_Click;
            btnSend.Click += BtnSend_Click;
            txtSearch.TextChanged += TxtSearch_TextChanged;
            treeRequests.SelectedItemChanged += TreeRequests_SelectedItemChanged;
            listHistory.SelectionChanged += ListHistory_SelectionChanged;
            
            // 添加右键菜单事件
            treeRequests.MouseRightButtonUp += TreeRequests_MouseRightButtonUp;
            
            // 响应工具栏按钮事件
            btnCopyResponse.Click += BtnCopyResponse_Click;
            btnFormatJson.Click += BtnFormatJson_Click;
            
            // 添加参数按钮事件
            btnAddQueryParam.Click += BtnAddQueryParam_Click;
            btnAddPathParam.Click += BtnAddPathParam_Click;
            btnAddHeader.Click += BtnAddHeader_Click;
            
            // 为 DataGrid 添加 CellEditEnding 事件处理
            gridQueryParams.CellEditEnding += DataGrid_CellEditEnding;
            gridPathParams.CellEditEnding += DataGrid_CellEditEnding;
            gridHeaders.CellEditEnding += DataGrid_CellEditEnding;
        }

        private void InitializeGrids()
        {
            gridQueryParams.ItemsSource = new ObservableCollection<RequestParameter>();
            gridPathParams.ItemsSource = new ObservableCollection<RequestParameter>();
            gridHeaders.ItemsSource = new ObservableCollection<RequestHeader>();
            
            // 为 Query 和 Path 参数 DataGrid 添加 CellEditEnding 事件处理
            gridQueryParams.CellEditEnding += DataGrid_CellEditEnding;
            gridPathParams.CellEditEnding += DataGrid_CellEditEnding;
            gridHeaders.CellEditEnding += DataGrid_CellEditEnding;
        }

        private void TreeRequests_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is RequestModel request)
            {
                txtUrl.Text = request.Url;
                cboMethod.SelectedItem = cboMethod.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == request.Method);
                txtBody.Text = request.Body;
                
                // 更新Query参数
                var queryParams = request.Parameters.Where(p => p.ParameterType == ParameterType.Query).ToList();
                gridQueryParams.ItemsSource = new ObservableCollection<RequestParameter>(queryParams);
                
                // 更新Path参数
                var pathParams = request.Parameters.Where(p => p.ParameterType == ParameterType.Path).ToList();
                gridPathParams.ItemsSource = new ObservableCollection<RequestParameter>(pathParams);
                
                // 更新Headers
                gridHeaders.ItemsSource = new ObservableCollection<RequestHeader>(request.Headers);
            }
        }

        private void TreeRequests_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = GetTreeViewItemAtPoint(e.GetPosition(treeRequests));
            if (item != null)
            {
                item.IsSelected = true;
                
                var contextMenu = new ContextMenu();
                var menuItem = new MenuItem { Header = "重命名" };
                menuItem.Click += (s, args) => StartRename(item);
                contextMenu.Items.Add(menuItem);
                
                contextMenu.IsOpen = true;
            }
        }

        private TreeViewItem GetTreeViewItemAtPoint(Point point)
        {
            var element = treeRequests.InputHitTest(point) as DependencyObject;
            while (element != null)
            {
                if (element is TreeViewItem item)
                    return item;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private void StartRename(TreeViewItem item)
        {
            if (item?.Tag is RequestModel request)
            {
                var stackPanel = item.Header as StackPanel;
                if (stackPanel != null)
                {
                    var nameTextBlock = stackPanel.Children[0] as TextBlock;
                    if (nameTextBlock != null)
                    {
                        var textBox = new TextBox
                        {
                            Text = nameTextBlock.Text,
                            FontSize = nameTextBlock.FontSize,
                            Margin = new Thickness(0),
                            Padding = new Thickness(0)
                        };

                        textBox.KeyDown += (s, args) =>
                        {
                            if (args.Key == System.Windows.Input.Key.Enter)
                            {
                                FinishRename(item, textBox, request);
                                SaveRequests();
                            }
                            else if (args.Key == System.Windows.Input.Key.Escape)
                            {
                                CancelRename(item, request);
                            }
                        };

                        textBox.LostFocus += (s, args) =>
                        {
                            FinishRename(item, textBox, request);
                            SaveRequests();
                        };

                        stackPanel.Children.RemoveAt(0);
                        stackPanel.Children.Insert(0, textBox);
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private void BtnNewRequest_Click(object sender, RoutedEventArgs e)
        {
            var request = new RequestModel
            {
                Name = $"新建请求 {_requests.Count + 1}"
            };

            _requests.Add(request);
            SaveRequests(); // 保存请求记录
            RefreshRequestTree();

            // 找到新建的TreeViewItem并选中
            TreeViewItem newItem = null;
            foreach (TreeViewItem item in treeRequests.Items)
            {
                if (item.Tag == request)
                {
                    newItem = item;
                    item.IsSelected = true;
                    break;
                }
            }

            if (newItem != null)
            {
                // 获取名称TextBlock并开始编辑
                var stackPanel = newItem.Header as StackPanel;
                if (stackPanel != null)
                {
                    var nameTextBlock = stackPanel.Children[0] as TextBlock;
                    if (nameTextBlock != null)
                    {
                        // 创建TextBox替换TextBlock
                        var textBox = new TextBox
                        {
                            Text = nameTextBlock.Text,
                            FontSize = nameTextBlock.FontSize,
                            Margin = new Thickness(0),
                            Padding = new Thickness(0)
                        };

                        textBox.KeyDown += (s, args) =>
                        {
                            if (args.Key == System.Windows.Input.Key.Enter)
                            {
                                FinishRename(newItem, textBox, request);
                                SaveRequests(); // 保存重命名后的记录
                            }
                            else if (args.Key == System.Windows.Input.Key.Escape)
                            {
                                CancelRename(newItem, request);
                            }
                        };

                        textBox.LostFocus += (s, args) =>
                        {
                            FinishRename(newItem, textBox, request);
                            SaveRequests(); // 保存重命名后的记录
                        };

                        // 先移除旧的TextBlock，再添加新的TextBox
                        stackPanel.Children.RemoveAt(0);
                        stackPanel.Children.Insert(0, textBox);
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private void FinishRename(TreeViewItem item, TextBox textBox, RequestModel request)
        {
            var stackPanel = item.Header as StackPanel;
            if (stackPanel != null)
            {
                string newName = textBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    newName = request.Name; // 如果为空，恢复原名称
                }

                request.Name = newName;
                var nameTextBlock = new TextBlock
                {
                    Text = newName,
                    FontSize = textBox.FontSize
                };

                // 先移除旧的TextBox，再添加新的TextBlock
                stackPanel.Children.RemoveAt(0);
                stackPanel.Children.Insert(0, nameTextBlock);
            }
        }

        private void CancelRename(TreeViewItem item, RequestModel request)
        {
            var stackPanel = item.Header as StackPanel;
            if (stackPanel != null)
            {
                var nameTextBlock = new TextBlock
                {
                    Text = request.Name,
                    FontSize = 13
                };

                // 先移除旧的TextBox，再添加新的TextBlock
                stackPanel.Children.RemoveAt(0);
                stackPanel.Children.Insert(0, nameTextBlock);
            }
        }

        private void LoadHistories()
        {
            listHistory.ItemsSource = _historyManager.GetHistories();
        }

        private void ListHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listHistory.SelectedItem is RequestHistory history)
            {
                // 加载历史记录到界面
                cboMethod.Text = history.Method;
                txtUrl.Text = history.Url;
                txtBody.Text = history.Body;
                gridQueryParams.ItemsSource = history.Parameters.Where(p => p.ParameterType == ParameterType.Query).ToList();
                gridPathParams.ItemsSource = history.Parameters.Where(p => p.ParameterType == ParameterType.Path).ToList();
                gridHeaders.ItemsSource = history.Headers;
                txtResponse.Text = FormatJson(history.Response);
                
                // 更新状态栏
                UpdateResponseStatusBarFromHistory(history);
            }
        }
        
        private void UpdateResponseStatusBarFromHistory(RequestHistory history)
        {
            // 更新状态码
            txtStatusCode.Text = $"状态码: {history.StatusCode}";
            
            // 设置状态码颜色
            if (history.StatusCode >= 200 && history.StatusCode < 300)
            {
                txtStatusCode.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString("#4CAF50")); // 绿色
            }
            else if (history.StatusCode >= 400)
            {
                txtStatusCode.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString("#F44336")); // 红色
            }
            else
            {
                txtStatusCode.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FF9800")); // 橙色
            }
            
            // 尝试从历史记录中获取内容类型
            string contentType = "-";
            var contentTypeHeader = history.Headers.FirstOrDefault(h => 
                h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            if (contentTypeHeader != null)
            {
                contentType = contentTypeHeader.Value;
            }
            txtContentType.Text = $"Content-Type: {contentType}";
            
            // 更新响应时间
            txtResponseTime.Text = $"响应时间: {history.ExecutionTime}ms";
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = txtUrl.Text;
                
                // 处理URL中的Path参数
                string processedUrl = ProcessPathParameters(url);
                
                // 添加Query参数
                string finalUrl = AppendUrlParameters(processedUrl);
                
                var method = new HttpMethod(cboMethod.Text);
                var request = new HttpRequestMessage(method, finalUrl);
                var startTime = DateTime.Now;

                // 添加请求头
                foreach (var header in GetEnabledHeaders())
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                // 添加请求体
                if (method != HttpMethod.Get && !string.IsNullOrEmpty(txtBody.Text))
                {
                    // 获取选择的内容类型
                    string contentType = "application/json";
                    if (cboContentType.SelectedItem is ComboBoxItem selectedItem)
                    {
                        contentType = selectedItem.Content.ToString();
                    }
                    
                    // 创建带有内容类型的StringContent
                    request.Content = new StringContent(txtBody.Text, System.Text.Encoding.UTF8, contentType);
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                var executionTime = (long)(DateTime.Now - startTime).TotalMilliseconds;
                
                // 更新响应内容
                txtResponse.Text = FormatJson(content);
                
                // 更新状态栏信息
                UpdateResponseStatusBar(response, executionTime);

                // 获取当前选中的请求名称
                string requestName = "";
                if (treeRequests.SelectedItem is TreeViewItem selectedTreeItem && 
                    selectedTreeItem.Tag is RequestModel selectedRequest)
                {
                    requestName = selectedRequest.Name;
                }

                // 保存历史记录
                var history = new RequestHistory
                {
                    Name = requestName,
                    Method = cboMethod.Text,
                    Url = finalUrl,
                    Parameters = gridQueryParams.Items.OfType<RequestParameter>().Concat(gridPathParams.Items.OfType<RequestParameter>()).ToList(),
                    Headers = gridHeaders.Items.OfType<RequestHeader>().ToList(),
                    Body = txtBody.Text,
                    Response = content,
                    StatusCode = (int)response.StatusCode,
                    ExecutionTime = executionTime
                };
                _historyManager.SaveHistory(history);
                LoadHistories(); // 刷新历史记录列表
            }
            catch (Exception ex)
            {
                MessageBox.Show($"请求失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 清空状态栏
                txtStatusCode.Text = "状态码: -";
                txtContentType.Text = "Content-Type: -";
                txtResponseTime.Text = "响应时间: -";
            }
        }
        
        private void UpdateResponseStatusBar(HttpResponseMessage response, long executionTime)
        {
            // 更新状态码
            txtStatusCode.Text = $"状态码: {(int)response.StatusCode} {response.StatusCode}";
            
            // 设置状态码颜色
            if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
            {
                txtStatusCode.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString("#4CAF50")); // 绿色
            }
            else if ((int)response.StatusCode >= 400)
            {
                txtStatusCode.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString("#F44336")); // 红色
            }
            else
            {
                txtStatusCode.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FF9800")); // 橙色
            }
            
            // 更新内容类型
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "-";
            txtContentType.Text = $"Content-Type: {contentType}";
            
            // 更新响应时间
            txtResponseTime.Text = $"响应时间: {executionTime}ms";
        }

        private string ProcessPathParameters(string url)
        {
            var pathParams = GetEnabledPathParameters();
            if (!pathParams.Any())
                return url;
        
            string processedUrl = url;
            foreach (var param in pathParams)
            {
                // 替换URL中的{参数名}格式
                processedUrl = processedUrl.Replace($"{{{param.Key}}}", Uri.EscapeDataString(param.Value));
            }
            
            return processedUrl;
        }

        private string AppendUrlParameters(string url)
        {
            var parameters = GetEnabledQueryParameters();
            if (!parameters.Any())
                return url;

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach (var param in parameters)
            {
                query[param.Key] = param.Value;
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }
        
        private IEnumerable<RequestParameter> GetEnabledQueryParameters()
        {
            return ((ObservableCollection<RequestParameter>)gridQueryParams.ItemsSource)
                .Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key));
        }

        private IEnumerable<RequestParameter> GetEnabledPathParameters()
        {
            return ((ObservableCollection<RequestParameter>)gridPathParams.ItemsSource)
                .Where(p => !string.IsNullOrWhiteSpace(p.Key));
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RefreshRequestTree();
        }

        private void RefreshRequestTree()
        {
            treeRequests.Items.Clear();
            var searchText = txtSearch.Text.ToLower();

            foreach (var request in _requests)
            {
                if (string.IsNullOrEmpty(searchText) || 
                    request.Name.ToLower().Contains(searchText))
                {
                    var item = new TreeViewItem
                    {
                        Header = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(0, 4, 0, 4),
                            Children = 
                            {
                                new TextBlock 
                                { 
                                    Text = request.Name,
                                    FontSize = 13
                                },
                                new TextBlock 
                                { 
                                    Text = request.Description,
                                    FontSize = 12,
                                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575")),
                                    Margin = new Thickness(0, 4, 0, 0),
                                    Visibility = string.IsNullOrEmpty(request.Description) ? Visibility.Collapsed : Visibility.Visible
                                }
                            }
                        },
                        Tag = request
                    };
                    treeRequests.Items.Add(item);
                }
            }
        }

        private IEnumerable<RequestHeader> GetEnabledHeaders()
        {
            return gridHeaders.Items.OfType<RequestHeader>()
                .Where(h => h.IsEnabled);
        }

        private string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;
                
            try
            {
                // 检查是否是JSON格式
                if (IsJsonString(json))
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var jsonObject = JsonSerializer.Deserialize<JsonElement>(json);
                    return JsonSerializer.Serialize(jsonObject, options);
                }
            }
            catch (Exception)
            {
                // 如果格式化失败，返回原始内容
            }
            
            return json;
        }
        
        private bool IsJsonString(string text)
        {
            text = text.Trim();
            return (text.StartsWith("{") && text.EndsWith("}")) || // 对象
                   (text.StartsWith("[") && text.EndsWith("]"));   // 数组
        }

        private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtResponse.Text))
            {
                Clipboard.SetText(txtResponse.Text);
                MessageBox.Show("响应内容已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void BtnFormatJson_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtResponse.Text))
            {
                txtResponse.Text = FormatJson(txtResponse.Text);
            }
        }

        private List<RequestModel> LoadRequests()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var filePath = Path.Combine(appData, "WpfApp1", "requests.json");
                
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<RequestModel>>(json) ?? new List<RequestModel>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载请求记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return new List<RequestModel>();
        }

        private void SaveRequests()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dirPath = Path.Combine(appData, "WpfApp1");
                var filePath = Path.Combine(dirPath, "requests.json");
                
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
                
                var json = JsonSerializer.Serialize(_requests, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存请求记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var dataGrid = (DataGrid)sender;
                var currentCell = e.Column.GetCellContent(e.Row).Parent as DataGridCell;
                
                if (currentCell != null)
                {
                    // 如果是Key列，跳转到Value列
                    if (e.Column.DisplayIndex == 1) // Key列
                    {
                        var textBox = e.EditingElement as TextBox;
                        if (!string.IsNullOrWhiteSpace(textBox?.Text))
                        {
                            // 使用Dispatcher延迟执行，确保当前编辑完成
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                // 移动到Value列
                                var nextColumn = dataGrid.Columns[2]; // Value列
                                dataGrid.CurrentCell = new DataGridCellInfo(e.Row.Item, nextColumn);
                                dataGrid.BeginEdit();
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                    // 如果是类型列，跳转到说明列
                    else if (e.Column.DisplayIndex == 3) // 类型列
                    {
                        // 使用Dispatcher延迟执行，确保当前编辑完成
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 移动到说明列
                            var nextColumn = dataGrid.Columns[4]; // 说明列
                            dataGrid.CurrentCell = new DataGridCellInfo(e.Row.Item, nextColumn);
                            dataGrid.BeginEdit();
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }
        }

        private void BtnAddQueryParam_Click(object sender, RoutedEventArgs e)
        {
            var parameters = (ObservableCollection<RequestParameter>)gridQueryParams.ItemsSource;
            parameters.Add(new RequestParameter { ParameterType = ParameterType.Query, Type = "string" });
            gridQueryParams.SelectedIndex = parameters.Count - 1;
            gridQueryParams.Focus();
            gridQueryParams.BeginEdit();
        }

        private void BtnAddPathParam_Click(object sender, RoutedEventArgs e)
        {
            var parameters = (ObservableCollection<RequestParameter>)gridPathParams.ItemsSource;
            parameters.Add(new RequestParameter { ParameterType = ParameterType.Path, IsRequired = true, Type = "string" });
            gridPathParams.SelectedIndex = parameters.Count - 1;
            gridPathParams.Focus();
            gridPathParams.BeginEdit();
        }

        private void BtnAddHeader_Click(object sender, RoutedEventArgs e)
        {
            var headers = (ObservableCollection<RequestHeader>)gridHeaders.ItemsSource;
            headers.Add(new RequestHeader());
            gridHeaders.SelectedIndex = headers.Count - 1;
            gridHeaders.Focus();
            gridHeaders.BeginEdit();
        }

        // 折叠/展开参数区域
        private void BtnCollapseParams_Click(object sender, RoutedEventArgs e)
        {
            if (paramsRow.Height.Value > 0)
            {
                // 保存当前高度
                paramsRow.Tag = paramsRow.Height;
                // 折叠
                paramsRow.Height = new GridLength(0);
                // 更改箭头方向
                arrowParams.Data = Geometry.Parse("M0,8 L8,0 L16,8");
            }
            else
            {
                // 恢复高度
                if (paramsRow.Tag != null)
                {
                    paramsRow.Height = (GridLength)paramsRow.Tag;
                }
                else
                {
                    paramsRow.Height = new GridLength(1, GridUnitType.Star);
                }
                // 更改箭头方向
                arrowParams.Data = Geometry.Parse("M0,0 L8,8 L16,0");
            }
        }

        // 折叠/展开响应区域
        private void BtnCollapseResponse_Click(object sender, RoutedEventArgs e)
        {
            var grid = this.Content as Grid;
            var mainGrid = grid.Children[1] as Grid;
            var responseRow = mainGrid.RowDefinitions[3];
            
            if (responseRow.Height.Value > 0)
            {
                // 保存当前高度
                responseRow.Tag = responseRow.Height;
                // 折叠
                responseRow.Height = new GridLength(0);
                // 更改箭头方向
                arrowResponse.Data = Geometry.Parse("M0,8 L8,0 L16,8");
            }
            else
            {
                // 恢复高度
                if (responseRow.Tag != null)
                {
                    responseRow.Height = (GridLength)responseRow.Tag;
                }
                else
                {
                    responseRow.Height = new GridLength(1, GridUnitType.Star);
                }
                // 更改箭头方向
                arrowResponse.Data = Geometry.Parse("M0,0 L8,8 L16,0");
            }
        }

        // 窗口按键事件处理
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 已移除F11快捷键功能
        }
    }
}