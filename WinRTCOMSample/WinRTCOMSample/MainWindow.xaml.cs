using Microsoft.UI.Xaml;
using System;
using WinRTCOMServer;

namespace WinRTCOMSample
{
    internal sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (COMService Service = new COMService())
                {
                    ServiceStatus.Text = $"Service Status: {await Service.CheckStatusAsync()}";
                    ServiceHello.Text = $"Service Hello: {await Service.HelloAsync("John")}";
                }
            }
            catch (Exception ex)
            {
                ServiceStatus.Text = $"Service Status: False";
                ServiceHello.Text = $"Service Hello: Error - {(string.IsNullOrEmpty(ex.Message) ? ex.GetType().FullName : ex.Message)}";
            }
        }
    }
}
