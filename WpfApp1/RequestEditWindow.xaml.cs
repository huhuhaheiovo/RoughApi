using System.Windows;
using WpfApp1.Models;

namespace WpfApp1
{
    public partial class RequestEditWindow : Window
    {
        public RequestModel Request { get; private set; }

        public RequestEditWindow(RequestModel request)
        {
            InitializeComponent();
            Request = request;
            
            // 初始化界面
            txtName.Text = request.Name;
            txtDescription.Text = request.Description;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("请输入接口名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            // 保存数据
            Request.Name = txtName.Text.Trim();
            Request.Description = txtDescription.Text.Trim();
            
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 